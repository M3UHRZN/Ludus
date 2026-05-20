using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Server-only enemy spawner. MapReadyEvent yayinlandiginda sahnedeki
/// EnemySpawnPoint marker'larini havuz olarak alir, ardisik dalgalar halinde
/// (wave) enemy uretir. Toplam alive enemy sayisi budget'la sinirlidir.
///
/// Mimari kararlar:
///   - Budget: targetCount = Min(maxAlive, roomCount / roomsPerEnemy)
///   - Wave: ilk spawn delay sonra, sabit interval ile yeni enemy gelir
///   - EnemyDiedEvent Observer: enemy oldukce alive count azalir, yeni slot acilir
///
/// Anil ile kontrat: harita uretildikten sonra GameEventBus.Publish(new MapReadyEvent(...))
/// cagirilir, bu spawner devreye girer.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class EnemySpawner : NetworkBehaviour
{
    [Header("Enemy Prefablari")]
    [Tooltip("Spawn icin kullanilacak enemy prefablari (her biri NetworkObject olmali). Bos ise spawn olmaz.")]
    [SerializeField] private GameObject[] _enemyPrefabs;

    [Header("Spawn Budget")]
    [Tooltip("Ayni anda sahnede en fazla kac enemy olabilir")]
    [SerializeField] private int _maxAliveEnemies = 5;

    [Tooltip("Budget formulu: target = roomCount / roomsPerEnemy (sonra maxAlive ile sinirlanir)")]
    [SerializeField] private int _roomsPerEnemy = 10;

    [Tooltip("MapReadyEvent gelmezse fallback hedef enemy sayisi")]
    [SerializeField] private int _fallbackTargetCount = 3;

    [Header("Wave Spawn")]
    [Tooltip("MapReadyEvent'ten sonra ilk enemy ne kadar sure sonra cikar")]
    [SerializeField] private float _firstSpawnDelay = 5f;

    [Tooltip("Iki ardisik spawn arasi sure (saniye)")]
    [SerializeField] private float _spawnInterval = 15f;

    [Header("Patrol Bagi")]
    [Tooltip("Spawn'da yakin patrol grubu aramak icin maks. mesafe")]
    [SerializeField] private float _patrolGroupSearchRadius = 20f;

    [Header("Player Mesafe Kontrolu")]
    [Tooltip("Player'a bu mesafeden daha yakin spawn olmaz (jump scare riskini onler)")]
    [SerializeField] private float _minDistanceFromPlayer = 20f;

    [Tooltip("Player'a bu mesafeden daha uzak spawn olmaz. 0 = limit yok.")]
    [SerializeField] private float _maxDistanceFromPlayer = 0f;

    [Header("Test / Fallback")]
    [Tooltip("MapReadyEvent gelmese bile sahnede EnemySpawnPoint varsa wave loop'u baslat")]
    [SerializeField] private bool _spawnOnStartIfNoMapEvent = false;

    [Tooltip("Fallback durumda Start'tan sonra ne kadar bekle")]
    [SerializeField] private float _fallbackStartDelay = 2f;

    private int _targetEnemyCount;
    private int _aliveCount;
    private bool _waveLoopActive;
    private Coroutine _waveLoop;
    private int _prefabCursor;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        GameEventBus.Subscribe<MapReadyEvent>(OnMapReady);
        GameEventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        Debug.Log("[EnemySpawner] OnNetworkSpawn: MapReadyEvent + EnemyDiedEvent dinlemeye basladi.");

        if (_spawnOnStartIfNoMapEvent)
            Invoke(nameof(FallbackStart), _fallbackStartDelay);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        GameEventBus.Unsubscribe<MapReadyEvent>(OnMapReady);
        GameEventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        _waveLoopActive = false;
        if (_waveLoop != null) StopCoroutine(_waveLoop);
    }

    /// <summary>
    /// Observer: enemy oldukce alive count azalir. Wave loop bir sonraki interval'de
    /// bos slot'u gorur ve yeni enemy spawn eder. Boylece "bir oldu, biri geldi" akisi
    /// loose-coupled saglanir.
    /// </summary>
    private void OnEnemyDied(EnemyDiedEvent evt)
    {
        _aliveCount = Mathf.Max(0, _aliveCount - 1);
        Debug.Log($"[EnemySpawner] EnemyDiedEvent alindi (id={evt.EnemyId}). Alive: {_aliveCount}/{_targetEnemyCount}.");
    }

    private void OnMapReady(MapReadyEvent evt)
    {
        if (_waveLoopActive) return;

        _targetEnemyCount = CalculateTarget(evt.RoomCount);
        Debug.Log($"[EnemySpawner] MapReadyEvent alindi (rooms={evt.RoomCount}). Target enemy count: {_targetEnemyCount}.");
        StartWaveLoop();
    }

    private void FallbackStart()
    {
        if (_waveLoopActive) return;
        _targetEnemyCount = _fallbackTargetCount;
        Debug.Log($"[EnemySpawner] Fallback wave loop baslatildi. Target: {_targetEnemyCount}.");
        StartWaveLoop();
    }

    private int CalculateTarget(int roomCount)
    {
        if (_roomsPerEnemy <= 0) return _maxAliveEnemies;
        int formulaTarget = Mathf.Max(1, roomCount / _roomsPerEnemy);
        return Mathf.Min(_maxAliveEnemies, formulaTarget);
    }

    private void StartWaveLoop()
    {
        _waveLoopActive = true;
        _waveLoop = StartCoroutine(WaveSpawnLoop());
    }

    private IEnumerator WaveSpawnLoop()
    {
        yield return new WaitForSeconds(_firstSpawnDelay);

        while (_waveLoopActive)
        {
            if (_aliveCount < _targetEnemyCount)
                TrySpawnOne();

            yield return new WaitForSeconds(_spawnInterval);
        }
    }

    private bool TrySpawnOne()
    {
        if (_enemyPrefabs == null || _enemyPrefabs.Length == 0)
        {
            Debug.LogError("[EnemySpawner] Enemy prefab listesi bos.");
            return false;
        }

        var spawnPoints = FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None);
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[EnemySpawner] Sahnede EnemySpawnPoint yok, spawn atlandi.");
            return false;
        }

        var patrolGroups = FindObjectsByType<PatrolWaypointGroup>(FindObjectsSortMode.None);
        EnemySpawnPoint chosen = PickSpawnPoint(spawnPoints);
        if (chosen == null) return false;

        GameObject prefab = PickPrefab();
        if (prefab == null)
        {
            Debug.LogError("[EnemySpawner] Secilen prefab null.");
            return false;
        }

        Vector3 pos = chosen.transform.position;
        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        var enemyGo = Instantiate(prefab, pos, rot);
        var netObj = enemyGo.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[EnemySpawner] Enemy prefab'inda NetworkObject yok: {prefab.name}");
            Destroy(enemyGo);
            return false;
        }

        netObj.Spawn();

        // Her enemy'ye farkli avoidance priority ver. Ayni priority'de iki agent
        // dar gecitte/kapida birbirine yol vermeyip kilitleniyor (deadlock).
        // Farkli deger = dusuk priority bekler, yuksek gecer.
        var agent = enemyGo.GetComponent<NavMeshAgent>();
        if (agent != null)
            agent.avoidancePriority = Random.Range(20, 80);

        var ctrl = enemyGo.GetComponent<EnemyController>();
        if (ctrl != null)
        {
            var waypoints = ResolveWaypointsFor(chosen, patrolGroups);
            if (waypoints != null && waypoints.Length > 0)
                ctrl.SetWaypoints(waypoints);
        }

        _aliveCount++;
        Debug.Log($"[EnemySpawner] Yeni enemy spawn edildi ({prefab.name}). Alive: {_aliveCount}/{_targetEnemyCount}.");
        return true;
    }

    /// <summary>
    /// Round-robin prefab secimi: her spawn'da siradaki enemy tipini doner.
    /// Rastgele secim bazen tek tipi ust uste verebiliyordu; round-robin ile
    /// tum tipler (Type A, Type B...) garanti olarak sahnede gorunur.
    /// </summary>
    private GameObject PickPrefab()
    {
        if (_enemyPrefabs == null || _enemyPrefabs.Length == 0) return null;
        GameObject prefab = _enemyPrefabs[_prefabCursor % _enemyPrefabs.Length];
        _prefabCursor++;
        return prefab;
    }

    /// <summary>
    /// Player'dan min mesafede ve (varsa) max mesafenin altinda olan spawn point'leri filtreleyip
    /// rastgele birini doner. Player bulunamazsa veya filtre bos donerse fallback olarak
    /// havuzdan rastgele dondurur.
    /// </summary>
    private EnemySpawnPoint PickSpawnPoint(EnemySpawnPoint[] candidates)
    {
        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null)
            return candidates[Random.Range(0, candidates.Length)];

        Vector3 playerPos = playerObj.transform.position;
        float minSqr = _minDistanceFromPlayer * _minDistanceFromPlayer;
        float maxSqr = _maxDistanceFromPlayer > 0f
            ? _maxDistanceFromPlayer * _maxDistanceFromPlayer
            : float.MaxValue;

        var filtered = new List<EnemySpawnPoint>(candidates.Length);
        foreach (var sp in candidates)
        {
            if (sp == null) continue;
            float sqr = (sp.transform.position - playerPos).sqrMagnitude;
            if (sqr < minSqr) continue;
            if (sqr > maxSqr) continue;
            filtered.Add(sp);
        }

        if (filtered.Count == 0)
        {
            Debug.LogWarning("[EnemySpawner] Player mesafe filtresi sonucu bos. Rastgele spawn'a fallback.");
            return candidates[Random.Range(0, candidates.Length)];
        }

        return filtered[Random.Range(0, filtered.Count)];
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
