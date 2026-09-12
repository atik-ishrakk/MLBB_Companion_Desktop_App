namespace MLBB.Core.Interfaces;

/// <summary>
/// Service communicating with BlueStacks Android subsystem over ADB.
/// </summary>
public interface IAdbService
{
    bool IsConnected { get; }
    string? CurrentDevice { get; }
    Dictionary<string, int> GetConfiguredPorts();
    Task<bool> PingDeviceAsync();
    Task<(string appState, string packageName)> GetFocusedAppStateAsync();
    Task<byte[]?> CaptureScreencapAsync();
}

