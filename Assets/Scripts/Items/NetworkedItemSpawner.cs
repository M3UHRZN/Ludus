// Assets/Scripts/Items/NetworkedItemSpawner.cs
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Server-only item spawner. MapReadyEvent geldiginde oda bounds'larini
/// alir, seed'den deterministik System.Random uretir, her oda icin
/// LootEntry tablosundan weighted secim yaparak item'lari spawn eder.
/// NavMesh.SamplePosition ile zemin'e snap, NetworkObject.Spawn ile
/// tum peer'lara replikasyon.
///
/// Mimari:
///   - Sadece IsServer subscribe olur.
///   - MapReadyEvent.RoomBounds null/empty ise hicbir sey yapmaz.
///   - System.Random seed ile constructed; replay'lerde ayni dagilim.
///   - Toplam item bugeti _maxItemsPerRun ile sinirli.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkedItemSpawner : NetworkBehaviour
{
    [Header("Loot Table")]
    [Tooltip("Spawn edilebilecek item prefab'lari + agirliklari.")]
    [SerializeField] private LootEntry[] _lootTable;

    [Header("Budget")]
    [Tooltip("Bu run icinde toplam en fazla kac item spawn olabilir.")]
    [SerializeField] private int _maxItemsPerRun = 30;

    [Header("Placement")]
    [Tooltip("Random pozisyon, oda bounds'undan bu kadar icerden secilir (duvar bosluklari icin).")]
    [SerializeField] private float _boundsInset = 1f;

    [Tooltip("Random pozisyon NavMesh'e snap edilirken bu mesafe icinde walkable nokta aranir. " +
             "Cok genis tutulursa snap koridordan veya komsu odadan nokta cekebilir; 1m guvenli aralik.")]
    [SerializeField] private float _navMeshSampleRadius = 1f;

    [Tooltip("NavMesh'e snap basarisiz olursa kac kez yeniden dene. Snap basarili olsa bile " +
             "oda Bounds disinda kaldiysa yine retry'a girer (koridor/void spawn'i onlemek icin).")]
    [SerializeField] private int _placementRetryCount = 8;

    [Tooltip("True ise NavMesh.SamplePosition sonucunun hala oda Bounds'i icinde olmasi sart kosulur. " +
             "Aksi halde snap koridorun ortasinda valid bir nokta bulup ESYA DISARIDA SPAWN OLABILIR.")]
    [SerializeField] private bool _enforceSnapInsideRoom = true;

    [Tooltip("Snap noktasinin Y'sinin candidate Y'sinden bu kadar uzaga sapmasi kabul edilmez. " +
             "Duvar ustune snap olusursa Y cok yukarida olur, bu kontrol elenir. 1.5m tipik tek-kat oda.")]
    [SerializeField] private float _maxYDeviationFromFloor = 1.5f;

    [Tooltip("Candidate Y'sini room.min.y + bu offset yapariz — bounds tabani floor sayilir. " +
             "Boylece SamplePosition mid-air'dan baslayip yukari duvarlara snap'lemez.")]
    [SerializeField] private float _floorYOffset = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool _verbose = false;

    private int _totalSpawned;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == SceneNames.Lobby || activeScene == SceneNames.MainMenu)
        {
            if (_verbose)
                Debug.Log("[NetworkedItemSpawner] Lobi veya Ana Menü sahnesi algılandı, spawner devre dışı bırakıldı.");
            return;
        }

        GameEventBus.Subscribe<MapReadyEvent>(OnMapReady);
        if (_verbose)
            Debug.Log("[NetworkedItemSpawner] OnNetworkSpawn (server). MapReadyEvent dinleniyor.");
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;
        GameEventBus.Unsubscribe<MapReadyEvent>(OnMapReady);
    }

    private void OnMapReady(MapReadyEvent evt)
    {
        if (evt.RoomBounds == null || evt.RoomBounds.Length == 0)
        {
            if (_verbose)
                Debug.Log("[NetworkedItemSpawner] MapReadyEvent geldi ama RoomBounds bos — atlandi.");
            return;
        }

        if (_lootTable == null || _lootTable.Length == 0)
        {
            Debug.LogWarning("[NetworkedItemSpawner] LootTable bos, spawn atlandi.");
            return;
        }

        var rng = new System.Random(evt.Seed);
        if (_verbose)
            Debug.Log($"[NetworkedItemSpawner] MapReadyEvent alindi (seed={evt.Seed}, rooms={evt.RoomBounds.Length}). Spawn baslıyor.");

        DistributeItems(rng, evt.RoomBounds);

        if (_verbose)
            Debug.Log($"[NetworkedItemSpawner] Run sonu spawn sayisi: {_totalSpawned}/{_maxItemsPerRun}.");
    }

    /// <summary>
    /// Loot table'dan agirliklara gore bir entry secer. Tum agirliklar
    /// toplanir, [0,total) araliginda bir sayi cekilir, cumulative olarak
    /// hangi entry'ye dustugu bulunur. 0 veya negatif weight'li entry'ler
    /// haric tutulur. Gecerli entry yoksa null doner.
    /// </summary>
    private LootEntry? PickWeighted(System.Random rng)
    {
        float totalWeight = 0f;
        for (int i = 0; i < _lootTable.Length; i++)
        {
            if (_lootTable[i].Weight > 0f && _lootTable[i].Prefab != null)
                totalWeight += _lootTable[i].Weight;
        }

        if (totalWeight <= 0f)
        {
            Debug.LogWarning("[NetworkedItemSpawner] LootTable'da gecerli weight'li entry yok.");
            return null;
        }

        float roll = (float)rng.NextDouble() * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < _lootTable.Length; i++)
        {
            if (_lootTable[i].Weight <= 0f || _lootTable[i].Prefab == null) continue;
            cumulative += _lootTable[i].Weight;
            if (roll <= cumulative)
                return _lootTable[i];
        }

        // Float drift fallback: en son gecerli entry
        for (int i = _lootTable.Length - 1; i >= 0; i--)
        {
            if (_lootTable[i].Weight > 0f && _lootTable[i].Prefab != null)
                return _lootTable[i];
        }
        return null;
    }

    /// <summary>
    /// Random XZ + dusuk Y candidate ile NavMesh'e snap eder. Y'yi mid-air'dan
    /// degil bounds.min.y + offset'ten basliyoruz — boylece SamplePosition'in
    /// duvar ustundeki yuksek NavMesh patch'lerine snap yapip "esya havada"
    /// bug'i olusmasi imkansiz hale geliyor. 3 katmanli filtre:
    ///   (a) Snap basarili olmali
    ///   (b) Snap X/Z hala oda bounds icinde olmali (koridor/void engeli)
    ///   (c) Snap Y candidate Y'sinden _maxYDeviationFromFloor kadar uzak olmamali
    ///       (duvar ustune snap'i son kapali kapi olarak eliyoruz)
    /// _placementRetryCount kadar deneme; hicbir snap gecerli olmazsa false.
    /// </summary>
    private bool TryPickPositionInRoom(System.Random rng, Bounds room, out Vector3 position)
    {
        position = Vector3.zero;

        Vector3 min = room.min + new Vector3(_boundsInset, 0f, _boundsInset);
        Vector3 max = room.max - new Vector3(_boundsInset, 0f, _boundsInset);

        if (max.x <= min.x || max.z <= min.z)
        {
            // Inset, oda sinirindan buyuk. Tam bounds kullaniliyor.
            min = room.min;
            max = room.max;
        }

        // Floor Y olarak bounds tabani + kucuk offset. MapGenerator bounds'i
        // oda zemini + duvar yuksekligi seklinde olusturuyor (min.y = floor).
        float floorY = room.min.y + _floorYOffset;

        for (int attempt = 0; attempt < _placementRetryCount; attempt++)
        {
            float rx = Mathf.Lerp(min.x, max.x, (float)rng.NextDouble());
            float rz = Mathf.Lerp(min.z, max.z, (float)rng.NextDouble());

            // Candidate dusuk Y'de — SamplePosition radius'unun (1m) icinde
            // sadece floor seviyesindeki NavMesh olur, duvar ustundeki uzak patch'ler
            // 3D mesafe olarak ulasilmazsa snap orada yapilmaz.
            Vector3 candidate = new Vector3(rx, floorY, rz);

            if (!NavMesh.SamplePosition(candidate, out var navHit, _navMeshSampleRadius, NavMesh.AllAreas))
                continue;

            // (b) X/Z bounds icinde mi?
            if (_enforceSnapInsideRoom)
            {
                bool xzInside =
                    navHit.position.x >= room.min.x && navHit.position.x <= room.max.x &&
                    navHit.position.z >= room.min.z && navHit.position.z <= room.max.z;
                if (!xzInside)
                {
                    if (_verbose)
                        Debug.Log($"[NetworkedItemSpawner] Snap X/Z disinda ({navHit.position}), retry.");
                    continue;
                }
            }

            // (c) Y candidate'tan cok uzaklasti mi? Duvar ustune snap = Y cok yukarida.
            float yDelta = Mathf.Abs(navHit.position.y - candidate.y);
            if (yDelta > _maxYDeviationFromFloor)
            {
                if (_verbose)
                    Debug.Log($"[NetworkedItemSpawner] Snap Y deltasi cok ({yDelta:F2}m, snap={navHit.position.y:F2}), retry.");
                continue;
            }

            position = navHit.position;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Her oda icin LootEntry tablosunu gezer, MinPerRoom..MaxPerRoom
    /// araliginda hedef sayi belirler, her birinde PickWeighted ile prefab
    /// secer ve NavMesh-snap'li pozisyona NetworkObject.Spawn eder.
    /// _maxItemsPerRun'a ulasinca durur.
    /// </summary>
    private void DistributeItems(System.Random rng, Bounds[] rooms)
    {
        _totalSpawned = 0;

        for (int r = 0; r < rooms.Length; r++)
        {
            if (_totalSpawned >= _maxItemsPerRun) break;

            // Bu oda icin hedef sayi: her entry'nin Min..Max araligindan rastgele,
            // sonra hepsini toplama. Boylece "her odada 2-5 item arasi" gibi
            // organik bir dagilim cikar.
            int targetForRoom = 0;
            for (int e = 0; e < _lootTable.Length; e++)
            {
                var entry = _lootTable[e];
                if (entry.Prefab == null || entry.Weight <= 0f) continue;
                int min = Mathf.Max(0, entry.MinPerRoom);
                int max = Mathf.Max(min, entry.MaxPerRoom);
                if (max <= 0) continue;
                // [min, max] inclusive — System.Random.Next(min, max+1)
                targetForRoom += rng.Next(min, max + 1);
            }

            for (int i = 0; i < targetForRoom; i++)
            {
                if (_totalSpawned >= _maxItemsPerRun) break;

                var pick = PickWeighted(rng);
                if (!pick.HasValue) break;

                if (!TryPickPositionInRoom(rng, rooms[r], out var pos))
                {
                    if (_verbose)
                        Debug.LogWarning($"[NetworkedItemSpawner] Oda {r} icin NavMesh snap basarisiz, atlandi.");
                    continue;
                }

                SpawnOne(pick.Value.Prefab, pos);
                _totalSpawned++;
            }
        }
    }

    /// <summary>
    /// Server'da prefab'i Instantiate edip NetworkObject.Spawn cagirir.
    /// Prefab'da NetworkObject yoksa hata loglayip atlar (ItemRegistry'deki
    /// prefab'lari guncellemek inspector isi — kod runtime crash'i secmez).
    /// </summary>
    private void SpawnOne(BaseItem prefab, Vector3 position)
    {
        if (prefab == null)
        {
            Debug.LogError("[NetworkedItemSpawner] SpawnOne null prefab ile cagrildi. Atlandi.");
            return;
        }

        var go = Instantiate(prefab.gameObject, position, Quaternion.identity);
        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[NetworkedItemSpawner] Prefab '{prefab.name}' uzerinde NetworkObject yok. Spawn iptal edildi.");
            Destroy(go);
            return;
        }

        // destroyWithScene: true — RNGmap unload olunca NGO bu item'i otomatik despawn etsin.
        // (NGO 2.11 Spawn() varsayilani false; false objeler sahne gecisinde lobiye/yeni run'a tasiniyor.)
        netObj.Spawn(true);

        if (_verbose)
            Debug.Log($"[NetworkedItemSpawner] Spawned '{prefab.name}' at {position}.");
    }
}
