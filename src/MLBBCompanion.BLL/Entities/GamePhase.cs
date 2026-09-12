namespace MLBB.Core.Entities;

/// <summary>
/// Canonical game phases across ADB telemetry, CV matchers, and client UI.
/// </summary>
public static class GamePhase
{
    public const string Standby = "Standby";
    public const string BluestackOn = "Bluestack On";
    public const string Loading = "Loading";
    public const string Homepage = "Homepage";
    public const string Lobby = "Lobby";
    public const string Matchmaking = "Matchmaking";
    public const string MatchStart = "Match Start";
    public const string DraftPick = "Draft Pick";
    public const string Preparation = "Preparation Phase";
    public const string InGame = "In Game";
    public const string Scoreboard = "In Game ScoreBoard";
    public const string Unknown = "Unknown";
    public const string NA = "N/A";
    public const string Transient = "Transient";

    public static readonly HashSet<string> NonGameplay = new(StringComparer.OrdinalIgnoreCase)
    {
        Standby,
        BluestackOn,
        Unknown,
        NA,
        Transient
    };

    public static bool IsInDraft(string? phase) =>
        string.Equals(phase, DraftPick, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(phase, Preparation, StringComparison.OrdinalIgnoreCase);
}

