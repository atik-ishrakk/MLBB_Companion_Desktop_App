using System.Text.Json.Serialization;

namespace MLBB.Core.Entities;

/// <summary>
/// Bounding box rectangle in pixels or normalized values.
/// </summary>
public record RoiRect(int X, int Y, int Width, int Height)
{
    public static readonly RoiRect Empty = new(0, 0, 0, 0);
}

/// <summary>
/// Threshold configuration for Computer Vision matchers.
/// </summary>
public class DetectionThresholds
{
    [JsonPropertyName("ban_threshold")]
    public double BanThreshold { get; set; } = 0.60;

    [JsonPropertyName("pick_threshold")]
    public double PickThreshold { get; set; } = 0.50;

    [JsonPropertyName("item_threshold")]
    public double ItemThreshold { get; set; } = 0.50;

    [JsonPropertyName("empty_ban_threshold")]
    public double EmptyBanThreshold { get; set; } = 0.45;

    [JsonPropertyName("empty_pick_ally_threshold")]
    public double EmptyPickAllyThreshold { get; set; } = 0.50;

    [JsonPropertyName("empty_pick_enemy_threshold")]
    public double EmptyPickEnemyThreshold { get; set; } = 0.55;

    [JsonPropertyName("empty_slot_std")]
    public double EmptySlotStd { get; set; } = 12.0;

    [JsonPropertyName("dark_ban_mean")]
    public double DarkBanMean { get; set; } = 10.0;

    [JsonPropertyName("dark_ban_std")]
    public double DarkBanStd { get; set; } = 7.0;

    [JsonPropertyName("dark_pick_mean")]
    public double DarkPickMean { get; set; } = 10.0;

    [JsonPropertyName("dark_pick_std")]
    public double DarkPickStd { get; set; } = 8.0;

    [JsonPropertyName("min_edge_density")]
    public double MinEdgeDensity { get; set; } = 0.025;

    [JsonPropertyName("min_top_margin")]
    public double MinTopMargin { get; set; } = 0.03;

    [JsonPropertyName("phase_confidence_high")]
    public double PhaseConfidenceHigh { get; set; } = 0.65;

    [JsonPropertyName("phase_confidence_low")]
    public double PhaseConfidenceLow { get; set; } = 0.60;

    [JsonPropertyName("temporal_confirmation_frames")]
    public int TemporalConfirmationFrames { get; set; } = 3;

    [JsonPropertyName("mirror_check")]
    public bool MirrorCheck { get; set; } = true;

    [JsonPropertyName("clahe_clip_limit")]
    public double ClaheClipLimit { get; set; } = 2.0;

    [JsonPropertyName("clahe_tile_grid")]
    public int ClaheTileGrid { get; set; } = 8;
}

/// <summary>
/// Full configuration payload for calibration and frame ROIs.
/// </summary>
public class RoiConfiguration
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "2.0";

    [JsonPropertyName("coordinate_system")]
    public string CoordinateSystem { get; set; } = "normalized";

    [JsonPropertyName("raw_pixel_spec_coordinate_system")]
    public string? RawPixelSpecCoordinateSystem { get; set; } = "pixels";

    [JsonPropertyName("thresholds")]
    public Dictionary<string, object> Thresholds { get; set; } = [];

    [JsonPropertyName("raw_pixel_spec")]
    public Dictionary<string, object> RawPixelSpec { get; set; } = [];

    [JsonPropertyName("rois")]
    public Dictionary<string, List<double>> Rois { get; set; } = [];
}

