using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Thread-safe in-memory simulation state provider for demo live feed testing.
/// </summary>
public class SimulationStateService : ISimulationStateService
{
    private volatile bool _isSimulating;

    public bool IsSimulating
    {
        get => _isSimulating;
        set => _isSimulating = value;
    }

    public bool ToggleSimulation()
    {
        _isSimulating = !_isSimulating;
        return _isSimulating;
    }
}

