using System.Runtime.InteropServices;
using System.Text;
using MLBBCompanion.BLL.Interfaces;

namespace MLBBCompanion.DAL.Implementations;

public class WindowBridge : IWindowBridge
{
    private const uint WM_CLOSE = 0x0010;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_W = 0x57;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nint dwExtraInfo);

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    public bool CloseWindowOrTabByTitle(string titleSubstring)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        var matchedHwnds = new List<nint>();

        EnumWindows((hWnd, _) =>
        {
            if (IsWindowVisible(hWnd))
            {
                var length = GetWindowTextLength(hWnd);
                if (length > 0)
                {
                    var sb = new StringBuilder(length + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    var title = sb.ToString();
                    if (title.Contains(titleSubstring, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedHwnds.Add(hWnd);
                    }
                }
            }
            return true;
        }, nint.Zero);

        var closedAny = false;

        foreach (var hwnd in matchedHwnds)
        {
            try
            {
                SetForegroundWindow(hwnd);
                Thread.Sleep(60);

                // Post WM_CLOSE for standalone app windows
                PostMessage(hwnd, WM_CLOSE, nint.Zero, nint.Zero);

                // Send Ctrl + W keystroke for browser tab strips
                keybd_event(VK_CONTROL, 0, 0, nint.Zero);
                keybd_event(VK_W, 0, 0, nint.Zero);
                Thread.Sleep(20);
                keybd_event(VK_W, 0, KEYEVENTF_KEYUP, nint.Zero);
                keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, nint.Zero);

                closedAny = true;
            }
            catch
            {
                // Ignore transient window dispatch errors
            }
        }

        return closedAny;
    }
}
