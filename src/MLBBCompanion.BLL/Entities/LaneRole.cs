namespace MLBB.Core.Entities;

/// <summary>
/// MLBB Lane and Role positions.
/// </summary>
public static class LaneRole
{
    public const string Exp = "EXP";
    public const string Jungle = "Jungle";
    public const string Mid = "Mid";
    public const string Gold = "Gold";
    public const string Roam = "Roam";
    public const string None = "None";

    public static readonly string[] AllLanes = [Exp, Jungle, Mid, Gold, Roam];

    public static bool IsValid(string? lane) =>
        !string.IsNullOrEmpty(lane) && AllLanes.Any(l => string.Equals(l, lane, StringComparison.OrdinalIgnoreCase));
}

