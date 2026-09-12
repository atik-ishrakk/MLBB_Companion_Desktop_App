using System.Text.Json.Serialization;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Entities;

/// <summary>
/// Scored candidate hero from template/ZNCC matching.
/// </summary>
public class CandidateMatch
{
    [JsonPropertyName("hero")]
    public string Hero { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("lane")]
    public string? Lane { get; set; }

    [JsonPropertyName("shape")]
    public string? Shape { get; set; }
}

/// <summary>
/// Detailed detection result for a single draft slot (ban or pick).
/// </summary>
public class SlotMatch
{
    [JsonPropertyName("slot")]
    public int Slot { get; set; }

    [JsonPropertyName("side")]
    public string Side { get; set; } = "ally"; // "ally" or "enemy"

    [JsonPropertyName("hero")]
    public string? Hero { get; set; }

    [JsonPropertyName("matched_hero")]
    public string? MatchedHero { get; set; }

    [JsonPropertyName("lane")]
    public string? Lane { get; set; }

    [JsonPropertyName("detected_lane")]
    public string? DetectedLane { get; set; }

    [JsonPropertyName("spell")]
    public string? Spell { get; set; }

    [JsonPropertyName("detected_spell")]
    public string? DetectedSpell { get; set; }

    [JsonPropertyName("lane_evidence")]
    public Dictionary<string, object>? LaneEvidence { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("shape")]
    public string Shape { get; set; } = "round"; // "round" or "rectangle"

    [JsonPropertyName("crop_thumb")]
    public string? CropThumb { get; set; }

    [JsonPropertyName("edge_density")]
    public double? EdgeDensity { get; set; }

    [JsonPropertyName("rejection_reason")]
    public string? RejectionReason { get; set; }

    [JsonPropertyName("top_k_candidates")]
    public List<CandidateMatch> TopKCandidates { get; set; } = [];

    [JsonPropertyName("empty_similarity")]
    public double EmptySimilarity { get; set; }

    [JsonIgnore]
    public string? HeroId { get => MatchedHero ?? Hero; set { MatchedHero = value; Hero = value; } }

    [JsonIgnore]
    public string? SpellName { get => DetectedSpell ?? Spell; set { DetectedSpell = value; Spell = value; } }
}

/// <summary>
/// Telemetry breakdown of execution timings in milliseconds.
/// </summary>
public class PipelineTimings
{
    [JsonPropertyName("extraction_ms")]
    public double ExtractionMs { get; set; }

    [JsonPropertyName("ban_inference_ms")]
    public double BanInferenceMs { get; set; }

    [JsonPropertyName("pick_inference_ms")]
    public double PickInferenceMs { get; set; }

    [JsonPropertyName("ally_pick_ms")]
    public double AllyPickMs { get; set; }

    [JsonPropertyName("enemy_pick_ms")]
    public double EnemyPickMs { get; set; }

    [JsonPropertyName("pick_assignment_ms")]
    public double PickAssignmentMs { get; set; }

    [JsonPropertyName("ban_slot_timings_ms")]
    public List<double>? BanSlotTimingsMs { get; set; }

    [JsonPropertyName("ban_min_slot_ms")]
    public double BanMinSlotMs { get; set; }

    [JsonPropertyName("ban_max_slot_ms")]
    public double BanMaxSlotMs { get; set; }

    [JsonPropertyName("ban_avg_slot_ms")]
    public double BanAvgSlotMs { get; set; }

    [JsonPropertyName("total_ms")]
    public double TotalMs { get; set; }
}

/// <summary>
/// Live draft scan response payload for /cv/draft-scan.
/// </summary>
public class DraftScanResult
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // "active", "standby", "transient"

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = GamePhase.Standby;

    [JsonPropertyName("subPhase")]
    public string? SubPhase { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; } = 0.95;

    [JsonPropertyName("is_transient")]
    public bool? IsTransient { get; set; }

    [JsonPropertyName("is_simulated")]
    public bool IsSimulated { get; set; }

    [JsonPropertyName("diagnostic_code")]
    public string DiagnosticCode { get; set; } = "OK";

    [JsonPropertyName("phase_detection_method")]
    public string PhaseDetectionMethod { get; set; } = "anchor";

    [JsonPropertyName("capture_details")]
    public CaptureDiagnostics? CaptureDetails { get; set; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, object>? Metrics { get; set; }

    [JsonPropertyName("ally_slots")]
    public List<string> AllySlots { get; set; } = [];

    [JsonPropertyName("enemy_slots")]
    public List<string> EnemySlots { get; set; } = [];

    [JsonPropertyName("ally_lanes")]
    public List<string?> AllyLanes { get; set; } = [];

    [JsonPropertyName("ally_spells")]
    public List<string?> AllySpells { get; set; } = [];

    [JsonPropertyName("enemy_spells")]
    public List<string?> EnemySpells { get; set; } = [];

    [JsonPropertyName("ally_lane_evidence")]
    public List<object> AllyLaneEvidence { get; set; } = [];

    [JsonPropertyName("ally_bans")]
    public List<string> AllyBans { get; set; } = [];

    [JsonPropertyName("enemy_bans")]
    public List<string> EnemyBans { get; set; } = [];

    [JsonPropertyName("timings")]
    public PipelineTimings Timings { get; set; } = new();

    [JsonPropertyName("pipeline_timings_ms")]
    public PipelineTimings PipelineTimingsMs => Timings;

    [JsonPropertyName("debug_bans_array")]
    public List<SlotMatch> DebugBansArray { get; set; } = [];

    [JsonPropertyName("debug_ally_picks_array")]
    public List<SlotMatch> DebugAllyPicksArray { get; set; } = [];

    [JsonPropertyName("debug_enemy_picks_array")]
    public List<SlotMatch> DebugEnemyPicksArray { get; set; } = [];

    [JsonPropertyName("debug_picks_array")]
    public List<SlotMatch> DebugPicksArray { get; set; } = [];

    [JsonIgnore]
    public List<SlotMatch> Bans { get => DebugBansArray; set => DebugBansArray = value; }

    [JsonIgnore]
    public List<SlotMatch> AllyPicks { get => DebugAllyPicksArray; set => DebugAllyPicksArray = value; }

    [JsonIgnore]
    public List<SlotMatch> EnemyPicks { get => DebugEnemyPicksArray; set => DebugEnemyPicksArray = value; }

    [JsonIgnore]
    public List<string?> Lanes { get => AllyLanes; set => AllyLanes = value; }

    [JsonIgnore]
    public string DetectedPhase { get => Phase; set => Phase = value; }

    [JsonIgnore]
    public string? DetectedSubPhase { get => SubPhase; set => SubPhase = value; }

    [JsonIgnore]
    public string? Details { get => Message; set => Message = value; }
}

/// <summary>
/// Payload returned by /api/analyze_frame.
/// </summary>
public class FrameAnalysisResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("is_transient")]
    public bool? IsTransient { get; set; }

    [JsonPropertyName("phase")]
    public string Phase { get; set; } = GamePhase.Standby;

    [JsonPropertyName("subPhase")]
    public string? SubPhase { get; set; }

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }

    [JsonPropertyName("metrics")]
    public Dictionary<string, object>? Metrics { get; set; }

    [JsonPropertyName("bans")]
    public List<SlotMatch> Bans { get; set; } = [];

    [JsonPropertyName("picks")]
    public List<SlotMatch> Picks { get; set; } = [];

    [JsonPropertyName("ally_spells")]
    public List<string?> AllySpells { get; set; } = [];

    [JsonPropertyName("enemy_spells")]
    public List<string?> EnemySpells { get; set; } = [];

    [JsonPropertyName("ally_lane_evidence")]
    public List<object> AllyLaneEvidence { get; set; } = [];

    [JsonPropertyName("debug_bans_array")]
    public List<SlotMatch> DebugBansArray { get; set; } = [];

    [JsonPropertyName("debug_ally_picks_array")]
    public List<SlotMatch> DebugAllyPicksArray { get; set; } = [];

    [JsonPropertyName("debug_enemy_picks_array")]
    public List<SlotMatch> DebugEnemyPicksArray { get; set; } = [];

    [JsonPropertyName("debug_picks_array")]
    public List<SlotMatch> DebugPicksArray { get; set; } = [];

    [JsonPropertyName("pipeline_timings_ms")]
    public PipelineTimings PipelineTimingsMs { get; set; } = new();

    [JsonPropertyName("timings")]
    public PipelineTimings Timings => PipelineTimingsMs;
}

