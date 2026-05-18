// CorpseItem.cs
// Assets/Scripts/Items/CorpseItem.cs

using Unity.Netcode;
using UnityEngine;


[RequireComponent(typeof(PhysicsObject))]
public class CorpseItem : NetworkBehaviour
{
    // ------------------------------------------------------------------ Kimlik

    [Header("Corpse Identity")]
    [SerializeField] private string _ownerName     = "Unknown";
    [SerializeField] private ulong  _ownerClientId = 0;

    public string OwnerName     => _ownerName;
    public ulong  CorpseOwnerClientId => _ownerClientId;

    // ------------------------------------------------------------------ Durum

    public bool IsRevived { get; private set; } = false;

    private PhysicsObject   _physObj;
    private ulong           _lastCarrierId = ulong.MaxValue;

    // ------------------------------------------------------------------ Spawn

    public override void OnNetworkSpawn()
    {
        _physObj = GetComponent<PhysicsObject>();
        _physObj.NetIsHeld.OnValueChanged += OnHeldStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (_physObj != null)
            _physObj.NetIsHeld.OnValueChanged -= OnHeldStateChanged;
    }

    // ------------------------------------------------------------------ NetIsHeld hook

    private void OnHeldStateChanged(bool previous, bool current)
    {
        if (current) OnCorpsePickedUp();
        else         OnCorpseDropped();
    }

    private void OnCorpsePickedUp()
    {
        if (IsRevived) return;

        ulong carrierClientId = _physObj.NetGrabberClientId.Value;
        PlayerInventory inventory = GetInventory(carrierClientId);

        if (inventory != null && inventory.IsFull())
        {
            // Carry slot dolu — grab'i iptal et (sadece server)
            if (IsServer)
                _physObj.ServerStopHold();

            Debug.Log("[CorpseItem] Carry slot dolu — ceset alınamadı.");
            return;
        }

        _lastCarrierId = carrierClientId;

        // Owner client'ının inventory'sine ServerRpc üzerinden yaz
        if (inventory != null)
            inventory.SetCarryingCorpse(true);

        GameEventBus.Publish(new CorpsePickedUpEvent(_ownerClientId, carrierClientId));
        Debug.Log($"[CorpseItem] {_ownerName}'in cesedi alındı (carrier: {carrierClientId})");
    }

    private void OnCorpseDropped()
    {
        PlayerInventory inventory = GetInventory(_lastCarrierId);

        if (inventory != null)
            inventory.SetCarryingCorpse(false);

        GameEventBus.Publish(new CorpseDroppedEvent(_ownerClientId));
        Debug.Log($"[CorpseItem] {_ownerName}'in cesedi bırakıldı.");

        _lastCarrierId = ulong.MaxValue;
    }

    // ------------------------------------------------------------------ Yardımcı

    private static PlayerInventory GetInventory(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || clientId == ulong.MaxValue) return null;
        if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
        return client.PlayerObject?.GetComponent<PlayerInventory>();
    }

    // ------------------------------------------------------------------ Revival

    /// <summary>
    /// InfirmaryPod trigger'ı, ceset pod'a bırakılınca bunu çağırır.
    /// </summary>
    public void OnRevived()
    {
        if (IsRevived) return;
        IsRevived = true;

        GameEventBus.Publish(new PlayerRevivedEvent((int)_ownerClientId));
        Debug.Log($"[CorpseItem] {_ownerName} canlandı!");
        Destroy(gameObject, 0.5f);
    }

    // ------------------------------------------------------------------ Init

    /// <summary>
    /// PlayerDiedEvent handler'ı ceset prefab'ı spawn ettikten sonra bunu çağırır.
    /// </summary>
    public void Initialize(string ownerName, ulong ownerClientId)
    {
        _ownerName     = ownerName;
        _ownerClientId = ownerClientId;
    }
}