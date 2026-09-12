using MLBBCompanion.BLL.Models;

namespace MLBBCompanion.BLL.Interfaces;

public interface ILauncherService
{
    Task<DeviceStatus> GetStatusAsync();
    void RequestCloseTab();
    void ResetCloseTab();
    bool IsCloseTabRequested();
    Task<bool> StartAllAsync();
    Task<bool> LaunchBlueStacksAsync();
    Task<bool> CloseBlueStacksAsync();
    Task<bool> StopGameAsync();
    Task<bool> OpenDashboardWindowAsync();
    Task<bool> CloseDashboardWindowAsync();
    EnvironmentSummary GetEnvironmentSummary();
}
