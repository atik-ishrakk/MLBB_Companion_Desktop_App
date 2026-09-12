using System.Text.Json.Serialization;

namespace MLBB.Core.Entities;

/// <summary>
/// Domain model for an MLBB Hero.
/// </summary>
public class Hero
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("damageType")]
    public string DamageType { get; set; } = string.Empty;

    [JsonPropertyName("specialty")]
    public List<string> Specialty { get; set; } = [];

    [JsonPropertyName("strengths")]
    public List<string> Strengths { get; set; } = [];

    [JsonPropertyName("weaknesses")]
    public List<string> Weaknesses { get; set; } = [];

    [JsonPropertyName("counteredBy")]
    public List<CounterInfo> CounteredBy { get; set; } = [];

    [JsonPropertyName("itemCounters")]
    public List<ItemCounterInfo> ItemCounters { get; set; } = [];

    [JsonPropertyName("tricks")]
    public HeroTrick? Tricks { get; set; }

    [JsonPropertyName("avatar")]
    public string? Avatar { get; set; }

    [JsonPropertyName("cornerAvatar")]
    public string? CornerAvatar { get; set; }
}

public class CounterInfo
{
    [JsonPropertyName("heroId")]
    public string HeroId { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class ItemCounterInfo
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class HeroTrick
{
    [JsonPropertyName("combos")]
    public List<ComboInfo> Combos { get; set; } = [];

    [JsonPropertyName("spellSynergy")]
    public string? SpellSynergy { get; set; }

    [JsonPropertyName("proTips")]
    public string? ProTips { get; set; }
}

public class ComboInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sequence")]
    public string Sequence { get; set; } = string.Empty;
}

