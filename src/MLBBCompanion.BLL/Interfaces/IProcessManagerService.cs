namespace MLBB.Core.Interfaces;

/// <summary>
/// Service for Windows OS process management, BlueStacks lifecycle, and port/memory cleanup.
/// </summary>
public interface IProcessManagerService
{
    (bool isRunning, int adbPort) IsBlueStacksRunning();
    Task<(bool success, string message, string? instance)> LaunchBlueStacksAsync();
    Task<(bool success, string message, int processesKilled)> CloseBlueStacksAsync();
    void PurgeRamAndVram();
    Task<int> KillProcessesByPortsAsync(IEnumerable<int> ports, int? excludePid = null);
    void TriggerGracefulShutdown(bool closeEmulator = false);
}

