using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Service coordinating process checks, ADB window focus, and CV anchor matchers for game phase determination.
/// </summary>
public class PhaseDetectionService : IPhaseDetectionService
{
    private readonly IProcessManagerService _processManager;
    private readonly IAdbService _adbService;
    private readonly IVisionEngine _visionEngine;
    private readonly IScreenCaptureProvider _screenCapture;

    public PhaseDetectionService(
        IProcessManagerService processManager,
        IAdbService adbService,
        IVisionEngine visionEngine,
        IScreenCaptureProvider screenCapture)
    {
        _processManager = processManager;
        _adbService = adbService;
        _visionEngine = visionEngine;
        _screenCapture = screenCapture;
    }

    public async Task<(string phase, string? subPhase, double confidence, string details)> GetCurrentGamePhaseAsync(CapturedFrame? frame = null)
    {
        var (bsRunning, _) = _processManager.IsBlueStacksRunning();
        if (!bsRunning)
        {
            return (GamePhase.Standby, null, 0.0, "BlueStacks App Player is not running.");
        }

        // Check ADB connection & app state
        bool adbConnected = await _adbService.PingDeviceAsync();
        if (adbConnected)
        {
            var (appState, _) = await _adbService.GetFocusedAppStateAsync();
            if (appState == "STANDBY")
            {
                return (GamePhase.BluestackOn, null, 1.0, "BlueStacks Launcher (Direct ADB Watchdog)");
            }

            if (appState == "LOADING")
            {
                return (GamePhase.Loading, null, 1.0, "MLBB Game Splash Loading (Direct ADB)");
            }
        }

        // Capture frame if not provided
        frame ??= await _screenCapture.CaptureActiveWindowAsync();
        if (frame == null || !frame.IsValid)
        {
            return (GamePhase.BluestackOn, null, 0.5, "BlueStacks active (No frame captured for CV phase check).");
        }

        // Run CV Phase detection
        return _visionEngine.DetectPhase(frame);
    }
}

