// Assets/Scripts/Items/ItemRegistry.cs
using System.Collections.Generic;
using UnityEngine;

public class ItemRegistry : MonoBehaviour
{
    /// <summary>
    /// Maps ushort item IDs to BaseItem prefabs.
    /// Assign prefabs in the Inspector.
    /// </summary>
    
    public static ItemRegistry Instance {get; private set;}

    [SerializeField] private List<BaseItem> _itemPrefabs = new();

    private Dictionary<ushort, BaseItem> _registry = new();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        for (ushort i = 0; i < _itemPrefabs.Count; i++)
            _registry[i] = _itemPrefabs[i];
    }

    /// <summary>Returns the BaseItem prefab for the given ID, or null.</summary>
    public BaseItem GetPrefab(ushort id) =>
        _registry.TryGetValue(id, out var prefab) ? prefab : null;


    /// <summary>Returns the weight for the given item ID.</summary>
    public int GetWeight(ushort id) =>
        GetPrefab(id) != null ? GetPrefab(id).Weight : 0;
}