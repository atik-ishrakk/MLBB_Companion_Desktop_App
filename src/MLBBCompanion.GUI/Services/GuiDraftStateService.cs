using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBBCompanion.GUI.Services;

/// <summary>
/// Single-source-of-truth reactive draft state manager for the desktop Windows GUI.
/// Handles 5v5 picks, bans, dynamic ban counts, spell/lane assignments, 6-item builds, and auto-sync.
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

    private static string GetDefaultLaneForSlot(int slotIndex) => slotIndex switch
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

    public void SelectHero(string side, int slotIndex, Hero? hero, bool isBan = false)
    {
        if (isBan)
        {
            var banList = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyBans : EnemyBans;
            if (slotIndex >= 0 && slotIndex < banList.Count)
            {
                banList[slotIndex] = hero;
            }
        }
        else
        {
            var picks = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyPicks : EnemyPicks;
            if (slotIndex >= 0 && slotIndex < 5)
            {
                picks[slotIndex] = hero;
                if (hero != null)
                {
                    ApplyDefaultBuild(hero, side, slotIndex);
                }
            }
        }

        NotifyChanged();
    }

    public void SetSpell(string side, int slotIndex, string spellId)
    {
        var spells = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllySpells : EnemySpells;
        if (slotIndex >= 0 && slotIndex < 5)
        {
            spells[slotIndex] = spellId;
            NotifyChanged();
        }
    }

    public void SetLane(string side, int slotIndex, string lane)
    {
        var lanes = side.Equals("ally", StringComparison.OrdinalIgnoreCase) ? AllyLanes : EnemyLanes;
        if (slotIndex >= 0 && slotIndex < 5)
        {
            lanes[slotIndex] = lane;
            NotifyChanged();
        }
    }

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

    public void ResetDraft()
    {
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
