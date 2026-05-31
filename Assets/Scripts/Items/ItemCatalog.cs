using System.Collections.Generic;
using UnityEngine;
using Ludus.UsableItems.Core;

/// <summary>
/// Tüm oyunun tek id→item lookup'ı. ItemDatabase + ItemRegistry + UsableItemCatalog
/// yerine geçer. Resources'tan tek sefer yüklenir → her sahnede (lobby VE gameplay)
/// prefab başına serialized referans olmadan çözülür.
/// </summary>
[CreateAssetMenu(fileName = "ItemCatalog", menuName = "Ludus/Item Catalog")]
public class ItemCatalog : ScriptableObject
{
    [SerializeField] private List<ItemDefinition> definitions = new();

    public IReadOnlyList<ItemDefinition> AllDefinitions => definitions;

    private static ItemCatalog _instance;
    public static ItemCatalog Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<ItemCatalog>("ItemCatalog");
            if (_instance == null)
                Debug.LogError("[ItemCatalog] Resources/ItemCatalog.asset bulunamadi.");
            return _instance;
        }
    }

    public ItemDefinition GetById(ushort id)
    {
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null && definitions[i].Id == id) return definitions[i];
        return null;
    }

    public GameObject GetPrefab(ushort id) { var d = GetById(id); return d != null ? d.WorldPrefab : null; }
    public Sprite GetIcon(ushort id) { var d = GetById(id); return d != null ? d.Icon : null; }
    public int GetWeight(ushort id) => ItemCatalogLookup.GetWeight(definitions, id);
    public bool Contains(ushort id) => GetById(id) != null;

    public void CollectBuyable(List<ItemDefinition> result)
    {
        if (result == null) return;
        for (int i = 0; i < definitions.Count; i++)
            if (definitions[i] != null && definitions[i].IsBuyable) result.Add(definitions[i]);
    }
}
