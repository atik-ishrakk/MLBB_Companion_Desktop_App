using MLBB.Core.Entities;
using MLBB.Core.Interfaces;

namespace MLBB.Core.Services;

/// <summary>
/// Business logic service for accessing and filtering MLBB heroes, items, and synergies.
/// </summary>
public class HeroDataService : IHeroDataService
{
    private readonly IHeroRepository _repository;

    public HeroDataService(IHeroRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<Hero> GetAllHeroes() => _repository.GetAllHeroes();

    public Hero? GetHeroById(string id) => _repository.GetHeroById(id);

    public IReadOnlyList<Hero> GetHeroesByRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return GetAllHeroes();

        return _repository.GetAllHeroes()
            .Where(h => string.Equals(h.Role, role, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyList<Hero> SearchHeroes(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return GetAllHeroes();

        var q = query.Trim().ToLowerInvariant();
        return _repository.GetAllHeroes()
            .Where(h => h.Name.ToLowerInvariant().Contains(q) ||
                        h.Id.ToLowerInvariant().Contains(q) ||
                        h.Role.ToLowerInvariant().Contains(q) ||
                        h.Specialty.Any(s => s.ToLowerInvariant().Contains(q)))
            .ToList();
    }

    public IReadOnlyList<Item> GetAllItems() => _repository.GetAllItems();

    public Item? GetItemById(string id) => _repository.GetItemById(id);

    public IReadOnlyList<Item> GetItemsByType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return GetAllItems();

        return _repository.GetAllItems()
            .Where(i => string.Equals(i.Type, type, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public IReadOnlyDictionary<string, SynergyData> GetAllSynergies() => _repository.GetSynergies();

    public void ReloadDatabase() => _repository.Reload();
}

