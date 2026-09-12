using MLBB.Core.Entities;

namespace MLBB.Core.Interfaces;

/// <summary>
/// Data access abstraction for MLBB Hero and Item definitions.
/// </summary>
public interface IHeroRepository
{
    IReadOnlyList<Hero> GetAllHeroes();
    Hero? GetHeroById(string heroId);
    IReadOnlyList<Item> GetAllItems();
    Item? GetItemById(string itemId);
    IReadOnlyDictionary<string, SynergyData> GetSynergies();
    void Reload();
}

