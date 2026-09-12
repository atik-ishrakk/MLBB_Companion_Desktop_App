using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Business logic for querying heroes, item builds, synergies, and counter relationships.
/// </summary>
public interface IHeroDataService
{
    IReadOnlyList<Hero> GetAllHeroes();
    Hero? GetHeroById(string id);
    IReadOnlyList<Hero> GetHeroesByRole(string role);
    IReadOnlyList<Hero> SearchHeroes(string query);
    IReadOnlyList<Item> GetAllItems();
    Item? GetItemById(string id);
    IReadOnlyList<Item> GetItemsByType(string type);
    IReadOnlyDictionary<string, SynergyData> GetAllSynergies();
    void ReloadDatabase();
}

