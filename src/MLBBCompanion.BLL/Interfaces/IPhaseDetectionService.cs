using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Service determining the current MLBB game phase (Standby, BlueStacks On, Homepage, Lobby, Draft Pick, In Game).
/// </summary>
public interface IPhaseDetectionService
{
    Task<(string phase, string? subPhase, double confidence, string details)> GetCurrentGamePhaseAsync(CapturedFrame? frame = null);
}

