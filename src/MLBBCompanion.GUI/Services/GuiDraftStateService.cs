using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBBCompanion.GUI.Services;

/// <summary>
/// Single-source-of-truth reactive draft state manager for the desktop Windows GUI.
/// Handles 5v5 picks, bans, dynamic ban counts, spell/lane assignments, 6-item builds, and auto-sync.
/// Core logic enforces:
/// - Single assignment per hero across all picks and bans (no duplicates).
/// - Unique lanes per team (EXP, Jungle, Mid, Gold, Roam cannot be duplicate on the same team).
/// - Spells and Equipment can be multiple.
/// - Locked state when all slots are filled to prevent draft pick from getting cleared.
/// </summary>
public class GuiDraftStateService
{
    private readonly IHeroDataService _heroDataService;

    public int MaxBans { get; private set; } = 5;
    public Hero?[] AllyPicks { get; } = new Hero?[5];
    public Hero?[] EnemyPicks { get; } = new Hero?[5];
    public List<Hero?> AllyBans { get; } = new();
    public List<Hero?> EnemyBans { get; } = new();
    public Item?[][] AllyEquipments { get; } = new Item?[5][];
    public Item?[][] EnemyEquipments { get; } = new Item?[5][];
    public string[] AllySpells { get; } = new string[5];
    public string[] EnemySpells { get; } = new string[5];
    public string[] AllyLanes { get; } = new string[5];
    public string[] EnemyLanes { get; } = new string[5];

    public int MyHeroIndex { get; set; } = 0;
    public string SelectedSide { get; set; } = "ally";
    public int SelectedSlotIndex { get; set; } = 0;
    public string CurrentGamePhase { get; set; } = "Draft Pick";
    public bool AutoCvSync { get; set; } = false;

    /// <summary>
    /// When true, the draft is locked: picks, bans, and lanes cannot be cleared or overwritten
    /// by CV background sync or phase changes. Triggered automatically when all 10 pick slots are filled.
    /// </summary>
    public bool IsLocked { get; set; } = false;

    public bool AreAllPicksFilled =>
        AllyPicks.All(h => h != null) && EnemyPicks.All(h => h != null);

    public event Action? OnStateChanged;

    public GuiDraftStateService(IHeroDataService heroDataService)
    {
        _heroDataService = heroDataService;

        for (int i = 0; i < 5; i++)
        {
            AllyEquipments[i] = new Item?[6];
            EnemyEquipments[i] = new Item?[6];
            AllySpells[i] = (i == 1) ? "retribution" : "flicker";
            EnemySpells[i] = (i == 1) ? "retribution" : "flicker";
            AllyLanes[i] = GetDefaultLaneForSlot(i);
            EnemyLanes[i] = GetDefaultLaneForSlot(i);
        }

        SetBanFormat(5, suppressNotification: true);
    }

    public static string GetDefaultLaneForSlot(int slotIndex) => slotIndex switch
    {
        0 => "EXP",
        1 => "Jungle",
        2 => "Mid",
        3 => "Gold",
        4 => "Roam",
        _ => "Mid"
    };

    public void SetBanFormat(int count, bool suppressNotification = false)
    {
        MaxBans = Math.Clamp(count, 3, 5);

        while (AllyBans.Count < MaxBans) AllyBans.Add(null);
        while (AllyBans.Count > MaxBans) AllyBans.RemoveAt(AllyBans.Count - 1);

        while (EnemyBans.Count < MaxBans) EnemyBans.Add(null);
        while (EnemyBans.Count > MaxBans) EnemyBans.RemoveAt(EnemyBans.Count - 1);

        if (!suppressNotification) NotifyChanged();
    }

    /// <summary>
    /// Returns all hero IDs currently picked or banned across both teams.
    /// Optional excludeHeroId allows the slot currently being edited to not be blocked.
    /// </summary>
    public HashSet<string> GetAllAssignedHeroIds(string? excludeHeroId = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < 5; i++)
        {
            if (AllyPicks[i] != null && !string.Equals(AllyPicks[i]!.Id, excludeHeroId, StringComparison.OrdinalIgnoreCase))
                set.Add(AllyPicks[i]!.Id);
            if (EnemyPicks[i] != null && !string.Equals(EnemyPicks[i]!.Id, excludeHeroId, StringComparison.OrdinalIgnoreCase))
                set.Add(EnemyPicks[i]!.Id);
        }

        for (int i = 0; i < AllyBans.Count; i++)
        {
            if (AllyBans[i] != null && !string.Equals(AllyBans[i]!.Id, excludeHeroId, StringComparison.OrdinalIgnoreCase))
                set.Add(AllyBans[i]!.Id);
        }

        for (int i = 0; i < EnemyBans.Count; i++)
        {
            if (EnemyBans[i] != null && !string.Equals(EnemyBans[i]!.Id, excludeHeroId, StringComparison.OrdinalIgnoreCase))
                set.Add(EnemyBans[i]!.Id);
        }

        return set;
    }

    /// <summary>
    /// Selects a hero for a pick or ban slot. Enforces global hero uniqueness:
    /// A hero pick or hero ban cannot be multiple. If the hero is already assigned to any other
    /// slot on either team, it is automatically removed from the previous slot.
    /// Automatically engages locked state once all 10 pick slots are filled.
    /// </summary>
    public void SelectHero(string side, int slotIndex, Hero? hero, bool isBan = false)
    {
        bool isAlly = side.Equals("ally", StringComparison.OrdinalIgnoreCase);

        if (hero != null)
        {
            // Enforce hero uniqueness across all picks and bans on both teams
            RemoveHeroFromOtherSlots(hero.Id, side, slotIndex, isBan);
        }

        if (isBan)
        {
            var banList = isAlly ? AllyBans : EnemyBans;
            if (slotIndex >= 0 && slotIndex < banList.Count)
            {
                banList[slotIndex] = hero;
            }
        }
        else
        {
            var picks = isAlly ? AllyPicks : EnemyPicks;
            if (slotIndex >= 0 && slotIndex < 5)
            {
                picks[slotIndex] = hero;
                if (hero != null)
                {
                    ApplyDefaultBuild(hero, side, slotIndex);
                }
            }
        }

        // Automatic lock transition when all 10 pick slots are filled
        if (AreAllPicksFilled && !IsLocked)
        {
            IsLocked = true;
        }

        NotifyChanged();
    }

    private void RemoveHeroFromOtherSlots(string heroId, string targetSide, int targetSlotIndex, bool targetIsBan)
    {
        bool isTargetAlly = targetSide.Equals("ally", StringComparison.OrdinalIgnoreCase);

        // Check Ally Picks
        for (int i = 0; i < 5; i++)
        {
            if (!(isTargetAlly && !targetIsBan && i == targetSlotIndex))
            {
                if (AllyPicks[i] != null && string.Equals(AllyPicks[i]!.Id, heroId, StringComparison.OrdinalIgnoreCase))
                {
                    AllyPicks[i] = null;
                }
            }
        }

        // Check Enemy Picks
        for (int i = 0; i < 5; i++)
        {
            if (!(!isTargetAlly && !targetIsBan && i == targetSlotIndex))
            {
                if (EnemyPicks[i] != null && string.Equals(EnemyPicks[i]!.Id, heroId, StringComparison.OrdinalIgnoreCase))
                {
                    EnemyPicks[i] = null;
                }
            }
        }

        // Check Ally Bans
        for (int i = 0; i < AllyBans.Count; i++)
        {
            if (!(isTargetAlly && targetIsBan && i == targetSlotIndex))
            {
                if (AllyBans[i] != null && string.Equals(AllyBans[i]!.Id, heroId, StringComparison.OrdinalIgnoreCase))
                {
                    AllyBans[i] = null;
                }
            }
        }

        // Check Enemy Bans
        for (int i = 0; i < EnemyBans.Count; i++)
        {
            if (!(!isTargetAlly && targetIsBan && i == targetSlotIndex))
            {
                if (EnemyBans[i] != null && string.Equals(EnemyBans[i]!.Id, heroId, StringComparison.OrdinalIgnoreCase))
                {
                    EnemyBans[i] = null;
                }
            }
        }
    }

    /// <summary>
    /// Spells can be multiple across heroes. No uniqueness restriction.
    /// </summary>
    public void SetSpell(string side, int slotIndex, string spellId)
    {
        var spells = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllySpells : EnemySpells;
        if (slotIndex >= 0 && slotIndex < 5)
        {
            spells[slotIndex] = spellId;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Lanes cannot be multiple on the same team. If another slot on the team already has this lane,
    /// the lanes are swapped between the two slots so that EXP, Jungle, Mid, Gold, and Roam remain 100% unique.
    /// </summary>
    public void SetLane(string side, int slotIndex, string lane)
    {
        var lanes = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyLanes : EnemyLanes;
        if (slotIndex >= 0 && slotIndex < 5)
        {
            string currentLane = lanes[slotIndex];
            int existingSlot = Array.FindIndex(lanes, l => string.Equals(l, lane, StringComparison.OrdinalIgnoreCase));
            if (existingSlot >= 0 && existingSlot != slotIndex)
            {
                // Swap lanes with the other slot so lanes are never duplicate on the team
                lanes[existingSlot] = currentLane;
            }

            lanes[slotIndex] = lane;
            NotifyChanged();
        }
    }

    /// <summary>
    /// Equipment can be multiple across heroes or within a hero build. No uniqueness restriction.
    /// </summary>
    public void SetItem(string side, int slotIndex, int itemIndex, Item? item)
    {
        var builds = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyEquipments : EnemyEquipments;
        if (slotIndex >= 0 && slotIndex < 5 && itemIndex >= 0 && itemIndex < 6)
        {
            builds[slotIndex][itemIndex] = item;
            NotifyChanged();
        }
    }

    public void ClearSlot(string side, int slotIndex, bool isBan = false)
    {
        if (isBan)
        {
            var banList = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyBans : EnemyBans;
            if (slotIndex >= 0 && slotIndex < banList.Count)
            {
                banList[slotIndex] = null;
            }
        }
        else
        {
            var picks = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyPicks : EnemyPicks;
            var builds = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyEquipments : EnemyEquipments;
            if (slotIndex >= 0 && slotIndex < 5)
            {
                picks[slotIndex] = null;
                for (int i = 0; i < 6; i++) builds[slotIndex][i] = null;
            }
        }

        NotifyChanged();
    }

    public void SetGamePhase(string phase)
    {
        if (CurrentGamePhase != phase)
        {
            CurrentGamePhase = phase;
            NotifyChanged();
        }
    }

    public void ToggleLock()
    {
        IsLocked = !IsLocked;
        NotifyChanged();
    }

    /// <summary>
    /// Resets draft picks, bans, equipment, spells, lanes, and unlocks the draft state.
    /// </summary>
    public void ResetDraft()
    {
        IsLocked = false;

        for (int i = 0; i < 5; i++)
        {
            AllyPicks[i] = null;
            EnemyPicks[i] = null;
            for (int k = 0; k < 6; k++)
            {
                AllyEquipments[i][k] = null;
                EnemyEquipments[i][k] = null;
            }
            AllySpells[i] = (i == 1) ? "retribution" : "flicker";
            EnemySpells[i] = (i == 1) ? "retribution" : "flicker";
            AllyLanes[i] = GetDefaultLaneForSlot(i);
            EnemyLanes[i] = GetDefaultLaneForSlot(i);
        }

        for (int i = 0; i < AllyBans.Count; i++) AllyBans[i] = null;
        for (int i = 0; i < EnemyBans.Count; i++) EnemyBans[i] = null;

        NotifyChanged();
    }

    private void ApplyDefaultBuild(Hero hero, string side, int slotIndex)
    {
        var role = (hero.Role ?? string.Empty).ToLowerInvariant();
        string[] defaultItemIds = role switch
        {
            var r when r.Contains("marksman") => ["swift_boots", "corrosion_scythe", "demon_hunter_sword", "golden_staff", "wind_of_nature", "blade_of_despair"],
            var r when r.Contains("mage") => ["arcane_boots", "genius_wand", "lightning_truncheon", "holy_crystal", "divine_glaive", "blood_wings"],
            var r when r.Contains("assassin") => ["magic_shoes", "blade_of_the_heptaseas", "hunter_strike", "malefic_roar", "endless_battle", "immortality"],
            var r when r.Contains("fighter") => ["warrior_boots", "war_axe", "brute_force_breastplate", "endless_battle", "athena_shield", "immortality"],
            var r when r.Contains("tank") => ["tough_boots", "dominance_ice", "athena_shield", "antique_cuirass", "blade_armor", "immortality"],
            var r when r.Contains("support") => ["magic_shoes", "flaskoftheoasis", "dominance_ice", "oracle", "athena_shield", "immortality"],
            _ => ["warrior_boots", "endless_battle", "blade_of_despair", "athena_shield", "antique_cuirass", "immortality"]
        };

        var builds = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyEquipments : EnemyEquipments;
        for (int i = 0; i < 6; i++)
        {
            if (i < defaultItemIds.Length)
            {
                builds[slotIndex][i] = _heroDataService.GetItemById(defaultItemIds[i]) 
                                      ?? new Item { Id = defaultItemIds[i], Name = defaultItemIds[i].Replace("_", " ") };
            }
        }
    }

    public void NotifyChanged()
    {
        OnStateChanged?.Invoke();
    }
}
