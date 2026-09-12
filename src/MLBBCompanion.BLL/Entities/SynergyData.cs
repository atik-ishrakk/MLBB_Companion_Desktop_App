namespace MLBB.Core.Entities;

/// <summary>
/// Domain model for esports Wombo-Combo ally synergies.
/// </summary>
public class SynergyData
{
    public string HeroId { get; set; } = string.Empty;
    public List<string> Pairs { get; set; } = [];
    public double Bonus { get; set; }
    public string Reason { get; set; } = string.Empty;
}

