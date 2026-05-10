using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Server-only enemy spawner. MapReadyEvent yayinlandiginda sahnedeki
/// EnemySpawnPoint marker'larini tarar, her biri icin server'da Enemy
/// NetworkObject spawn eder ve yakindaki PatrolWaypointGroup'u atar.
///
/// Anil ile kontrat: harita uretildikten sonra GameEventBus.Publish(new MapReadyEvent(...))
/// cagirilir, bu spawner devreye girer.
/// </summary>
public class EnemySpawner : NetworkBehaviour
{
    [Header("Enemy")]
    [Tooltip("NetworkObject component'i olan Enemy prefab'i (Assets/Prefabs/Enemy.prefab)")]
    [SerializeField] private GameObject _enemyPrefab;

    [Header("Davranis")]
    [Tooltip("Spawn'da yakin patrol grubu aramak icin maks. mesafe")]
    [SerializeField] private float _patrolGroupSearchRadius = 20f;

    [Tooltip("MapReadyEvent gelmese bile sahnede EnemySpawnPoint varsa direkt spawn et (test icin)")]
    [SerializeField] private bool _spawnOnStartIfNoMapEvent = false;

    [Tooltip("Test sahnesi icin MapReadyEvent yayinlanmadiysa Start'tan sonra ne kadar bekle")]
    [SerializeField] private float _fallbackSpawnDelay = 2f;

    private bool _hasSpawned;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GameEventBus.Subscribe<MapReadyEvent>(OnMapReady);

        if (_spawnOnStartIfNoMapEvent)
            Invoke(nameof(FallbackSpawn), _fallbackSpawnDelay);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        GameEventBus.Unsubscribe<MapReadyEvent>(OnMapReady);
    }

    private void OnMapReady(MapReadyEvent evt)
    {
        if (_hasSpawned) return;
        Debug.Log($"[EnemySpawner] MapReadyEvent alindi (seed={evt.Seed}, roomCount={evt.RoomCount}). Spawn baslıyor.");
        SpawnAllEnemies();
    }

    private void FallbackSpawn()
    {
        if (_hasSpawned) return;
        Debug.Log("[EnemySpawner] MapReadyEvent gelmedi, fallback spawn devrede.");
        SpawnAllEnemies();
    }

    private void SpawnAllEnemies()
    {
        if (_enemyPrefab == null)
        {
            Debug.LogError("[EnemySpawner] Enemy prefab atanmamis.");
            return;
        }

        var spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        var patrolGroups = FindObjectsByType<PatrolWaypointGroup>(FindObjectsSortMode.None);

        Debug.Log($"[EnemySpawner] {spawnPoints.Length} spawn point, {patrolGroups.Length} patrol grubu bulundu.");

        int totalSpawned = 0;
        foreach (var sp in spawnPoints)
        {
            for (int i = 0; i < sp.EnemyCount; i++)
            {
                if (TrySpawnAt(sp, patrolGroups))
                    totalSpawned++;
            }
        }

        _hasSpawned = true;
        Debug.Log($"[EnemySpawner] Toplam {totalSpawned} enemy spawn edildi.");
    }

    private bool TrySpawnAt(EnemySpawnPoint sp, PatrolWaypointGroup[] patrolGroups)
    {
        Vector3 pos = sp.transform.position;
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var enemyGo = Instantiate(_enemyPrefab, pos, rot);
        var netObj = enemyGo.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[EnemySpawner] Enemy prefab'inda NetworkObject yok: {_enemyPrefab.name}");
            Destroy(enemyGo);
            return false;
        }

        netObj.Spawn();

        // Patrol noktalarini ata
        var ctrl = enemyGo.GetComponent<EnemyController>();
        if (ctrl != null)
        {
            var waypoints = ResolveWaypointsFor(sp, patrolGroups);
            if (waypoints != null && waypoints.Length > 0)
                ctrl.SetWaypoints(waypoints);
        }

        return true;
    }

    /// <summary>
    /// Onceligi: en yakin PatrolWaypointGroup. Yoksa SpawnPoint kendi
    /// GenerateWaypointPositions ile uretilen runtime waypoint'leri olarak doner.
    /// </summary>
    private Transform[] ResolveWaypointsFor(EnemySpawnPoint sp, PatrolWaypointGroup[] groups)
    {
        PatrolWaypointGroup nearest = null;
        float minSqr = _patrolGroupSearchRadius * _patrolGroupSearchRadius;

        foreach (var g in groups)
        {
            float d = (g.transform.position - sp.transform.position).sqrMagnitude;
            if (d < minSqr)
            {
                minSqr = d;
                nearest = g;
            }
        }

        if (nearest != null && nearest.Count > 0)
            return nearest.Waypoints;

        // Fallback: SpawnPoint'in kendi cember waypoint'leri (runtime Transform list)
        return BuildRuntimeWaypoints(sp);
    }

    /// <summary>
    /// SpawnPoint cevresindeki Vector3 noktalari runtime'da Transform listesine
    /// cevir. Sahnede gercek GameObject yaratir, parent SpawnPoint olur.
    /// </summary>
    private Transform[] BuildRuntimeWaypoints(EnemySpawnPoint sp)
    {
        var positions = sp.GenerateWaypointPositions();
        if (positions == null || positions.Length == 0) return null;

        var list = new List<Transform>(positions.Length);
        for (int i = 0; i < positions.Length; i++)
        {
            var go = new GameObject($"WP_runtime_{i}");
            go.transform.SetParent(sp.transform, worldPositionStays: false);
            go.transform.position = positions[i];
            list.Add(go.transform);
        }
        return list.ToArray();
    }
}
