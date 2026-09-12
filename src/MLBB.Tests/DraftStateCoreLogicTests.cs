using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Services;
using MLBB.Infrastructure.Repositories;
using MLBBCompanion.GUI.Services;
using Xunit;

namespace MLBB.Tests;

public class DraftStateCoreLogicTests
{
    private readonly GuiDraftStateService _draftState;
    private readonly HeroDataService _heroService;

    public DraftStateCoreLogicTests()
    {
        var repo = new HeroRepository(NullLogger<HeroRepository>.Instance);
        _heroService = new HeroDataService(repo);
        _draftState = new GuiDraftStateService(_heroService);
    }

    [Fact]
    public void HeroPick_CannotBeMultiple_RemovesFromPreviousSlot()
    {
        var chou = _heroService.GetHeroById("chou") ?? new Hero { Id = "chou", Name = "Chou" };

        // Pick Chou in Ally Slot 0
        _draftState.SelectHero("ally", 0, chou);
        Assert.Equal("chou", _draftState.AllyPicks[0]?.Id);

        // Pick Chou in Enemy Slot 2
        _draftState.SelectHero("enemy", 2, chou);
        Assert.Equal("chou", _draftState.EnemyPicks[2]?.Id);

        // Ally Slot 0 must now be null (Chou was moved, cannot exist in multiple slots)
        Assert.Null(_draftState.AllyPicks[0]);
    }

    [Fact]
    public void HeroBan_CannotBeMultiple_AndCannotBeBothPickedAndBanned()
    {
        var fanny = _heroService.GetHeroById("fanny") ?? new Hero { Id = "fanny", Name = "Fanny" };

        // Ban Fanny in Ally Ban 0
        _draftState.SelectHero("ally", 0, fanny, isBan: true);
        Assert.Equal("fanny", _draftState.AllyBans[0]?.Id);

        // Try to ban Fanny in Enemy Ban 1 -> moves to Enemy Ban 1, cleared from Ally Ban 0
        _draftState.SelectHero("enemy", 1, fanny, isBan: true);
        Assert.Equal("fanny", _draftState.EnemyBans[1]?.Id);
        Assert.Null(_draftState.AllyBans[0]);

        // Pick Fanny in Ally Pick 3 -> Fanny removed from Enemy Ban 1, assigned to Ally Pick 3
        _draftState.SelectHero("ally", 3, fanny, isBan: false);
        Assert.Equal("fanny", _draftState.AllyPicks[3]?.Id);
        Assert.Null(_draftState.EnemyBans[1]);
    }

    [Fact]
    public void Lanes_CannotBeMultiple_SwapsLanesOnSameTeam()
    {
        // Initial defaults: [0: EXP, 1: Jungle, 2: Mid, 3: Gold, 4: Roam]
        Assert.Equal("EXP", _draftState.AllyLanes[0]);
        Assert.Equal("Gold", _draftState.AllyLanes[3]);

        // Assign Slot 0 to "Gold" -> Slot 3 had Gold, so Slot 3 receives Slot 0's previous lane ("EXP")
        _draftState.SetLane("ally", 0, "Gold");

        Assert.Equal("Gold", _draftState.AllyLanes[0]);
        Assert.Equal("EXP", _draftState.AllyLanes[3]);

        // Verify all 5 ally lanes remain strictly unique (no duplicates)
        var uniqueLanes = new HashSet<string>(_draftState.AllyLanes, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(5, uniqueLanes.Count);
        Assert.Contains("EXP", uniqueLanes);
        Assert.Contains("Jungle", uniqueLanes);
        Assert.Contains("Mid", uniqueLanes);
        Assert.Contains("Gold", uniqueLanes);
        Assert.Contains("Roam", uniqueLanes);
    }

    [Fact]
    public void EquipmentAndSpells_CanBeMultiple()
    {
        // Multiple heroes can equip the exact same spell
        _draftState.SetSpell("ally", 0, "flicker");
        _draftState.SetSpell("ally", 1, "flicker");
        _draftState.SetSpell("ally", 2, "flicker");

        Assert.Equal("flicker", _draftState.AllySpells[0]);
        Assert.Equal("flicker", _draftState.AllySpells[1]);
        Assert.Equal("flicker", _draftState.AllySpells[2]);

        // Multiple heroes can equip the exact same item
        var item = _heroService.GetItemById("immortality") ?? new Item { Id = "immortality", Name = "Immortality" };
        _draftState.SetItem("ally", 0, 0, item);
        _draftState.SetItem("ally", 1, 0, item);

        Assert.Equal("immortality", _draftState.AllyEquipments[0][0]?.Id);
        Assert.Equal("immortality", _draftState.AllyEquipments[1][0]?.Id);
    }

    [Fact]
    public void DraftLock_EngagesWhenAll10PicksFilled_AndPreventsWipe()
    {
        Assert.False(_draftState.IsLocked);

        var heroes = _heroService.GetAllHeroes();
        Assert.True(heroes.Count >= 10);

        // Fill 5 Ally picks
        for (int i = 0; i < 5; i++)
        {
            _draftState.SelectHero("ally", i, heroes[i]);
        }
        Assert.False(_draftState.IsLocked); // Only 5 picks so far

        // Fill 5 Enemy picks
        for (int i = 0; i < 5; i++)
        {
            _draftState.SelectHero("enemy", i, heroes[5 + i]);
        }

        // All 10 pick slots are now filled -> MUST be locked!
        Assert.True(_draftState.AreAllPicksFilled);
        Assert.True(_draftState.IsLocked);

        // Verify slots remain populated
        for (int i = 0; i < 5; i++)
        {
            Assert.NotNull(_draftState.AllyPicks[i]);
            Assert.NotNull(_draftState.EnemyPicks[i]);
        }

        // ResetDraft must reset and unlock
        _draftState.ResetDraft();
        Assert.False(_draftState.IsLocked);
        Assert.False(_draftState.AreAllPicksFilled);
        Assert.Null(_draftState.AllyPicks[0]);
    }
}
