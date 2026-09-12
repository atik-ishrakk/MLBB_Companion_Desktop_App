using MLBBCompanion.BLL.Models;

namespace MLBBCompanion.BLL.Interfaces;

public interface IAdbRepository
{
    Task<DeviceStatus> GetDeviceStatusAsync();
    Task<bool> ForceStopGameAsync();
}
