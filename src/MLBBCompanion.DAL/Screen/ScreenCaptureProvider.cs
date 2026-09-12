using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using MLBB.Core.Interfaces;
using OpenCvSharp;

namespace MLBB.Infrastructure.Screen;

/// <summary>
/// High-performance screen capture provider prioritizing ADB framebuffer and falling back to Win32 window capture.
/// </summary>
[SupportedOSPlatform("windows")]
public class ScreenCaptureProvider : IScreenCaptureProvider
{
    private readonly ILogger<ScreenCaptureProvider> _logger;
    private readonly IAdbService _adbService;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const int SW_RESTORE = 9;

    public ScreenCaptureProvider(ILogger<ScreenCaptureProvider> logger, IAdbService adbService)
    {
        _logger = logger;
        _adbService = adbService;
    }

    public bool IsWindowAvailable()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        return FindBlueStacksWindow() != IntPtr.Zero;
    }

    public static IntPtr FindBlueStacksWindow()
    {
        IntPtr found = IntPtr.Zero;
        string[] targetProcessNames = ["HD-Player", "BlueStacks", "BlueStacksAppPlayer", "BstkSVC", "dnplayer", "Nox", "MuMuPlayer"];
        string[] titleKeywords = ["BlueStacks", "HD-Player", "Mobile Legends", "LDPlayer", "MuMu", "Nox"];

        EnumWindows((hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd))
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid != 0)
                {
                    try
                    {
                        using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                        string procName = proc.ProcessName;

                        // Exclude browser and development processes from being mistaken for emulator
                        if (procName.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("msedge", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("firefox", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("brave", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("opera", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("code", StringComparison.OrdinalIgnoreCase) ||
                            procName.Equals("devenv", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        // Check process name first (HD-Player, BlueStacks, etc.)
                        if (targetProcessNames.Any(p => procName.Equals(p, StringComparison.OrdinalIgnoreCase)))
                        {
                            found = hWnd;
                            return false;
                        }

                        var sb = new System.Text.StringBuilder(256);
                        GetWindowText(hWnd, sb, 256);
                        string title = sb.ToString();
                        if (!string.IsNullOrEmpty(title) &&
                            !title.Contains("Companion", StringComparison.OrdinalIgnoreCase) &&
                            titleKeywords.Any(k => title.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        {
                            found = hWnd;
                            return false;
                        }
                    }
                    catch { }
                }
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public CaptureDiagnostics LastDiagnostics { get; private set; } = new();

    public async Task<CapturedFrame?> CaptureActiveWindowAsync(CancellationToken cancellationToken = default)
    {
        bool adbConnected = false;
        // 1. Try ADB screencap first
        try
        {
            if (!_adbService.IsConnected)
            {
                await _adbService.PingDeviceAsync();
            }

            adbConnected = _adbService.IsConnected;
            if (adbConnected)
            {
                byte[]? adbBytes = await _adbService.CaptureScreencapAsync();
                if (adbBytes != null && adbBytes.Length > 100)
                {
                    using var mat = Mat.FromImageData(adbBytes, ImreadModes.Color);
                    if (!mat.Empty())
                    {
                        Mat finalMat = mat;
                        byte[] finalBytes = adbBytes;
                        if (mat.Width != 1920 || mat.Height != 1080)
                        {
                            var resized = new Mat();
                            Cv2.Resize(mat, resized, new OpenCvSharp.Size(1920, 1080), 0, 0, InterpolationFlags.Lanczos4);
                            finalMat = resized;
                            Cv2.ImEncode(".png", finalMat, out finalBytes);
                        }

                        LastDiagnostics = new CaptureDiagnostics
                        {
                            Code = "OK",
                            Message = $"Frame captured via ADB screencap ({finalMat.Width}x{finalMat.Height} HD).",
                            AdbConnected = true,
                            BlueStacksWindowFound = true,
                            MethodAttempted = "ADB"
                        };
                        return new CapturedFrame
                        {
                            Buffer = finalBytes,
                            Width = finalMat.Width,
                            Height = finalMat.Height,
                            Source = "ADB",
                            NativeHandle = finalMat.Clone()
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ADB frame capture attempt failed.");
        }

        // 2. Try BlueStacks desktop window capture (Client area with automatic 16:9 sidebar trimming and HD upscale)
        return await Task.Run<CapturedFrame?>(() =>
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LastDiagnostics = new CaptureDiagnostics
                {
                    Code = "UNSUPPORTED_PLATFORM",
                    Message = "Win32 window capture is only available on Windows.",
                    AdbConnected = adbConnected,
                    BlueStacksWindowFound = false,
                    MethodAttempted = "Win32"
                };
                return null;
            }

            try
            {
                IntPtr hwnd = FindBlueStacksWindow();
                if (hwnd == IntPtr.Zero)
                {
                    LastDiagnostics = new CaptureDiagnostics
                    {
                        Code = "BLUESTACKS_NOT_FOUND",
                        Message = "BlueStacks 5 window not found (process or window title inactive).",
                        AdbConnected = adbConnected,
                        BlueStacksWindowFound = false,
                        MethodAttempted = "Win32 + ADB"
                    };
                    return null;
                }

                if (IsIconic(hwnd))
                {
                    ShowWindow(hwnd, SW_RESTORE);
                    Thread.Sleep(50);
                }

                // Get Client Area (excludes window title bar and outer window borders)
                if (!GetClientRect(hwnd, out var clientRect))
                {
                    LastDiagnostics = new CaptureDiagnostics
                    {
                        Code = "CAPTURE_FAILED",
                        Message = "Failed to query BlueStacks client rectangle.",
                        AdbConnected = adbConnected,
                        BlueStacksWindowFound = true,
                        MethodAttempted = "Win32"
                    };
                    return null;
                }

                int clientWidth = clientRect.Right - clientRect.Left;
                int clientHeight = clientRect.Bottom - clientRect.Top;

                if (clientWidth <= 100 || clientHeight <= 100)
                {
                    LastDiagnostics = new CaptureDiagnostics
                    {
                        Code = "CAPTURE_FAILED",
                        Message = $"BlueStacks client window dimensions too small ({clientWidth}x{clientHeight}).",
                        AdbConnected = adbConnected,
                        BlueStacksWindowFound = true,
                        MethodAttempted = "Win32"
                    };
                    return null;
                }

                var pt = new POINT { X = 0, Y = 0 };
                ClientToScreen(hwnd, ref pt);

                // In BlueStacks windowed mode, the right toolbar is attached to the client area.
                // Mobile Legends is strictly 16:9. Compute the 16:9 game area width to strip off the toolbar:
                int gameWidth = clientWidth;
                double currentRatio = (double)clientWidth / clientHeight;
                if (currentRatio > 1.80)
                {
                    gameWidth = (int)Math.Round(clientHeight * 16.0 / 9.0);
                    gameWidth = Math.Clamp(gameWidth, 100, clientWidth);
                }

                using var bmp = new Bitmap(gameWidth, clientHeight, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(pt.X, pt.Y, 0, 0, new System.Drawing.Size(gameWidth, clientHeight), CopyPixelOperation.SourceCopy);
                }

                // If window was shrunk on desktop, upscale client area to 1920x1080 HD using high quality bicubic interpolation
                Bitmap exportBmp = bmp;
                bool needDisposeExport = false;
                if (gameWidth != 1920 || clientHeight != 1080)
                {
                    var scaledBmp = new Bitmap(1920, 1080, PixelFormat.Format24bppRgb);
                    using (var gScale = Graphics.FromImage(scaledBmp))
                    {
                        gScale.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        gScale.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        gScale.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        gScale.DrawImage(bmp, new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, gameWidth, clientHeight), GraphicsUnit.Pixel);
                    }
                    exportBmp = scaledBmp;
                    needDisposeExport = true;
                }

                using var ms = new MemoryStream();
                exportBmp.Save(ms, ImageFormat.Png);
                byte[] bytes = ms.ToArray();
                if (needDisposeExport) exportBmp.Dispose();

                using var mat = Mat.FromImageData(bytes, ImreadModes.Color);

                LastDiagnostics = new CaptureDiagnostics
                {
                    Code = "OK",
                    Message = $"Frame captured successfully via Win32 client area (1920x1080 HD, original {gameWidth}x{clientHeight}).",
                    AdbConnected = adbConnected,
                    BlueStacksWindowFound = true,
                    MethodAttempted = "Win32"
                };

                return new CapturedFrame
                {
                    Buffer = bytes,
                    Width = 1920,
                    Height = 1080,
                    Source = (gameWidth == 1920 && clientHeight == 1080) ? "Win32Window" : "Win32 (Upscaled HD)",
                    NativeHandle = mat.Clone()
                };
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Win32 desktop window capture error.");
                LastDiagnostics = new CaptureDiagnostics
                {
                    Code = "FRAME_CAPTURE_FAILED",
                    Message = $"Win32 capture error: {ex.Message}",
                    AdbConnected = adbConnected,
                    BlueStacksWindowFound = true,
                    MethodAttempted = "Win32"
                };
                return null;
            }
        }, cancellationToken);
    }

    public CapturedFrame? DecodeBase64Frame(string base64Image)
    {
        if (string.IsNullOrWhiteSpace(base64Image))
            return null;

        try
        {
            string clean = base64Image;
            int commaIdx = clean.IndexOf(',');
            if (commaIdx >= 0)
            {
                clean = clean[(commaIdx + 1)..];
            }

            byte[] bytes = Convert.FromBase64String(clean);
            using var mat = Mat.FromImageData(bytes, ImreadModes.Color);
            if (mat.Empty())
                return null;

            return new CapturedFrame
            {
                Buffer = bytes,
                Width = mat.Width,
                Height = mat.Height,
                Source = "Base64Payload",
                NativeHandle = mat.Clone()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding base64 image data.");
            return null;
        }
    }
}

