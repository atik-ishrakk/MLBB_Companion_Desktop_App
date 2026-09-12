using System.Text.Json.Serialization;

namespace MLBB.Core.Entities;

/// <summary>
/// Dynamic detection anchor configuration for game phases.
/// </summary>
public class PhaseAnchorEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("x1")]
    public int X1 { get; set; }

    [JsonPropertyName("y1")]
    public int Y1 { get; set; }

    [JsonPropertyName("x2")]
    public int X2 { get; set; }

    [JsonPropertyName("y2")]
    public int Y2 { get; set; }

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.65;

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Request to crop an anchor area directly from an uploaded screenshot and save to detection database.
/// </summary>
public class CropAnchorRequest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = string.Empty;

    [JsonPropertyName("x1")]
    public int X1 { get; set; }

    [JsonPropertyName("y1")]
    public int Y1 { get; set; }

    [JsonPropertyName("x2")]
    public int X2 { get; set; }

    [JsonPropertyName("y2")]
    public int Y2 { get; set; }

    [JsonPropertyName("threshold")]
    public double Threshold { get; set; } = 0.65;

    [JsonPropertyName("image_base64")]
    public string ImageBase64 { get; set; } = string.Empty;
}

/// <summary>
/// Request to crop any slot area (e.g. hero pick card or ban) from an uploaded screenshot to the database.
/// </summary>
public class CropSlotRequest
{
    [JsonPropertyName("slot_type")]
    public string SlotType { get; set; } = "hero_pick"; // "hero_pick", "phase_anchor", "ban"

    [JsonPropertyName("target_name")]
    public string TargetName { get; set; } = string.Empty;

    [JsonPropertyName("sub_dir")]
    public string? SubDir { get; set; } // "ally_picks", "enemy_picks", "anchors"

    [JsonPropertyName("x1")]
    public int X1 { get; set; }

    [JsonPropertyName("y1")]
    public int Y1 { get; set; }

    [JsonPropertyName("x2")]
    public int X2 { get; set; }

    [JsonPropertyName("y2")]
    public int Y2 { get; set; }

    [JsonPropertyName("image_base64")]
    public string ImageBase64 { get; set; } = string.Empty;
}
