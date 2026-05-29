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
    [Tooltip("Ayni anda sahnede en fazla kac enemy olabilir (orn. 5 Type A + 1 Type B icin 6)")]
    [SerializeField] private int _maxAliveEnemies = 6;

    [Tooltip("Budget formulu: target = roomCount / roomsPerEnemy (sonra maxAlive ile sinirlanir)")]
    [SerializeField] private int _roomsPerEnemy = 7;

    [Header("Nadir Dusman (Type B)")]
    [Tooltip("Element 1 = nadir dusman (Type B). Her haritada en fazla bu kadar spawn olur. Gerisi Element 0 (Type A).")]
    [SerializeField] private int _rareEnemyMaxCount = 1;

    [Tooltip("MapReadyEvent gelmezse fallback hedef enemy sayisi")]
    [SerializeField] private int _fallbackTargetCount = 3;

    [Header("Wave Spawn")]
    [Tooltip("MapReadyEvent'ten sonra ilk enemy ne kadar sure sonra cikar")]
    [SerializeField] private float _firstSpawnDelay = 5f;

    [Tooltip("INITIAL FILL fazinda iki ardisik spawn arasi sure (saniye). Bu sirada " +
             "harita hizli doldurulur (oyuncu daha sahneyi tanirken).")]
    [SerializeField] private float _spawnInterval = 8f;

    [Tooltip("RESPAWN fazinda (initial fill tamamlandiktan sonra) bir Type A oldukten " +
             "sonra yenisinin gelmesi icin bekleme suresi. Priest (Type B) yine respawn " +
             "etmez - cap-1 (_rareEnemyMaxCount).")]
    [SerializeField] private float _respawnDelay = 35f;

    [Tooltip("True ise canli oyuncunun GORUS HATTINDA olan spawn point'ler tercih " +
             "edilmez (dusman oyuncunun gozunun onunde belirmesin). LOS-blocked nokta " +
             "yoksa fallback olarak normal mesafe filtresi kullanilir.")]
    [SerializeField] private bool _avoidLineOfSight = true;

    [Tooltip("LOS kontrolu icin maksimum kontrol mesafesi. Bu mesafenin uzaginda olan " +
             "spawn point'ler 'oyuncu gormez' kabul edilir (raycast atilmaz).")]
    [SerializeField] private float _losCheckRange = 35f;

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

    [Header("Loot Room Guardian (Feature 2)")]
    [Tooltip("Loot odalarinda dedicated robot bekciler spawn edilsin mi (oyuncuya zorluk + 'oh dusunmusler' hissi).")]
    [SerializeField] private bool _spawnLootGuardians = true;
    [Tooltip("MapReadyEvent'ten sonra item'larin spawn olmasini beklemek icin gecikme.")]
    [SerializeField] private float _lootGuardianDelay = 3f;
    [Tooltip("Toplam max kac loot guardian doganlir (cok olmasin, oyuncu nefes alsin).")]
    [SerializeField] private int _lootGuardianCap = 3;
    [Tooltip("Bir odaya guardian uretebilmek icin oda icindeki minimum item sayisi.")]
    [SerializeField] private int _minItemsPerLootRoom = 2;
    [Tooltip("Guardian'in gorus mesafesi (varsayilan 15m'den dusuk; sneak'e izin verir).")]
    [SerializeField] private float _guardianSightRange = 11f;
    [Tooltip("Guardian'in ranged saldiri tetik mesafesi (varsayilan 12m'den dusuk).")]
    [SerializeField] private float _guardianAttackRange = 9f;

    private int _targetEnemyCount;
    private int _aliveCount;
    private bool _waveLoopActive;
    private Coroutine _waveLoop;
    private int _rareSpawned;
    private bool _initialFillComplete;  // false -> _spawnInterval, true -> _respawnDelay

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == SceneNames.Lobby || activeScene == SceneNames.MainMenu)
        {
            Debug.Log("[EnemySpawner] Lobi veya Ana Menü sahnesi algılandı, spawner devre dışı bırakıldı.");
            return;
        }

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

        // Item'lar genelde MapReadyEvent ile yakin zamanda spawn olur ama
        // exact siralama garanti degil — kucuk bir gecikmeyle loot odalarini
        // tariyoruz. Wave loop'undan AYRI (extra) guardian'lar uretilir.
        if (_spawnLootGuardians && evt.RoomBounds != null && evt.RoomBounds.Length > 0)
            StartCoroutine(SpawnLootGuardiansDelayed(evt.RoomBounds));
    }

    /// <summary>
    /// Item'lar spawn olduktan sonra hangi odalarda item oldugunu tespit eder
    /// ve top-N odanin merkezine 1 robot guardian doğurur. Bu robotlar
    /// SetupAsLootGuardian ile yapilandirilir: oda disina cikmazlar, gorus
    /// kisa, oyuncu sneak edebilir; ama gorurse lazer + ates yapar.
    /// </summary>
    private IEnumerator SpawnLootGuardiansDelayed(Bounds[] roomBounds)
    {
        yield return new WaitForSeconds(_lootGuardianDelay);

        var items = FindObjectsByType<BaseItem>(FindObjectsSortMode.None);
        if (items == null || items.Length == 0)
        {
            Debug.Log("[EnemySpawner] Loot guardian icin item bulunamadi — atlandi.");
            yield break;
        }

        // Her oda icin item sayisini topla
        int[] itemsInRoom = new int[roomBounds.Length];
        for (int r = 0; r < roomBounds.Length; r++)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i] == null) continue;
                if (roomBounds[r].Contains(items[i].transform.position))
                    itemsInRoom[r]++;
            }
        }

        // Min eşigin altinda olmayan odalari sayilarina gore sirala (desc)
        var ranked = new List<(int idx, int count)>();
        for (int r = 0; r < itemsInRoom.Length; r++)
        {
            if (itemsInRoom[r] >= _minItemsPerLootRoom)
                ranked.Add((r, itemsInRoom[r]));
        }
        ranked.Sort((a, b) => b.count.CompareTo(a.count));

        int spawnCount = Mathf.Min(_lootGuardianCap, ranked.Count);
        Debug.Log($"[EnemySpawner] Loot guardian taramasi: {ranked.Count} aday oda, {spawnCount} guardian uretilecek.");

        for (int i = 0; i < spawnCount; i++)
            SpawnLootGuardianInRoom(roomBounds[ranked[i].idx]);
    }

    /// <summary>
    /// Oda merkezi yakininda NavMesh'e snap'lenmis bir noktada robot (Type A)
    /// instantiate eder, NetworkObject.Spawn ile replicate eder, sonra
    /// SetupAsLootGuardian ile guardian moduna gecirir. Bu robotlar EnemyDied
    /// event'ini de tetikler (alive count azalir) ama wave loop'una sayilmaz
    /// (counter ayri); wave bagimsiz devam eder.
    /// </summary>
    private void SpawnLootGuardianInRoom(Bounds roomBounds)
    {
        if (_enemyPrefabs == null || _enemyPrefabs.Length == 0) return;
        GameObject prefab = _enemyPrefabs[0]; // Type A (robot) garantili
        if (prefab == null) return;

        Vector3 raw = roomBounds.center;
        Vector3 spawnPos = raw;
        if (NavMesh.SamplePosition(raw, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            spawnPos = hit.position;

        Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        var enemyGo = Instantiate(prefab, spawnPos, rot);
        var netObj = enemyGo.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[EnemySpawner] Guardian prefab'inda NetworkObject yok: {prefab.name}");
            Destroy(enemyGo);
            return;
        }
        netObj.Spawn(true);

        var agent = enemyGo.GetComponent<NavMeshAgent>();
        if (agent != null) agent.avoidancePriority = Random.Range(20, 80);

        var ctrl = enemyGo.GetComponent<EnemyController>();
        if (ctrl != null)
        {
            Transform[] roomCorners = BuildRoomCornerWaypoints(roomBounds, enemyGo.transform);
            ctrl.SetupAsLootGuardian(_guardianSightRange, _guardianAttackRange, roomCorners);
        }

        _aliveCount++;
        Debug.Log($"[EnemySpawner] Loot guardian spawn edildi (oda merkezi={roomBounds.center}). Alive: {_aliveCount}.");
    }

    /// <summary>
    /// Oda Bounds'inin 4 kose noktasinda runtime Transform'lar uretip patrol
    /// waypoint'leri olarak doner. Y'yi spawn pos'undan alir (multi-level
    /// olabilir). Robot bu kose listesi arasinda turlayarak odayi "korur".
    /// </summary>
    private Transform[] BuildRoomCornerWaypoints(Bounds roomBounds, Transform parent)
    {
        Vector3 c = roomBounds.center;
        Vector3 e = roomBounds.extents * 0.6f; // tam kose yerine biraz ic
        float y = parent.position.y;

        Vector3[] corners = {
            new Vector3(c.x - e.x, y, c.z - e.z),
            new Vector3(c.x + e.x, y, c.z - e.z),
            new Vector3(c.x + e.x, y, c.z + e.z),
            new Vector3(c.x - e.x, y, c.z + e.z),
        };

        var list = new List<Transform>(4);
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 wp = corners[i];
            if (NavMesh.SamplePosition(wp, out NavMeshHit h, 3f, NavMesh.AllAreas))
                wp = h.position;

            var go = new GameObject($"GuardianWP_{i}");
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.position = wp;
            list.Add(go.transform);
        }
        return list.ToArray();
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

            // Initial fill devam ediyorsa hizli interval; tamamlandiysa yavas respawn.
            // Faz gecisi TrySpawnOne icinde aliveCount ilk kez target'a ulasinca yapilir.
            float interval = _initialFillComplete ? _respawnDelay : _spawnInterval;
            yield return new WaitForSeconds(interval);
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

        // destroyWithScene: true — RNGmap unload olunca NGO bu enemy'yi otomatik despawn etsin.
        // (NGO 2.11 Spawn() varsayilani false; false objeler sahne gecisinde lobiye/yeni run'a tasiniyor.)
        netObj.Spawn(true);

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

        // Initial fill bittiyse faz gecisi yap — bundan sonra wave loop respawn delay'e
        // baglanir (35sn default). Bir kez true, hep true (ölum gelse de respawn modu).
        string phase = _initialFillComplete ? "RESPAWN" : "INITIAL_FILL";
        if (!_initialFillComplete && _aliveCount >= _targetEnemyCount)
        {
            _initialFillComplete = true;
            Debug.Log($"[EnemySpawner] Initial fill tamamlandi. Respawn fazina geciliyor (delay={_respawnDelay}s).");
        }

        Debug.Log($"[EnemySpawner] [{phase}] Yeni enemy spawn edildi ({prefab.name}). Alive: {_aliveCount}/{_targetEnemyCount}.");
        return true;
    }

    /// <summary>
    /// Nadir dusman limitli prefab secimi.
    /// Element 0 = yaygin dusman (Type A), Element 1 = nadir dusman (Type B).
    /// Once _rareEnemyMaxCount kadar Type B spawn edilir (genelde 1), sonra
    /// tum spawn'lar Type A olur. Boylece her haritada tek bir guclu Type B,
    /// gerisi yaygin Type A cikar.
    /// </summary>
    private GameObject PickPrefab()
    {
        if (_enemyPrefabs == null || _enemyPrefabs.Length == 0) return null;

        if (_enemyPrefabs.Length >= 2 && _rareSpawned < _rareEnemyMaxCount)
        {
            _rareSpawned++;
            return _enemyPrefabs[1]; // nadir (Type B)
        }
        return _enemyPrefabs[0]; // yaygin (Type A)
    }

    /// <summary>
    /// Spawn point secim zinciri:
    ///   1. Mesafe filtresi (TUM canli oyunculardan min-max araliginda olanlar)
    ///   2. LOS filtresi (hicbir oyuncunun gormedigi noktalar tercih edilir)
    ///   3. Filtre bos donerse soft fallback (mesafe-only, sonra rastgele)
    /// Multiplayer'da TUM oyunculara karsi check yapilir — bir oyuncu gorse bile elenir.
    /// </summary>
    private EnemySpawnPoint PickSpawnPoint(EnemySpawnPoint[] candidates)
    {
        var players = PlayerStateMachine.ServerPlayers;
        if (players == null || players.Count == 0)
            return candidates[Random.Range(0, candidates.Length)];

        float minSqr = _minDistanceFromPlayer * _minDistanceFromPlayer;
        float maxSqr = _maxDistanceFromPlayer > 0f
            ? _maxDistanceFromPlayer * _maxDistanceFromPlayer
            : float.MaxValue;

        // 1) Mesafe filtresi (tum canli oyunculara karsi min/max kontrol)
        var distanceOk = new List<EnemySpawnPoint>(candidates.Length);
        foreach (var sp in candidates)
        {
            if (sp == null) continue;
            if (IsWithinAcceptableDistance(sp.transform.position, players, minSqr, maxSqr))
                distanceOk.Add(sp);
        }

        if (distanceOk.Count == 0)
        {
            Debug.LogWarning("[EnemySpawner] Mesafe filtresi sonucu bos. Rastgele spawn fallback.");
            return candidates[Random.Range(0, candidates.Length)];
        }

        // 2) LOS filtresi (oyuncularin gormedigi noktalar tercih)
        if (!_avoidLineOfSight) return distanceOk[Random.Range(0, distanceOk.Count)];

        var losBlocked = new List<EnemySpawnPoint>(distanceOk.Count);
        foreach (var sp in distanceOk)
        {
            if (!IsVisibleToAnyAlivePlayer(sp.transform.position, players))
                losBlocked.Add(sp);
        }

        if (losBlocked.Count > 0) return losBlocked[Random.Range(0, losBlocked.Count)];

        // 3) Hicbir LOS-blocked nokta yok — distance-only fallback
        Debug.LogWarning("[EnemySpawner] Tum mesafe-OK spawn noktalari LOS'ta. Distance-only fallback.");
        return distanceOk[Random.Range(0, distanceOk.Count)];
    }

    private bool IsWithinAcceptableDistance(Vector3 pos, List<PlayerStateMachine> players, float minSqr, float maxSqr)
    {
        // Spawn point HER canli oyuncudan min mesafenin disinda olmali
        // (bir oyuncudan uzak bile olsa baska bir oyuncunun burnunde olmamali)
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || !p.IsAlive) continue;
            float sqr = (pos - p.transform.position).sqrMagnitude;
            if (sqr < minSqr) return false;
            if (sqr > maxSqr) return false; // max var ise (0 = limit yok)
        }
        return true;
    }

    private bool IsVisibleToAnyAlivePlayer(Vector3 pos, List<PlayerStateMachine> players)
    {
        Vector3 spawnEye = pos + Vector3.up * 1.5f;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || !p.IsAlive) continue;
            Vector3 playerEye = p.transform.position + Vector3.up * 1.5f;
            Vector3 dir = spawnEye - playerEye;
            float dist = dir.magnitude;
            if (dist > _losCheckRange) continue;
            if (dist < 0.01f) return true;

            // Engelsiz yol varsa oyuncu gorur — kotu nokta
            if (!Physics.Raycast(playerEye, dir.normalized, dist))
                return true;
        }
        return false;
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
