using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

// DungeonGenerator ile EnemySpawner arasi adapter: spawn marker + patrol waypoint ekler, NavMesh bake eder, MapReadyEvent yayinlar.
[RequireComponent(typeof(DungeonGeneratorRunner))]
public class MapEnemyBridge : MonoBehaviour
{
    [Header("Spawn Point")]
    [SerializeField] private int _spawnPointsPerSmallRoom = 1;
    [SerializeField] private int _spawnPointsPerLargeRoom = 1;

    [Header("Patrol Waypoint")]
    [SerializeField] private bool _createPatrolGroups = true;
    [Range(0.3f, 0.9f)]
    [SerializeField] private float _waypointSpreadFactor = 0.6f;

    [Header("NavMesh")]
    [SerializeField] private bool _bakeNavMeshAtRuntime = true;
    [SerializeField] private int _bakeDelayFrames = 1;
    [SerializeField] private bool _usePhysicsCollidersForRuntimeBake = true;

    [Header("Map Ready Event")]
    [SerializeField] private bool _publishMapReadyEvent = true;

    [Header("Debug")]
    [SerializeField] private bool _verbose = true;

    private DungeonGeneratorRunner _runner;
    private NavMeshSurface _navMeshSurface;

    private void Awake()
    {
        _runner = GetComponent<DungeonGeneratorRunner>();
    }

    private void Start()
    {
        StartCoroutine(WaitAndSetup());
    }

    private IEnumerator WaitAndSetup()
    {
        // Runner kendi Start'inda Generate cagiriyor.
        // Visualizer "DungeonLayout" objesini bu transform'un altina koyar.
        // Bir frame bekle ki Instantiate'lar tamamlansin.
        for (int i = 0; i < _bakeDelayFrames; i++)
            yield return null;

        Transform dungeonRoot = transform.Find("DungeonLayout");
        if (dungeonRoot == null)
        {
            Debug.LogError("[MapEnemyBridge] DungeonLayout bulunamadi. Visualizer cagrildi mi?");
            yield break;
        }

        int roomCount = SetupRooms(dungeonRoot, out var roomBoundsList);

        if (_bakeNavMeshAtRuntime)
        {
            BakeNavMesh();
            // NavMesh hazir olana kadar bir frame daha
            yield return null;
        }

        if (_publishMapReadyEvent)
        {
            int seed = ExtractSeedSafe();
            GameEventBus.Publish(new MapReadyEvent(seed, roomCount, roomBoundsList.ToArray()));
            if (_verbose)
                Debug.Log($"[MapEnemyBridge] MapReadyEvent yayinlandi (seed={seed}, rooms={roomCount}, bounds={roomBoundsList.Count}).");
        }
    }

    private int SetupRooms(Transform dungeonRoot, out List<Bounds> roomBoundsList)
    {
        int processed = 0;
        roomBoundsList = new List<Bounds>();

        // Snapshot al, child collection runtime'da degisiyor
        // (waypoint ekledigimiz icin enumeration sirasinda mutate olur)
        var rooms = new List<Transform>(dungeonRoot.childCount);
        foreach (Transform child in dungeonRoot)
            rooms.Add(child);

        foreach (var room in rooms)
        {
            if (!room.name.StartsWith("Room")) continue;

            Bounds bounds = EstimateRoomBounds(room);
            bool isLargeRoom = room.name.Contains("2x2") || room.name.Contains("1x3") || room.name.Contains("3x1");

            int spawnCount = isLargeRoom ? _spawnPointsPerLargeRoom : _spawnPointsPerSmallRoom;
            AddSpawnPoints(room, bounds, spawnCount);

            if (_createPatrolGroups)
                AddPatrolGroup(room, bounds);

            roomBoundsList.Add(bounds);
            processed++;
        }

        if (_verbose)
            Debug.Log($"[MapEnemyBridge] {processed} oda marker'li hazirlandi.");

        return processed;
    }

    private void AddSpawnPoints(Transform room, Bounds bounds, int count)
    {
        if (count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject($"EnemySpawnPoint_{i}");
            go.transform.SetParent(room, worldPositionStays: false);

            // Tek spawn ise oda merkezi; birden fazla ise bounds icinde rastgele
            Vector3 worldPos = count == 1
                ? bounds.center
                : new Vector3(
                    Random.Range(bounds.min.x + 1f, bounds.max.x - 1f),
                    bounds.center.y,
                    Random.Range(bounds.min.z + 1f, bounds.max.z - 1f));

            go.transform.position = worldPos;
            go.AddComponent<EnemySpawnPoint>();
        }
    }

    private void AddPatrolGroup(Transform room, Bounds bounds)
    {
        var groupGo = new GameObject("PatrolGroup");
        groupGo.transform.SetParent(room, worldPositionStays: false);
        groupGo.transform.position = bounds.center;
        var group = groupGo.AddComponent<PatrolWaypointGroup>();

        // Oda kose yakinlarinda 4 waypoint
        Vector3 half = bounds.extents * _waypointSpreadFactor;
        Vector3[] offsets =
        {
            new Vector3(-half.x, 0f, -half.z),
            new Vector3( half.x, 0f, -half.z),
            new Vector3( half.x, 0f,  half.z),
            new Vector3(-half.x, 0f,  half.z)
        };

        var waypoints = new Transform[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var wp = new GameObject($"WP_{i}");
            wp.transform.SetParent(groupGo.transform, worldPositionStays: false);
            wp.transform.position = bounds.center + offsets[i];
            waypoints[i] = wp.transform;
        }

        group.SetWaypoints(waypoints);
    }

    // Sadece "Floor" isimli renderer'lardan bounds hesapla, yoksa fallback olarak hepsini al
    private static Bounds EstimateRoomBounds(Transform room)
    {
        var renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(room.position, new Vector3(6f, 1f, 6f));

        bool foundFloor = false;
        Bounds floorBounds = default;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            string n = r.gameObject.name;
            if (n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

            if (!foundFloor)
            {
                floorBounds = r.bounds;
                foundFloor = true;
            }
            else
            {
                floorBounds.Encapsulate(r.bounds);
            }
        }

        if (foundFloor) return floorBounds;

        // Fallback (test sahneleri, custom prefab vs.)
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void BakeNavMesh()
    {
        _navMeshSurface = FindFirstObjectByType<NavMeshSurface>();

        if (_navMeshSurface == null)
        {
            _navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            _navMeshSurface.collectObjects = CollectObjects.All;
        }

        _navMeshSurface.collectObjects = CollectObjects.All;
        _navMeshSurface.useGeometry = _usePhysicsCollidersForRuntimeBake
            ? UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders
            : UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;

        _navMeshSurface.BuildNavMesh();

        if (_verbose)
            Debug.Log("[MapEnemyBridge] Runtime NavMesh bake tamamlandi.");
    }

    // DungeonGeneratorRunner seed'i expose etmiyor, MapReadyEvent icin -1 yeterli
    private int ExtractSeedSafe()
    {
        return -1;
    }
}
