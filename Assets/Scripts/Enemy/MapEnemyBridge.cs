using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Anil'in DungeonGeneratorRunner'i ile bizim EnemySpawner pipeline'ini
/// birbirine baglayan adaptor.
///
/// DungeonGeneratorRunner.Start icinde dungeon spawn olur. Bir frame
/// bekledikten sonra:
///   1) "DungeonLayout" child'i altindaki her oda parent'ina
///      EnemySpawnPoint marker'i ekler (oda merkezine).
///   2) Her odaya PatrolWaypointGroup ekler ve oda kose yakinlarinda
///      waypoint child'lari uretir (oda boyutuna gore).
///   3) Runtime'da NavMesh bake eder (NavMeshSurface).
///   4) GameEventBus.Publish(new MapReadyEvent(...)) yayinlar — bu sayede
///      EnemySpawner kendi OnMapReady akisini calistirir.
///
/// Aynı GameObject'te DungeonGeneratorRunner ve DungeonVisualizer bulunmali.
/// </summary>
[RequireComponent(typeof(DungeonGeneratorRunner))]
public class MapEnemyBridge : MonoBehaviour
{
    [Header("Spawn Point")]
    [Tooltip("Kucuk odalara (1x1) konulacak EnemySpawnPoint sayisi")]
    [SerializeField] private int _spawnPointsPerSmallRoom = 1;

    [Tooltip("Buyuk odalara (1x3 / 3x1 / 2x2) konulacak EnemySpawnPoint sayisi")]
    [SerializeField] private int _spawnPointsPerLargeRoom = 1;

    [Header("Patrol Waypoint")]
    [Tooltip("Her odaya PatrolWaypointGroup ekle")]
    [SerializeField] private bool _createPatrolGroups = true;

    [Tooltip("Oda yarim-boyutunun yuzde kaci waypoint'leri kaplasin")]
    [Range(0.3f, 0.9f)]
    [SerializeField] private float _waypointSpreadFactor = 0.6f;

    [Header("NavMesh")]
    [Tooltip("Sahne yuklendiginde NavMesh'i runtime bake et")]
    [SerializeField] private bool _bakeNavMeshAtRuntime = true;

    [Tooltip("NavMesh bake'i kac frame sonra calistir (dungeon spawn'a vakit tani)")]
    [SerializeField] private int _bakeDelayFrames = 1;

    [Header("Map Ready Event")]
    [Tooltip("DungeonLayout hazirlaninca MapReadyEvent yayinla")]
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

        int roomCount = SetupRooms(dungeonRoot);

        if (_bakeNavMeshAtRuntime)
        {
            BakeNavMesh();
            // NavMesh hazir olana kadar bir frame daha
            yield return null;
        }

        if (_publishMapReadyEvent)
        {
            int seed = ExtractSeedSafe();
            GameEventBus.Publish(new MapReadyEvent(seed, roomCount));
            if (_verbose)
                Debug.Log($"[MapEnemyBridge] MapReadyEvent yayinlandi (seed={seed}, rooms={roomCount}).");
        }
    }

    // ------------------------------------------------------------------
    // Oda iyileme: marker ekle
    // ------------------------------------------------------------------

    private int SetupRooms(Transform dungeonRoot)
    {
        int processed = 0;

        // Snapshot al — child collection runtime'da degisiyor (waypoint ekledigimiz icin)
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

    // ------------------------------------------------------------------
    // Yardimcilar
    // ------------------------------------------------------------------

    /// <summary>
    /// Oda parent'i altindaki tum Renderer'larin birlesik bounds'ini doner.
    /// Floor / Wall / Door mesh'lerini iceren toplam alan.
    /// </summary>
    private static Bounds EstimateRoomBounds(Transform room)
    {
        var renderers = room.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return new Bounds(room.position, new Vector3(6f, 1f, 6f));

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
            _navMeshSurface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.RenderMeshes;
        }

        _navMeshSurface.BuildNavMesh();

        if (_verbose)
            Debug.Log("[MapEnemyBridge] Runtime NavMesh bake tamamlandi.");
    }

    /// <summary>
    /// DungeonGeneratorRunner seed bilgisini dogrudan expose etmiyor; biz
    /// MapReadyEvent'in seed parametresi icin sadece bilgilendirme amacli
    /// degerini -1 verirsek de spawner pipeline'ini bozmaz. Ileride Anil
    /// Runner'a LastSeed property eklerse buraya bagliyabiliriz.
    /// </summary>
    private int ExtractSeedSafe()
    {
        return -1;
    }
}
