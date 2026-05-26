using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// RoomPropSpawner — subscribes to MapReadyEvent and spawns
/// decorative factory props inside each room's bounds.
/// 
/// Attach to any persistent GameObject in the scene.
/// Assign factory prop prefabs via the Inspector.
/// 
/// Assets/Scripts/Map/RoomPropSpawner.cs
/// </summary>
public class RoomPropSpawner : MonoBehaviour
{
    [Header("Prop Prefabs")]
    [Tooltip("Small props to randomly place inside rooms (barrels, boxes, fire extinguishers, etc.)")]
    [SerializeField] private GameObject[] _propPrefabs;

    [Header("Spawn Settings")]
    [Tooltip("Minimum number of props per room")]
    [SerializeField] private int _minPropsPerRoom = 2;

    [Tooltip("Maximum number of props per room")]
    [SerializeField] private int _maxPropsPerRoom = 4;

    [Tooltip("How far from room edges props can spawn (wall margin)")]
    [SerializeField] private float _wallMargin = 1.2f;

    [Tooltip("Minimum distance between two props")]
    [SerializeField] private float _minPropSpacing = 0.8f;

    [Tooltip("Layer mask for overlap checks (avoid spawning inside walls)")]
    [SerializeField] private LayerMask _overlapMask = ~0;

    // Tracks all spawned props so they can be cleared on regeneration
    private readonly List<GameObject> _spawnedProps = new();

    private void OnEnable()
    {
        GameEventBus.Subscribe<MapReadyEvent>(OnMapReady);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<MapReadyEvent>(OnMapReady);
    }

    //MapReadyEvent Handler
    private void OnMapReady(MapReadyEvent evt)
    {
        ClearProps();

        if (_propPrefabs == null || _propPrefabs.Length == 0)
        {
            Debug.LogWarning("[RoomPropSpawner] No prop prefabs assigned!");
            return;
        }

        if (evt.RoomBounds == null || evt.RoomBounds.Length == 0)
        {
            Debug.LogWarning("[RoomPropSpawner] MapReadyEvent has no RoomBounds — skipping prop spawn.");
            return;
        }

        System.Random rng = new System.Random(evt.Seed);

        foreach (Bounds roomBounds in evt.RoomBounds)
        {
            SpawnPropsInRoom(roomBounds, rng);
        }

        Debug.Log($"[RoomPropSpawner] Spawned {_spawnedProps.Count} props across {evt.RoomBounds.Length} rooms.");
        if (evt.RoomBounds != null && evt.RoomBounds.Length > 0)
            Debug.Log($"[RoomPropSpawner] First room bounds center: {evt.RoomBounds[0].center}, min: {evt.RoomBounds[0].min}");

    }

    //Spawn props inside a single room
    private void SpawnPropsInRoom(Bounds roomBounds, System.Random rng)
    {
        int count = rng.Next(_minPropsPerRoom, _maxPropsPerRoom + 1);

        // Sadece merkezi kullan, sabit 2 birimlik alan
        float spawnRadius = 2f;
        Vector3 center = roomBounds.center;

        float minX = center.x - spawnRadius;
        float maxX = center.x + spawnRadius;
        float minZ = center.z - spawnRadius;
        float maxZ = center.z + spawnRadius;

        // If room is too small after margin, skip
        if (minX >= maxX || minZ >= maxZ) return;

        List<Vector3> placedPositions = new List<Vector3>();

        int attempts = 0;
        int placed = 0;

        while (placed < count && attempts < count * 10)
        {
            attempts++;

            float x = Lerp(minX, maxX, (float)rng.NextDouble());
            float z = Lerp(minZ, maxZ, (float)rng.NextDouble());
            Vector3 candidate = new Vector3(x, roomBounds.min.y + 0.1f, z);

            // Check spacing from other placed props
            if (!IsFarEnough(candidate, placedPositions)) continue;

            // Check for wall overlap
            //if (Physics.CheckSphere(candidate, 0.4f, _overlapMask)) continue;

            // Pick a random prefab
            int prefabIndex = rng.Next(_propPrefabs.Length);
            GameObject prefab = _propPrefabs[prefabIndex];
            if (prefab == null) continue;

            // Random Y rotation
            float yRot = (float)(rng.NextDouble() * 360.0);
            Quaternion rotation = Quaternion.Euler(0f, yRot, 0f);

            GameObject prop = Instantiate(prefab, candidate, rotation, transform);
            prop.transform.localScale = Vector3.one * 2f; // Scale up by 2x
            _spawnedProps.Add(prop);
            placedPositions.Add(candidate);
            placed++;
        }
    }

    //Clear all previously spawned props
    private void ClearProps()
    {
        foreach (var prop in _spawnedProps)
        {
            if (prop != null)
                Destroy(prop);
        }
        _spawnedProps.Clear();
    }

    //Helpers
    private bool IsFarEnough(Vector3 candidate, List<Vector3> placed)
    {
        float minSqr = _minPropSpacing * _minPropSpacing;
        foreach (var p in placed)
        {
            if ((candidate - p).sqrMagnitude < minSqr)
                return false;
        }
        return true;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}