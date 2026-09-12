namespace MLBB.Core.Interfaces;

/// <summary>
/// Service managing draft live feed simulation state for offline or demonstration testing.
/// </summary>
public interface ISimulationStateService
{
    bool IsSimulating { get; set; }
    bool ToggleSimulation();
}

