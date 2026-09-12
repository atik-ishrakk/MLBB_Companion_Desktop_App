using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Esports strategy recommendation engine ported from draft_recommender.js.
/// </summary>
public class DraftRecommenderService : IDraftRecommenderService
{
    private readonly IHeroDataService _heroDataService;

    private static readonly string[] FirstPicks =
    [
        "chou", "fredrinn", "valentina", "mathilda", "beatrix", "joy", "floryn", "hilda", "guinevere", "ling"
    ];

    private static readonly Dictionary<string, (double Durability, double Cc, double Objective, double Scaling, string DamageType)> RoleWeights =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Tank"] = (35, 25, 20, 10, "Physical"),
            ["Fighter"] = (22, 15, 20, 15, "Physical"),
            ["Mage"] = (10, 20, 10, 15, "Magic"),
            ["Marksman"] = (8, 10, 30, 35, "Physical"),
            ["Assassin"] = (10, 12, 20, 25, "Physical"),
            ["Support"] = (15, 22, 10, 12, "Magic")
        };

    public DraftRecommenderService(IHeroDataService heroDataService)
    {
        _heroDataService = heroDataService;
    }

    public IReadOnlyList<string> GetFirstPickPriorities() => FirstPicks;

    public DraftRecommendationResult AnalyzeDraft(DraftRecommendationRequest request)
    {
        var allHeroes = _heroDataService.GetAllHeroes();
        var heroMap = allHeroes.ToDictionary(h => h.Id.ToLowerInvariant(), h => h);
        var synergies = _heroDataService.GetAllSynergies();

        var allyPicks = (request.AllyPicks ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h) && !h.Equals("Empty", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.ToLowerInvariant())
            .ToList();

        var enemyPicks = (request.EnemyPicks ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h) && !h.Equals("Empty", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.ToLowerInvariant())
            .ToList();

        var bans = (request.AllyBans ?? []).Concat(request.EnemyBans ?? [])
            .Where(h => !string.IsNullOrWhiteSpace(h) && !h.Equals("Empty", StringComparison.OrdinalIgnoreCase))
            .Select(h => h.ToLowerInvariant())
            .ToHashSet();

        var takenHeroes = new HashSet<string>(allyPicks.Concat(enemyPicks).Concat(bans));

        // 1. Team Composition Analysis
        var allyComp = AnalyzeComposition(allyPicks, heroMap);
        var enemyComp = AnalyzeComposition(enemyPicks, heroMap);

        // 2. Score Candidates
        var candidates = new List<HeroPickRecommendation>();

        foreach (var hero in allHeroes)
        {
            var hId = hero.Id.ToLowerInvariant();
            if (takenHeroes.Contains(hId))
                continue;

            double counterScore = 0.0;
            double synergyScore = 0.0;
            var reasons = new List<string>();
            var countersEnemies = new List<string>();
            var synergizesWith = new List<string>();

            // Check how this candidate counters enemy picks
            foreach (var ep in enemyPicks)
            {
                if (heroMap.TryGetValue(ep, out var enemyHero))
                {
                    // If enemyHero is countered by this candidate
                    var counterEntry = enemyHero.CounteredBy.FirstOrDefault(c => c.HeroId.Equals(hId, StringComparison.OrdinalIgnoreCase));
                    if (counterEntry != null)
                    {
                        counterScore += 18.0;
                        countersEnemies.Add(enemyHero.Name);
                        reasons.Add($"Counters {enemyHero.Name}: {counterEntry.Reason}");
                    }

                    // If this candidate is countered by enemyHero
                    var weaknessEntry = hero.CounteredBy.FirstOrDefault(c => c.HeroId.Equals(ep, StringComparison.OrdinalIgnoreCase));
                    if (weaknessEntry != null)
                    {
                        counterScore -= 14.0;
                    }
                }
            }

            // Check synergy with ally picks
            foreach (var ap in allyPicks)
            {
                if (synergies.TryGetValue(ap, out var syn) && syn.Pairs.Any(p => p.Equals(hId, StringComparison.OrdinalIgnoreCase)))
                {
                    synergyScore += syn.Bonus * 4.0;
                    if (heroMap.TryGetValue(ap, out var allyHero))
                    {
                        synergizesWith.Add(allyHero.Name);
                        reasons.Add($"Synergy combo with {allyHero.Name}: {syn.Reason}");
                    }
                }

                if (synergies.TryGetValue(hId, out var mySyn) && mySyn.Pairs.Any(p => p.Equals(ap, StringComparison.OrdinalIgnoreCase)))
                {
                    synergyScore += mySyn.Bonus * 4.0;
                    if (heroMap.TryGetValue(ap, out var allyHero) && !synergizesWith.Contains(allyHero.Name))
                    {
                        synergizesWith.Add(allyHero.Name);
                        reasons.Add($"Combo with {allyHero.Name}: {mySyn.Reason}");
                    }
                }
            }

            // Role priority bonus if role is missing in ally comp
            double roleBonus = 0.0;
            if (allyComp.MissingRoles.Any(r => r.Equals(hero.Role, StringComparison.OrdinalIgnoreCase)))
            {
                roleBonus += 15.0;
                reasons.Add($"Fills missing team role: {hero.Role}");
            }

            // Preferred role / lane match
            if (!string.IsNullOrWhiteSpace(request.PreferredRole) &&
                hero.Role.Equals(request.PreferredRole, StringComparison.OrdinalIgnoreCase))
            {
                roleBonus += 12.0;
            }

            // First pick priority if very early in draft
            if (allyPicks.Count <= 1 && FirstPicks.Contains(hId))
            {
                roleBonus += 8.0;
                reasons.Add("High-priority esports first-pick hero.");
            }

            double totalScore = counterScore + synergyScore + roleBonus + 50.0;

            candidates.Add(new HeroPickRecommendation
            {
                HeroId = hero.Id,
                Name = hero.Name,
                Role = hero.Role,
                SuggestedLane = SuggestLaneForRole(hero.Role),
                Score = Math.Round(totalScore, 1),
                CounterScore = Math.Round(counterScore, 1),
                SynergyScore = Math.Round(synergyScore, 1),
                Reasons = reasons,
                CountersEnemies = countersEnemies,
                SynergizesWith = synergizesWith,
                Avatar = hero.Avatar
            });
        }

        var topPicks = candidates.OrderByDescending(c => c.Score).Take(8).ToList();

        // 3. Situational Item Counters
        var itemSuggestions = new List<ItemCounterSuggestion>();
        foreach (var ep in enemyPicks)
        {
            if (heroMap.TryGetValue(ep, out var enemyHero))
            {
                foreach (var ic in enemyHero.ItemCounters)
                {
                    var item = _heroDataService.GetItemById(ic.ItemId);
                    if (item != null)
                    {
                        itemSuggestions.Add(new ItemCounterSuggestion
                        {
                            ItemId = item.Id,
                            ItemName = item.Name,
                            CountersHero = enemyHero.Name,
                            Reason = ic.Reason,
                            Icon = item.Icon ?? item.Avatar
                        });
                    }
                }
            }
        }

        // 4. Win Probability Calculation
        double allyPower = allyComp.DurabilityScore + allyComp.CrowdControlScore + allyComp.ScalingScore + allyComp.ObjectiveScore;
        double enemyPower = enemyComp.DurabilityScore + enemyComp.CrowdControlScore + enemyComp.ScalingScore + enemyComp.ObjectiveScore;
        double baseDiff = allyPower - enemyPower;
        double allyWinProb = Math.Clamp(50.0 + baseDiff * 0.25, 15.0, 85.0);
        double enemyWinProb = 100.0 - allyWinProb;

        string advice = GenerateAdvice(allyComp, enemyComp, allyPicks.Count, enemyPicks.Count);

        return new DraftRecommendationResult
        {
            AllyWinProbability = Math.Round(allyWinProb, 1),
            EnemyWinProbability = Math.Round(enemyWinProb, 1),
            OverallAdvice = advice,
            RecommendedPicks = topPicks,
            FirstPickPriorities = FirstPicks.ToList(),
            ItemCounterSuggestions = itemSuggestions.DistinctBy(i => i.ItemId + i.CountersHero).ToList(),
            AllyComposition = allyComp,
            EnemyComposition = enemyComp
        };
    }

    public IReadOnlyList<HeroPickRecommendation> GetCounterRecommendations(string enemyHeroId)
    {
        return AnalyzeDraft(new DraftRecommendationRequest
        {
            EnemyPicks = [enemyHeroId]
        }).RecommendedPicks;
    }

    public IReadOnlyList<HeroPickRecommendation> GetSynergyRecommendations(string allyHeroId)
    {
        return AnalyzeDraft(new DraftRecommendationRequest
        {
            AllyPicks = [allyHeroId]
        }).RecommendedPicks;
    }

    private static TeamCompositionAnalysis AnalyzeComposition(List<string> picks, Dictionary<string, Hero> heroMap)
    {
        var comp = new TeamCompositionAnalysis();
        if (picks.Count == 0)
        {
            comp.MissingRoles = ["Tank", "Mage", "Marksman", "Fighter", "Support"];
            return comp;
        }

        int physCount = 0;
        int magCount = 0;
        var existingRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in picks)
        {
            if (heroMap.TryGetValue(p, out var hero))
            {
                existingRoles.Add(hero.Role);
                if (string.Equals(hero.DamageType, "Magic", StringComparison.OrdinalIgnoreCase))
                    magCount++;
                else
                    physCount++;

                if (RoleWeights.TryGetValue(hero.Role, out var rw))
                {
                    comp.DurabilityScore += rw.Durability;
                    comp.CrowdControlScore += rw.Cc;
                    comp.ObjectiveScore += rw.Objective;
                    comp.ScalingScore += rw.Scaling;
                }
            }
        }

        int totalHeroes = Math.Max(1, physCount + magCount);
        comp.PhysicalDamagePercent = Math.Round((double)physCount / totalHeroes * 100.0, 1);
        comp.MagicDamagePercent = Math.Round((double)magCount / totalHeroes * 100.0, 1);

        string[] essentialRoles = ["Tank", "Mage", "Marksman", "Fighter"];
        comp.MissingRoles = essentialRoles.Where(r => !existingRoles.Contains(r)).ToList();

        if (comp.PhysicalDamagePercent > 80)
            comp.Vulnerabilities.Add("Heavily Physical biased — vulnerable to Blade Armor & Antique Cuirass stacking.");
        if (comp.MagicDamagePercent > 80)
            comp.Vulnerabilities.Add("Heavily Magic biased — vulnerable to Athena's Shield & Radiant Armor.");
        if (comp.CrowdControlScore < 30)
            comp.Vulnerabilities.Add("Low Crowd Control — struggles against agile assassins.");

        if (comp.DurabilityScore > 60)
            comp.Strengths.Add("High Frontline Durability.");
        if (comp.CrowdControlScore > 50)
            comp.Strengths.Add("Strong Crowd Control lock-down.");

        return comp;
    }

    private static string SuggestLaneForRole(string role)
    {
        return role.ToLowerInvariant() switch
        {
            "tank" => LaneRole.Roam,
            "support" => LaneRole.Roam,
            "mage" => LaneRole.Mid,
            "marksman" => LaneRole.Gold,
            "assassin" => LaneRole.Jungle,
            "fighter" => LaneRole.Exp,
            _ => LaneRole.Exp
        };
    }

    private static string GenerateAdvice(TeamCompositionAnalysis ally, TeamCompositionAnalysis enemy, int allyCount, int enemyCount)
    {
        if (allyCount == 0 && enemyCount == 0)
            return "Ban Phase Active: Target high-priority meta bans or counter-flex picks.";

        if (ally.MissingRoles.Count > 0)
            return $"Prioritize filling missing team roles: {string.Join(", ", ally.MissingRoles)}.";

        if (enemy.PhysicalDamagePercent > 70)
            return "Enemy team is physical heavy: Tank should build Antique Cuirass and Blade Armor early.";

        if (enemy.MagicDamagePercent > 70)
            return "Enemy team is magic heavy: Frontline should build Athena's Shield or Radiant Armor.";

        return "Team composition is well-balanced. Coordinate wombo-combos and secure Turtle/Lord objectives.";
    }
}

