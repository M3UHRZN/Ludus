using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Lobi sahnesinde, extraction'dan dönen (satılmayan) item'ları seçilen bir noktadan fiziksel
/// fırlatır. Market'in SpawnPurchasedItem deseninin birebir aynısı. deliveryPoint sahnede
/// istediğin yere konur. Server-only spawn; client'lar NGO ile senkron alır.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class LobbyLootDispenser : NetworkBehaviour
{
    [Header("Teslimat")]
    [Tooltip("Dönen item'ların fırlayacağı nokta (boşsa bu obje kullanılır).")]
    [SerializeField] private Transform deliveryPoint;

    [Tooltip("Fırlatma impulse şiddeti (market deliveryImpulse gibi).")]
    [SerializeField] private float launchImpulse = 1.5f;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        DispenseReturnedItems();
    }

    private void DispenseReturnedItems()
    {
        var ids = ExtractedItemReturnBuffer.Drain();
        if (ids.Count == 0) return;

        ItemCatalog catalog = ItemCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning("[LobbyLootDispenser] ItemCatalog yok — dönen item'lar spawn edilemedi.");
            return;
        }

        Transform point = deliveryPoint != null ? deliveryPoint : transform;
        Vector3 position = point.position;
        Quaternion rotation = point.rotation;

        foreach (ushort id in ids)
        {
            GameObject prefab = catalog.GetPrefab(id);
            if (prefab == null)
            {
                Debug.LogWarning($"[LobbyLootDispenser] id={id} için prefab bulunamadı, atlandı.");
                continue;
            }

            GameObject spawned = Instantiate(prefab, position, rotation);

            NetworkObject netObject = spawned.GetComponent<NetworkObject>();
            if (netObject == null)
            {
                Debug.LogWarning($"[LobbyLootDispenser] '{prefab.name}' üzerinde NetworkObject yok.");
                Destroy(spawned);
                continue;
            }

            netObject.Spawn(true);

            if (spawned.TryGetComponent(out PhysicsObject physicsObject))
                physicsObject.ServerConfigureInventoryPickup(true, id);

            Rigidbody rb = spawned.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce((Vector3.up + point.forward * 0.4f) * launchImpulse, ForceMode.Impulse);
        }
    }
}
