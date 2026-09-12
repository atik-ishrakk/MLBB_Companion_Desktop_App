using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Business logic service for managing calibration ROI boundaries and thresholds.
/// </summary>
public interface IRoiConfigurationService
{
    RoiConfiguration GetCurrentConfiguration();
    RoiConfiguration SaveConfiguration(RoiConfiguration newConfig);
    RoiConfiguration ResetToDefault();
    RoiRect GetRoiPixels(int frameWidth, int frameHeight, string roiKey);
    double GetThreshold(string key, double fallback);
}

