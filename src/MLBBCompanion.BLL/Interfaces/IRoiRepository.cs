using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Data access abstraction for ROI boundaries and CV threshold configurations.
/// </summary>
public interface IRoiRepository
{
    RoiConfiguration GetConfiguration();
    void SaveConfiguration(RoiConfiguration config);
    RoiConfiguration ResetToDefault();
}

