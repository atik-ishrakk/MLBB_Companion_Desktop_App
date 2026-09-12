using System.Text.Json.Serialization;

namespace MLBB.Core.Entities;

/// <summary>
/// Health check model for /health.
/// </summary>
public class HealthStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "online";

    [JsonPropertyName("bluestacks")]
    public bool BlueStacks { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("cv_status")]
    public string CvStatus { get; set; } = "ready";

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }
}

/// <summary>
/// Full telemetry status model for /status.
/// </summary>
public class SystemStatus
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "online";

    [JsonPropertyName("bluestacks")]
    public bool BlueStacks { get; set; }

    [JsonPropertyName("gameRunning")]
    public bool GameRunning { get; set; }

    [JsonPropertyName("connected")]
    public bool Connected { get; set; }

    [JsonPropertyName("gamePhase")]
    public string GamePhase { get; set; } = MLBB.Core.Entities.GamePhase.Standby;

    [JsonPropertyName("subPhase")]
    public string? SubPhase { get; set; }

    [JsonPropertyName("device")]
    public string? Device { get; set; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }
}

/// <summary>
/// Root info model for /.
/// </summary>
public class RootStatus
{
    [JsonPropertyName("app")]
    public string App { get; set; } = "MLBB Companion Suite (.NET 10 ASP.NET Core)";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "online";

    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.0";

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "3-Tier Modular Clean Architecture";

    [JsonPropertyName("matchers")]
    public List<string> Matchers { get; set; } = [
        "PhaseMatcher", "BanMatcher", "AllyPickMatcher", "EnemyPickMatcher"
    ];

    [JsonPropertyName("endpoints")]
    public List<string> Endpoints { get; set; } = [
        "/health", "/status", "/checker/ping", "/cv/initialize",
        "/cv/draft-scan", "/api/analyze_frame", "/api/reload_db", "/launch",
        "/api/close-bluestacks", "/api/stop-server", "/shutdown",
        "/api/get_rois", "/api/save_rois", "/api/reset_rois",
        "/api/heroes", "/api/items", "/api/recommendations/draft"
    ];
}

