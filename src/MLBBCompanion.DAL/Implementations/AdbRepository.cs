using MLBBCompanion.BLL.Interfaces;
using MLBBCompanion.BLL.Models;

namespace MLBBCompanion.DAL.Implementations;

public class AdbRepository : IAdbRepository
{
    private readonly IEnvironmentDetector _detector;
    private readonly IProcessRunner _processRunner;
    private readonly CompanionConfiguration _config;

    public AdbRepository(
        IEnvironmentDetector detector,
        IProcessRunner processRunner,
        CompanionConfiguration? config = null)
    {
        _detector = detector;
        _processRunner = processRunner;
        _config = config ?? new CompanionConfiguration();
    }

    public async Task<DeviceStatus> GetDeviceStatusAsync()
    {
        var adbPath = _detector.DetectAdbPath();
        if (string.IsNullOrEmpty(adbPath))
        {
            return new DeviceStatus { Bluestacks = false, GameRunning = false, Status = "offline" };
        }

        var (code, devicesOutput, _) = await _processRunner.RunAsync(adbPath, "devices", timeoutSeconds: 2);

        if (code != 0 || !HasConnectedDevice(devicesOutput))
        {
            // Quick connect attempt to default BlueStacks ADB port
            await _processRunner.RunAsync(adbPath, $"connect 127.0.0.1:{_config.DefaultAdbPort}", timeoutSeconds: 1);
            (code, devicesOutput, _) = await _processRunner.RunAsync(adbPath, "devices", timeoutSeconds: 2);
        }

        if (code != 0 || !HasConnectedDevice(devicesOutput))
        {
            return new DeviceStatus { Bluestacks = false, GameRunning = false, Status = "offline" };
        }

        // Query process list with responsive timeout
        var (psCode, psOutput, _) = await _processRunner.RunAsync(adbPath, "shell ps", timeoutSeconds: 2);
        var gameRunning = false;

        if (psCode == 0 && !string.IsNullOrEmpty(psOutput))
        {
            var psLower = psOutput.ToLowerInvariant();
            gameRunning = _config.PackageNames.Any(pkg => psLower.Contains(pkg.ToLowerInvariant()));
        }

        return new DeviceStatus
        {
            Bluestacks = true,
            GameRunning = gameRunning,
            Status = gameRunning ? "live" : "standby"
        };
    }

    public async Task<bool> ForceStopGameAsync()
    {
        var adbPath = _detector.DetectAdbPath();
        if (string.IsNullOrEmpty(adbPath))
        {
            return false;
        }

        var success = false;
        foreach (var pkg in _config.PackageNames)
        {
            var (code, _, _) = await _processRunner.RunAsync(adbPath, $"shell am force-stop {pkg}");
            if (code == 0)
            {
                success = true;
            }
        }

        return success;
    }

    private static bool HasConnectedDevice(string? devicesOutput)
    {
        if (string.IsNullOrWhiteSpace(devicesOutput)) return false;

        foreach (var line in devicesOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("List of devices") || trimmed.StartsWith("* daemon"))
            {
                continue;
            }

            if (trimmed.EndsWith("device", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
