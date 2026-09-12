using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Services;
using MLBB.Infrastructure.Repositories;
using Xunit;

namespace MLBB.Tests;

public class HeroDataServiceTests
{
    private readonly HeroDataService _service;

    public HeroDataServiceTests()
    {
        var logger = NullLogger<HeroRepository>.Instance;
        var repo = new HeroRepository(logger);
        _service = new HeroDataService(repo);
    }

    [Fact]
    public void GetAllHeroes_LoadsCompleteHeroDatabase()
    {
        var heroes = _service.GetAllHeroes();
        Assert.NotEmpty(heroes);
        Assert.True(heroes.Count >= 100, $"Expected >= 100 heroes, found {heroes.Count}");
    }

    [Fact]
    public void GetHeroById_ReturnsSpecificHeroDetails()
    {
        var hero = _service.GetHeroById("gord");
        Assert.NotNull(hero);
        Assert.Equal("Gord", hero.Name);
        Assert.Equal("Mage", hero.Role);
        Assert.Equal("Magic", hero.DamageType);
        Assert.NotEmpty(hero.CounteredBy);
    }

    [Fact]
    public void GetHeroesByRole_FiltersCorrectly()
    {
        var tanks = _service.GetHeroesByRole("Tank");
        Assert.NotEmpty(tanks);
        Assert.All(tanks, h => Assert.Equal("Tank", h.Role, ignoreCase: true));
    }

    [Fact]
    public void SearchHeroes_FindsByNameAndSpecialty()
    {
        var results = _service.SearchHeroes("Miya");
        Assert.NotEmpty(results);
        Assert.Contains(results, h => h.Id.Equals("miya", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetAllItems_LoadsCompleteItemCatalog()
    {
        var items = _service.GetAllItems();
        Assert.NotEmpty(items);
        Assert.True(items.Count >= 50, $"Expected >= 50 items, found {items.Count}");
    }
}
