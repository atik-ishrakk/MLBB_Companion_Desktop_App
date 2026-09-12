using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MLBBCompanion.BLL.Interfaces;
using MLBBCompanion.BLL.Models;

namespace MLBBCompanion.BLL.Services;

public class LauncherService : ILauncherService
{
    private readonly IAdbRepository _adbRepo;
    private readonly IEnvironmentDetector _detector;
    private readonly IProcessRunner _processRunner;
    private readonly IWindowBridge _windowBridge;
    private readonly CompanionConfiguration _config;
    private readonly ILogger<LauncherService> _logger;

    private readonly object _lock = new();
    private bool _closeTabRequested;
    private bool _browserOpened;
    private Process? _browserProcess;
    private Process? _bluestacksProcess;

    public LauncherService(
        IAdbRepository adbRepo,
        IEnvironmentDetector detector,
        IProcessRunner processRunner,
        IWindowBridge windowBridge,
        ILogger<LauncherService> logger,
        CompanionConfiguration? config = null)
    {
        _adbRepo = adbRepo;
        _detector = detector;
        _processRunner = processRunner;
        _windowBridge = windowBridge;
        _logger = logger;
        _config = config ?? new CompanionConfiguration();
    }

    public async Task<DeviceStatus> GetStatusAsync()
    {
        var status = await _adbRepo.GetDeviceStatusAsync();
        lock (_lock)
        {
            status.CloseTab = _closeTabRequested;
            status.BrowserOpened = _browserOpened;
        }
        return status;
    }

    public void RequestCloseTab()
    {
        lock (_lock)
        {
            _closeTabRequested = true;
        }
        _logger.LogInformation("Dashboard tab close requested via status signal.");
    }

    public void ResetCloseTab()
    {
        lock (_lock)
        {
            _closeTabRequested = false;
        }
    }

    public bool IsCloseTabRequested()
    {
        lock (_lock)
        {
            return _closeTabRequested;
        }
    }

    public async Task<bool> StartAllAsync()
    {
        _logger.LogInformation("=== Initiating Full Companion Startup Sequence ===");
        try
        {
            // Launch BlueStacks and Game
            await LaunchBlueStacksAsync();

            _logger.LogInformation("=== Startup Sequence Complete ===");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during startup sequence.");
            return false;
        }
    }

    public async Task<bool> LaunchBlueStacksAsync()
    {
        var hdPlayer = _detector.DetectBlueStacksHdPlayer();
        var launcher = _detector.DetectBlueStacksLauncher();

        if (!string.IsNullOrEmpty(hdPlayer))
        {
            _logger.LogInformation("Starting BlueStacks 5 instance '{Instance}'...", _config.BlueStacksInstance);
            _bluestacksProcess = _processRunner.StartSilent(hdPlayer, $"--instance {_config.BlueStacksInstance}");

            _logger.LogInformation("Waiting for emulator bridge to initialize...");
            await Task.Delay(4500);

            foreach (var package in _config.PackageNames)
            {
                _logger.LogInformation("Requesting package launch: {Package}", package);
                var (code, _, _) = await _processRunner.RunAsync(
                    hdPlayer,
                    $"--instance {_config.BlueStacksInstance} --cmd launchApp --package {package}");

                if (code == 0)
                {
                    _logger.LogInformation("Package {Package} launched successfully.", package);
                    return true;
                }
            }

            return true;
        }

        if (!string.IsNullOrEmpty(launcher))
        {
            _logger.LogInformation("Starting BlueStacks launcher...");
            _bluestacksProcess = _processRunner.StartSilent(launcher, string.Empty);
            return true;
        }

        _logger.LogWarning("BlueStacks 5 was not detected on this system.");
        return false;
    }

    public async Task<bool> CloseBlueStacksAsync()
    {
        _logger.LogInformation("Shutting down BlueStacks 5 emulator...");
        var hdPlayer = _detector.DetectBlueStacksHdPlayer();

        if (!string.IsNullOrEmpty(hdPlayer))
        {
            await _processRunner.RunAsync(hdPlayer, $"--instance {_config.BlueStacksInstance} --cmd quit");
        }

        if (_bluestacksProcess is { HasExited: false })
        {
            try
            {
                _bluestacksProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process already exited
            }
        }

        // Clean up lingering HD-Player processes
        await _processRunner.RunAsync("taskkill", "/F /IM HD-Player.exe /T");

        _bluestacksProcess = null;
        _logger.LogInformation("BlueStacks 5 shutdown sequence finished.");
        return true;
    }

    public async Task<bool> StopGameAsync()
    {
        _logger.LogInformation("Stopping Mobile Legends game process via ADB...");
        var success = await _adbRepo.ForceStopGameAsync();
        if (success)
        {
            _logger.LogInformation("Mobile Legends game process stopped.");
        }
        else
        {
            _logger.LogWarning("Could not stop game process via ADB.");
        }
        return success;
    }

    public Task<bool> OpenDashboardWindowAsync()
    {
        ResetCloseTab();
        var browser = _detector.DetectBrowser();
        var dashboardUrl = $"http://{_config.Host}:{_config.Port}/";

        try
        {
            if (!string.IsNullOrEmpty(browser))
            {
                _logger.LogInformation("Opening full screen dashboard window via {Browser}...", Path.GetFileName(browser));
                _browserProcess = _processRunner.StartSilent(
                    browser,
                    $"--app={dashboardUrl} --start-fullscreen");
            }
            else
            {
                _logger.LogInformation("Opening dashboard in system default browser...");
                Process.Start(new ProcessStartInfo(dashboardUrl) { UseShellExecute = true });
            }

            lock (_lock)
            {
                _browserOpened = true;
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch dashboard window.");
            lock (_lock)
            {
                _browserOpened = false;
            }
            return Task.FromResult(false);
        }
    }

    public Task<bool> CloseDashboardWindowAsync()
    {
        _logger.LogInformation("Closing dashboard window session...");

        // 1. Signal status server so the web app triggers window.close()
        RequestCloseTab();

        // 2. Win32 bridge to close active 'MLBB Companion' window or tab
        var closedViaBridge = _windowBridge.CloseWindowOrTabByTitle("MLBB Companion");
        if (closedViaBridge)
        {
            _logger.LogInformation("Closed active companion window via Win32 bridge.");
        }

        // 3. Terminate process handle if still alive
        if (_browserProcess is { HasExited: false })
        {
            try
            {
                _browserProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // Already exited
            }
        }

        _browserProcess = null;
        lock (_lock)
        {
            _browserOpened = false;
        }

        return Task.FromResult(true);
    }

    public EnvironmentSummary GetEnvironmentSummary()
    {
        return _detector.GetSummary();
    }
}
