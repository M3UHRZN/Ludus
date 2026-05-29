namespace Ludus.UsableItems.Core
{
    /// <summary>
    /// Minimal, Unity-agnostik item görünümü. ItemDefinition (main assembly) bunu
    /// uygular; böylece ItemCatalogLookup ScriptableObject olmadan test edilebilir.
    /// </summary>
    public interface IItemRecord
    {
        ushort Id { get; }
        int Weight { get; }
        bool IsBuyable { get; }
    }
}
