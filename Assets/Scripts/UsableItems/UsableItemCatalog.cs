using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-independent lookup from a usable item's id to its networked world prefab.
/// Referenced as a serialized asset by PlayerInventory, so it resolves in every scene
/// (lobby AND gameplay) — unlike the scene-bound ItemDatabase. Each prefab listed here
/// is expected to carry a NetworkUsableItem component.
/// </summary>
[CreateAssetMenu(fileName = "UsableItemCatalog", menuName = "Ludus/Usable Item Catalog")]
public class UsableItemCatalog : ScriptableObject
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("Inventory item id (matches the ushort stored in PlayerInventory.Slots).")]
        public ushort itemId;

        [Tooltip("World prefab to spawn. Must have NetworkObject + a NetworkUsableItem component.")]
        public GameObject worldPrefab;
    }

    [SerializeField] private List<Entry> entries = new();

    /// <summary>Returns the world prefab for the given usable item id, or null if not registered.</summary>
    public GameObject GetPrefab(ushort itemId)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].itemId == itemId)
                return entries[i].worldPrefab;
        }
        return null;
    }

    /// <summary>True if the id is a registered usable item.</summary>
    public bool Contains(ushort itemId) => GetPrefab(itemId) != null;
}
