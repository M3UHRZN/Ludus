// Assets/Scripts/Items/ItemSpawner.cs
using UnityEngine;

/// <summary>
/// Spawns and despawns items using ObjectPool to avoid GC pressure.
/// Attach to a manager GameObject in the scene.
/// </summary>
public class ItemSpawner : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private BaseItem _itemPrefab;
    [SerializeField] private int _initialPoolSize = 20;

    private ObjectPool<BaseItem> _pool;

    private void Awake()
    {
        _pool = new ObjectPool<BaseItem>(_itemPrefab, _initialPoolSize, this.transform);
    }

    /// <summary>Spawns an item at the given position and rotation.</summary>
    
    public BaseItem Spawn(Vector3 position, Quaternion rotation)
    {
        BaseItem item = _pool.Get();
        item.transform.SetPositionAndRotation(position, rotation);
        Debug.Log($"[ItemSpawner] Spawned: {item.ItemName} at {position}");
        return item;
    }

    /// <summary>Returns an item back to the pool.</summary>
    public void Despawn(BaseItem item)
    {
        Debug.Log($"[ItemSpawner] Despawned: {item.ItemName}");
        _pool.Return(item);
    }
}