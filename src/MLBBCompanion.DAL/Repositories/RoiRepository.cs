using System.Text.Json;
using Microsoft.Extensions.Logging;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;

namespace MLBB.Infrastructure.Repositories;

/// <summary>
/// Repository managing frame regions JSON persistence and calibration overrides.
/// </summary>
public class RoiRepository : IRoiRepository
{
    private readonly ILogger<RoiRepository> _logger;
    private readonly string _configFilePath;
    private readonly object _lock = new();
    private RoiConfiguration? _cachedConfig;

    public RoiRepository(ILogger<RoiRepository> logger)
    {
        _logger = logger;

        var candidates = new[]
        {
            WorkspacePathResolver.ResolvePath("Assets", "frame_regions.json"),
            WorkspacePathResolver.ResolvePath("frame_regions.json"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "frame_regions.json"),
            Path.Combine(AppContext.BaseDirectory, "frame_regions.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "frame_regions.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "frame_regions.json")
        };

        _configFilePath = candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    public RoiConfiguration GetConfiguration()
    {
        lock (_lock)
        {
            if (_cachedConfig != null) return _cachedConfig;

            if (File.Exists(_configFilePath))
            {
                try
                {
                    string json = File.ReadAllText(_configFilePath);
                    _cachedConfig = JsonSerializer.Deserialize<RoiConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error loading frame_regions.json. Falling back to default.");
                }
            }

            _cachedConfig ??= CreateDefaultConfiguration();
            return _cachedConfig;
        }
    }

    public void SaveConfiguration(RoiConfiguration config)
    {
        lock (_lock)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                string? dir = Path.GetDirectoryName(_configFilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                File.WriteAllText(_configFilePath, json);
                _cachedConfig = config;
                _logger.LogInformation("Saved updated ROI configuration to: {Path}", _configFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save ROI configuration.");
                throw;
            }
        }
    }

    public RoiConfiguration ResetToDefault()
    {
        var defaultConfig = CreateDefaultConfiguration();
        SaveConfiguration(defaultConfig);
        return defaultConfig;
    }

    public static RoiConfiguration CreateDefaultConfiguration()
    {
        return new RoiConfiguration
        {
            Version = "2.0",
            CoordinateSystem = "normalized",
            RawPixelSpecCoordinateSystem = "pixels",
            Thresholds = new Dictionary<string, object>
            {
                ["ban_threshold"] = 0.45,
                ["pick_threshold"] = 0.50,
                ["item_threshold"] = 0.50,
                ["empty_ban_threshold"] = 0.45,
                ["empty_pick_ally_threshold"] = 0.50,
                ["empty_pick_enemy_threshold"] = 0.55,
                ["empty_slot_std"] = 12.0,
                ["dark_ban_mean"] = 10.0,
                ["dark_ban_std"] = 7.0,
                ["dark_pick_mean"] = 10.0,
                ["dark_pick_std"] = 8.0,
                ["min_edge_density"] = 0.025,
                ["min_top_margin"] = 0.03,
                ["phase_confidence_high"] = 0.65,
                ["phase_confidence_low"] = 0.60,
                ["temporal_confirmation_frames"] = 3,
                ["mirror_check"] = true,
                ["clahe_clip_limit"] = 2.0,
                ["clahe_tile_grid"] = 8
            },
            Rois = new Dictionary<string, List<double>>
            {
                // Ally Bans (80x80 at y=6px, 1920x1080)
                ["ally_ban_0"] = [30.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["ally_ban_1"] = [140.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["ally_ban_2"] = [250.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["ally_ban_3"] = [360.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["ally_ban_4"] = [470.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],

                // Enemy Bans
                ["enemy_ban_0"] = [1370.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["enemy_ban_1"] = [1480.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["enemy_ban_2"] = [1590.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["enemy_ban_3"] = [1700.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],
                ["enemy_ban_4"] = [1810.0 / 1920, 6.0 / 1080, 80.0 / 1920, 80.0 / 1080],

                // Ally Picks (210x132)
                ["ally_pick_0"] = [0.0 / 1920, 125.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["ally_pick_1"] = [0.0 / 1920, 298.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["ally_pick_2"] = [0.0 / 1920, 470.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["ally_pick_3"] = [0.0 / 1920, 642.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["ally_pick_4"] = [0.0 / 1920, 814.0 / 1080, 210.0 / 1920, 132.0 / 1080],

                // Enemy Picks
                ["enemy_pick_0"] = [1710.0 / 1920, 125.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["enemy_pick_1"] = [1710.0 / 1920, 298.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["enemy_pick_2"] = [1710.0 / 1920, 470.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["enemy_pick_3"] = [1710.0 / 1920, 642.0 / 1080, 210.0 / 1920, 132.0 / 1080],
                ["enemy_pick_4"] = [1710.0 / 1920, 814.0 / 1080, 210.0 / 1920, 132.0 / 1080]
            }
        };
    }
}

