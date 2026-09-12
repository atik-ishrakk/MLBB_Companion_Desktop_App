using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MLBB.Core.Entities;
using MLBB.Core.Interfaces;
using MLBB.Core.Services;

namespace MLBB.Infrastructure.Repositories;

/// <summary>
/// Repository loading and caching MLBB Heroes, Items, and Wombo-Combo synergies.
/// </summary>
public class HeroRepository : IHeroRepository
{
    private readonly ILogger<HeroRepository> _logger;
    private readonly object _lock = new();
    private List<Hero> _heroes = [];
    private List<Item> _items = [];
    private Dictionary<string, SynergyData> _synergies = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public HeroRepository(ILogger<HeroRepository> logger)
    {
        _logger = logger;
        EnsureLoaded();
    }

    public IReadOnlyList<Hero> GetAllHeroes()
    {
        EnsureLoaded();
        return _heroes;
    }

    public Hero? GetHeroById(string heroId)
    {
        EnsureLoaded();
        return _heroes.FirstOrDefault(h => string.Equals(h.Id, heroId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<Item> GetAllItems()
    {
        EnsureLoaded();
        return _items;
    }

    public Item? GetItemById(string itemId)
    {
        EnsureLoaded();
        return _items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyDictionary<string, SynergyData> GetSynergies()
    {
        EnsureLoaded();
        return _synergies;
    }

    public void Reload()
    {
        lock (_lock)
        {
            _initialized = false;
            EnsureLoaded();
        }
    }

    private void EnsureLoaded()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                var candidates = new[]
                {
                    WorkspacePathResolver.ResolvePath("Assets", "data", "game_data.json"),
                    WorkspacePathResolver.ResolvePath("Solution", "API", "Assets", "data", "game_data.json"),
                    Path.Combine(AppContext.BaseDirectory, "Assets", "data", "game_data.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Assets", "data", "game_data.json"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Solution", "API", "Assets", "data", "game_data.json")
                };

                string? foundPath = candidates.FirstOrDefault(File.Exists);
                if (foundPath != null)
                {
                    _logger.LogInformation("Loading hero data from: {Path}", foundPath);
                    string json = File.ReadAllText(foundPath);
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("heroes", out var heroesElem))
                    {
                        _heroes = JsonSerializer.Deserialize<List<Hero>>(heroesElem.GetRawText()) ?? [];
                    }

                    if (doc.RootElement.TryGetProperty("items", out var itemsElem))
                    {
                        _items = JsonSerializer.Deserialize<List<Item>>(itemsElem.GetRawText()) ?? [];
                    }
                }
                else
                {
                    _logger.LogWarning("game_data.json not found in search paths. Using fallback database.");
                }

                InitSynergies();
                _initialized = true;
                _logger.LogInformation("Hero repository loaded {HeroesCount} heroes, {ItemsCount} items, {SynergiesCount} synergies.",
                    _heroes.Count, _items.Count, _synergies.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading game data.");
            }
        }
    }

    private void InitSynergies()
    {
        _synergies = new Dictionary<string, SynergyData>(StringComparer.OrdinalIgnoreCase)
        {
            ["odette"] = new()
            {
                HeroId = "odette",
                Pairs = ["johnson", "tigreal", "atlas", "carmilla"],
                Bonus = 4,
                Reason = "Devastating AoE Crowd Control + Swan Song Ultimate wombo-combo."
            },
            ["johnson"] = new()
            {
                HeroId = "johnson",
                Pairs = ["odette", "kadita", "vale", "cecilion"],
                Bonus = 4,
                Reason = "Rapid Inbound crash dive delivers instant high-burst follow-up."
            },
            ["guinevere"] = new()
            {
                HeroId = "guinevere",
                Pairs = ["tigreal", "atlas", "minotaur", "gatotkaca"],
                Bonus = 3,
                Reason = "Airborne chaining prevents target from using Purify once airborne."
            },
            ["kadita"] = new()
            {
                HeroId = "kadita",
                Pairs = ["johnson", "petrify", "atlas", "tigreal"],
                Bonus = 4,
                Reason = "Ocean Oddity dive into Rough Waves creates lethal one-shot burst."
            },
            ["angela"] = new()
            {
                HeroId = "angela",
                Pairs = ["ling", "fanny", "leomord", "alucard", "roger"],
                Bonus = 3,
                Reason = "Heartguard shield + movement speed turbo-charges dive assassins."
            },
            ["atlas"] = new()
            {
                HeroId = "atlas",
                Pairs = ["claude", "odette", "guinevere", "pharsa", "luoyi"],
                Bonus = 4,
                Reason = "Fatal Links drag groups entire enemy team for massive AoE wipe."
            },
            ["tigreal"] = new()
            {
                HeroId = "tigreal",
                Pairs = ["odette", "guinevere", "kadita", "beatrix", "gord"],
                Bonus = 4,
                Reason = "Implosion vacuum locks entire squad for unstoppable teamfight combo."
            },
            ["carmilla"] = new()
            {
                HeroId = "carmilla",
                Pairs = ["cecilion", "vexana", "vale", "luoyi"],
                Bonus = 4,
                Reason = "Curse of Blood shares 100% damage and CC across chained targets."
            },
            ["claude"] = new()
            {
                HeroId = "claude",
                Pairs = ["atlas", "tigreal", "belerick", "lolita"],
                Bonus = 3,
                Reason = "Blazing Duet melts grouped teams during hard CC window."
            }
        };
    }
}

