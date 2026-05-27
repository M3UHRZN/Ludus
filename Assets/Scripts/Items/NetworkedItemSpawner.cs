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

    [Tooltip("Random pozisyon NavMesh'e snap edilirken bu mesafe icinde walkable nokta aranir.")]
    [SerializeField] private float _navMeshSampleRadius = 2f;

    [Tooltip("NavMesh'e snap basarisiz olursa kac kez yeniden dene.")]
    [SerializeField] private int _placementRetryCount = 4;

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
    /// Verilen bounds icinde rastgele bir nokta secer, NavMesh'e snap eder.
    /// Snap basarili olursa pozisyonu yazip true doner. Aksi halde
    /// _placementRetryCount kadar tekrar dener; hala basarisizsa false doner.
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

        for (int attempt = 0; attempt < _placementRetryCount; attempt++)
        {
            float rx = Mathf.Lerp(min.x, max.x, (float)rng.NextDouble());
            float rz = Mathf.Lerp(min.z, max.z, (float)rng.NextDouble());
            Vector3 candidate = new Vector3(rx, room.center.y, rz);

            if (NavMesh.SamplePosition(candidate, out var hit, _navMeshSampleRadius, NavMesh.AllAreas))
            {
                position = hit.position;
                return true;
            }
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
