using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Esports draft strategy & counter engine.
/// Computes win-probability, situational item counters, lane-specific counter-picks,
/// ally synergies, threat composition matrix, and macro advice.
/// </summary>
public interface IDraftRecommenderService
{
    DraftRecommendationResult AnalyzeDraft(DraftRecommendationRequest request);
    IReadOnlyList<string> GetFirstPickPriorities();
    IReadOnlyList<HeroPickRecommendation> GetCounterRecommendations(string enemyHeroId);
    IReadOnlyList<HeroPickRecommendation> GetSynergyRecommendations(string allyHeroId);
}

