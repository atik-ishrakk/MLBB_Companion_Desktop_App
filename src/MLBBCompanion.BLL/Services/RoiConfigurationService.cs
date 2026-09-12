using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Service managing dynamic ROIs, pixel conversions, and vision thresholds.
/// </summary>
public class RoiConfigurationService : IRoiConfigurationService
{
    private readonly IRoiRepository _repository;
    private readonly IVisionEngine? _visionEngine;

    public RoiConfigurationService(IRoiRepository repository, IVisionEngine? visionEngine = null)
    {
        _repository = repository;
        _visionEngine = visionEngine;
    }

    public RoiConfiguration GetCurrentConfiguration() => _repository.GetConfiguration();

    public RoiConfiguration SaveConfiguration(RoiConfiguration newConfig)
    {
        _repository.SaveConfiguration(newConfig);
        _visionEngine?.ClearSlotCache();
        return _repository.GetConfiguration();
    }

    public RoiConfiguration ResetToDefault()
    {
        var cfg = _repository.ResetToDefault();
        _visionEngine?.ClearSlotCache();
        return cfg;
    }

    public RoiRect GetRoiPixels(int frameWidth, int frameHeight, string roiKey)
    {
        var config = _repository.GetConfiguration();
        if (!config.Rois.TryGetValue(roiKey, out var coords) || coords.Count != 4)
        {
            return RoiRect.Empty;
        }

        if (string.Equals(config.CoordinateSystem, "normalized", StringComparison.OrdinalIgnoreCase))
        {
            int x = (int)Math.Round(coords[0] * frameWidth);
            int y = (int)Math.Round(coords[1] * frameHeight);
            int w = (int)Math.Round(coords[2] * frameWidth);
            int h = (int)Math.Round(coords[3] * frameHeight);

            x = Math.Clamp(x, 0, Math.Max(0, frameWidth - 1));
            y = Math.Clamp(y, 0, Math.Max(0, frameHeight - 1));
            w = Math.Clamp(w, 1, Math.Max(1, frameWidth - x));
            h = Math.Clamp(h, 1, Math.Max(1, frameHeight - y));

            return new RoiRect(x, y, w, h);
        }
        else
        {
            int x = (int)Math.Round(coords[0]);
            int y = (int)Math.Round(coords[1]);
            int w = (int)Math.Round(coords[2]);
            int h = (int)Math.Round(coords[3]);

            return new RoiRect(x, y, w, h);
        }
    }

    public double GetThreshold(string key, double fallback)
    {
        var config = _repository.GetConfiguration();
        if (config.Thresholds.TryGetValue(key, out var val))
        {
            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }
}

