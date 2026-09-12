using System.Text.Json.Serialization;

namespace MLBB.Core.Entities;

/// <summary>
/// Request for draft recommendations.
/// </summary>
public class DraftRecommendationRequest
{
    [JsonPropertyName("ally_picks")]
    public List<string> AllyPicks { get; set; } = [];

    [JsonPropertyName("enemy_picks")]
    public List<string> EnemyPicks { get; set; } = [];

    [JsonPropertyName("ally_bans")]
    public List<string> AllyBans { get; set; } = [];

    [JsonPropertyName("enemy_bans")]
    public List<string> EnemyBans { get; set; } = [];

    [JsonPropertyName("preferred_role")]
    public string? PreferredRole { get; set; }

    [JsonPropertyName("preferred_lane")]
    public string? PreferredLane { get; set; }
}

/// <summary>
/// Recommended hero option with counter and synergy scores.
/// </summary>
public class HeroPickRecommendation
{
    [JsonPropertyName("hero_id")]
    public string HeroId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("suggested_lane")]
    public string SuggestedLane { get; set; } = LaneRole.None;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("counter_score")]
    public double CounterScore { get; set; }

    [JsonPropertyName("synergy_score")]
    public double SynergyScore { get; set; }

    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = [];

    [JsonPropertyName("counters_enemies")]
    public List<string> CountersEnemies { get; set; } = [];

    [JsonPropertyName("synergizes_with")]
    public List<string> SynergizesWith { get; set; } = [];

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }
}

/// <summary>
/// Composition metrics and role coverage for a team.
/// </summary>
public class TeamCompositionAnalysis
{
    [JsonPropertyName("physical_damage_percent")]
    public double PhysicalDamagePercent { get; set; }

    [JsonPropertyName("magic_damage_percent")]
    public double MagicDamagePercent { get; set; }

    [JsonPropertyName("crowd_control_score")]
    public double CrowdControlScore { get; set; }

    [JsonPropertyName("durability_score")]
    public double DurabilityScore { get; set; }

    [JsonPropertyName("scaling_score")]
    public double ScalingScore { get; set; }

    [JsonPropertyName("objective_score")]
    public double ObjectiveScore { get; set; }

    [JsonPropertyName("missing_roles")]
    public List<string> MissingRoles { get; set; } = [];

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = [];

    [JsonPropertyName("vulnerabilities")]
    public List<string> Vulnerabilities { get; set; } = [];
}

/// <summary>
/// Full draft analysis & esports recommendation response.
/// </summary>
public class DraftRecommendationResult
{
    [JsonPropertyName("ally_win_probability")]
    public double AllyWinProbability { get; set; } = 50.0;

    [JsonPropertyName("enemy_win_probability")]
    public double EnemyWinProbability { get; set; } = 50.0;

    [JsonPropertyName("overall_advice")]
    public string OverallAdvice { get; set; } = string.Empty;

    [JsonPropertyName("recommended_picks")]
    public List<HeroPickRecommendation> RecommendedPicks { get; set; } = [];

    [JsonPropertyName("first_pick_priorities")]
    public List<string> FirstPickPriorities { get; set; } = [];

    [JsonPropertyName("item_counter_suggestions")]
    public List<ItemCounterSuggestion> ItemCounterSuggestions { get; set; } = [];

    [JsonPropertyName("ally_composition")]
    public TeamCompositionAnalysis AllyComposition { get; set; } = new();

    [JsonPropertyName("enemy_composition")]
    public TeamCompositionAnalysis EnemyComposition { get; set; } = new();
}

public class ItemCounterSuggestion
{
    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("counters_hero")]
    public string CountersHero { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

