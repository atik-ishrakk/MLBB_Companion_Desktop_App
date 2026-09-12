namespace MLBB.Core.Interfaces;

/// <summary>
/// Container for a captured frame in memory.
/// </summary>
public class CapturedFrame : IDisposable
{
    public byte[]? Buffer { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string Source { get; set; } = "Unknown";
    public bool IsValid => Buffer != null && Buffer.Length > 0 && Width > 0 && Height > 0;
    public object? NativeHandle { get; set; } // Can store native Mat or pointer for high-speed zero-copy operations

    public void Dispose()
    {
        if (NativeHandle is IDisposable d)
        {
            d.Dispose();
            NativeHandle = null;
        }
    }
}

/// <summary>
/// Diagnostic telemetry regarding frame acquisition attempts.
/// </summary>
public class CaptureDiagnostics
{
    public string Code { get; set; } = "OK"; // "OK", "BLUESTACKS_NOT_FOUND", "DEVICE_OFFLINE", "CAPTURE_FAILED", "UNSUPPORTED_PLATFORM"
    public string Message { get; set; } = string.Empty;
    public bool BlueStacksWindowFound { get; set; }
    public bool AdbConnected { get; set; }
    public string? MethodAttempted { get; set; }
    public long TimestampMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}

/// <summary>
/// Hardware abstraction for grabbing frames from BlueStacks emulator window or ADB stream.
/// </summary>
public interface IScreenCaptureProvider
{
    Task<CapturedFrame?> CaptureActiveWindowAsync(CancellationToken cancellationToken = default);
    CapturedFrame? DecodeBase64Frame(string base64Image);
    bool IsWindowAvailable();
    CaptureDiagnostics LastDiagnostics { get; }
}

