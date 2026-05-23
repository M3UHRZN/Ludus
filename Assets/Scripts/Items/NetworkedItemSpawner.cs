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
    [SerializeField] private bool _verbose = true;

    private int _totalSpawned;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
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

        // Asil placement sonraki task'larda doldurulacak.
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

    // Sonraki task'larda doldurulacak — su an no-op.
    private void DistributeItems(System.Random rng, Bounds[] rooms)
    {
        // Task 6 + 7 + 8 burayi dolduracak.
    }
}
