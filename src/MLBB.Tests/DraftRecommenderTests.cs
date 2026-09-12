using Microsoft.Extensions.Logging.Abstractions;
using MLBB.Core.Entities;
using MLBB.Core.Services;
using MLBB.Infrastructure.Repositories;
using Xunit;

namespace MLBB.Tests;

public class DraftRecommenderTests
{
    private readonly DraftRecommenderService _recommender;
    private readonly HeroDataService _heroDataService;

    public DraftRecommenderTests()
    {
        var logger = NullLogger<HeroRepository>.Instance;
        var heroRepo = new HeroRepository(logger);
        _heroDataService = new HeroDataService(heroRepo);
        _recommender = new DraftRecommenderService(_heroDataService);
    }

    [Fact]
    public void GetFirstPickPriorities_ReturnsTopEsportsHeroes()
    {
        var priorities = _recommender.GetFirstPickPriorities();
        Assert.NotNull(priorities);
        Assert.Contains("chou", priorities);
        Assert.Contains("fredrinn", priorities);
        Assert.Contains("valentina", priorities);
    }

    [Fact]
    public void AnalyzeDraft_WithEmptyDraft_ReturnsBalancedProbabilities()
    {
        var req = new DraftRecommendationRequest();
        var res = _recommender.AnalyzeDraft(req);

        Assert.NotNull(res);
        Assert.InRange(res.AllyWinProbability, 45.0, 55.0);
        Assert.NotEmpty(res.RecommendedPicks);
    }

    [Fact]
    public void AnalyzeDraft_WithEnemyMarksman_RecommendsCounterPick()
    {
        var req = new DraftRecommendationRequest
        {
            EnemyPicks = ["miya"]
        };

        var res = _recommender.AnalyzeDraft(req);
        Assert.NotNull(res);
        Assert.NotEmpty(res.RecommendedPicks);

        // Counter pick recommendation should contain Saber or Gusion with positive counter score
        var top = res.RecommendedPicks.First();
        Assert.True(top.Score > 50.0);
    }

    [Fact]
    public void AnalyzeDraft_WithAllyJohnson_RecommendsOdetteSynergy()
    {
        var req = new DraftRecommendationRequest
        {
            AllyPicks = ["johnson"]
        };

        var res = _recommender.AnalyzeDraft(req);
        Assert.NotNull(res);

        var odette = res.RecommendedPicks.FirstOrDefault(p => p.HeroId.Equals("odette", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(odette);
        Assert.True(odette.SynergyScore > 0);
    }

    [Fact]
    public void AnalyzeDraft_WithEnemyPhysicalHeavy_SuggestsPhysicalDefenseItems()
    {
        var req = new DraftRecommendationRequest
        {
            EnemyPicks = ["miya", "hanabi", "layla"]
        };

        var res = _recommender.AnalyzeDraft(req);
        Assert.NotNull(res);
        Assert.NotEmpty(res.ItemCounterSuggestions);
    }
}
