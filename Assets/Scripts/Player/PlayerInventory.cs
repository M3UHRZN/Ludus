using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    public const int MaxSlots = 4;
    private const int DefaultMarketStartingCredits = 100;
    private static int s_ServerMarketCredits = DefaultMarketStartingCredits;

    // PlayerSpawnCoordinator sahne gecisinde player'i despawn+respawn ediyor
    // (lobby prefab != gameplay prefab). Bu yuzden NetworkList<ushort> Slots
    // her geciste sifirlaniyor; lobbyde alinan flashbang map'e gitmiyordu.
    // Server-side static cache: OnNetworkDespawn'da snapshot, OnNetworkSpawn'da
    // ayni clientId icin restore yap. Boylece envanter sahne degisimine direnir.
    private static readonly System.Collections.Generic.Dictionary<ulong, (ushort[] slots, byte active)>
        s_ServerInventorySnapshot = new();

    public readonly NetworkList<ushort> Slots = new();

    public readonly NetworkVariable<byte> ActiveSlot = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Corpse Carry (Sprint 2 — Yasin) ──────────────────────────────────────
    public readonly NetworkVariable<bool> IsCarryingCorpse = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Usable Items")]
    [Tooltip("Pazardan alinan flashbang'in ItemCatalog id'si. Fiyat ItemDefinition.MarketPrice'tan okunur.")]
    [SerializeField] private ushort flashbangItemId = 1;

    private InputAction _scrollAction;
    private InputAction _useAction;
    private InputAction _dropAction;

    public override void OnNetworkSpawn()
    {
        // Server-only: bu clientId icin onceki sahnede snapshot alindiysa
        // restore et. IsOwner kontrolunden ONCE yapilmali cunku non-host
        // ortamlarinda server farkli client olabilir.
        if (IsServer && s_ServerInventorySnapshot.TryGetValue(OwnerClientId, out var saved))
        {
            Slots.Clear();
            for (int i = 0; i < saved.slots.Length && i < MaxSlots; i++)
                Slots.Add(saved.slots[i]);
            ActiveSlot.Value = (byte)Mathf.Min(saved.active, (byte)(Mathf.Max(0, Slots.Count - 1)));
            s_ServerInventorySnapshot.Remove(OwnerClientId);
        }

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        var input = GetComponent<PlayerInput>();
        _scrollAction = input.actions["Gameplay/Scroll"];
        _useAction    = input.actions["Gameplay/UseItem"];
        _dropAction   = input.actions["Gameplay/Drop"];

        // Esmanur UI kopru: Sunucu cantaya esya koydugunda / slot degisiminde
        // GameEventBus.Publish(LocalInventoryUpdatedEvent) otomatik tetikle.
        // Anonim lambda ile abone olursak unsubscribe edemeyiz; named handler kullaniyoruz.
        Slots.OnListChanged += OnSlotsChanged;
        ActiveSlot.OnValueChanged += OnActiveSlotChanged;

        // Ilk frame'de UI bir kere sifir state'le cizilsin
        TriggerUIUpdate();
    }

    public override void OnNetworkDespawn()
    {
        // Server-only: yok edilmeden once mevcut envanteri snapshot al ki
        // PlayerSpawnCoordinator sahne gecisinde yeni prefab spawn ettiginde
        // OnNetworkSpawn restore edebilsin (lobby -> map flashbang persist).
        if (IsServer && Slots != null && Slots.Count > 0)
        {
            var snap = new ushort[Slots.Count];
            for (int i = 0; i < Slots.Count; i++) snap[i] = Slots[i];
            s_ServerInventorySnapshot[OwnerClientId] = (snap, ActiveSlot.Value);
        }

        if (!IsOwner) return;

        Slots.OnListChanged -= OnSlotsChanged;
        ActiveSlot.OnValueChanged -= OnActiveSlotChanged;
    }

    private void OnSlotsChanged(Unity.Netcode.NetworkListEvent<ushort> changeEvent) => TriggerUIUpdate();
    private void OnActiveSlotChanged(byte prev, byte current) => TriggerUIUpdate();

    /// <summary>UI'a "cantam degisti, kendini yeniden ciz" sinyali fırlatır.
    /// Public yapildi cunku UI (InventoryUIController) sahne degisiminde geç
    /// subscribe olabiliyor; Awake/OnEnable'da bu metodu cagirip mevcut durumu
    /// yeniden cizebilsin diye.</summary>
    public void TriggerUIUpdate()
    {
        ushort[] currentItems = new ushort[Slots.Count];
        for (int i = 0; i < Slots.Count; i++)
        {
            currentItems[i] = Slots[i];
        }

        GameEventBus.Publish(new LocalInventoryUpdatedEvent(currentItems, ActiveSlot.Value));
    }

    private void Update()
    {
        if (_scrollAction == null) return;
        HandleScroll();

        bool usePressed = _useAction.WasPressedThisFrame() ||
                          (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame);
        if (usePressed)
            UseActiveItem();

        // Çantadan seçili eşyayı yere atma tuşuna basılırsa
        if (_dropAction != null && _dropAction.WasPressedThisFrame())
        {
            DropActiveItemFromInventory();
        }
    }

    // ÇANTADAN YERE ATMA OPERASYONU
    private void DropActiveItemFromInventory()
    {
        // Çanta boşsa hiçbir şey yapma
        if (Slots.Count == 0) return;

        // 1. Atılacak eşyanın ID'sini al
        ushort itemIdToDrop = Slots[ActiveSlot.Value];

        // 2. Eşyayı çantadan (Listeden) sil
        RemoveAtSlot(ActiveSlot.Value);

        // 3. Sunucuya "Bu eşyayı önüme fiziksel olarak geri yarat" de
        Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
        SpawnItemServerRpc(itemIdToDrop, spawnPos);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SpawnItemServerRpc(ushort itemId, Vector3 spawnPosition)
    {
        ItemCatalog catalog = ItemCatalog.Instance;
        GameObject itemPrefab = catalog != null ? catalog.GetPrefab(itemId) : null;

        if (itemPrefab != null)
        {
            GameObject spawned = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);
            spawned.GetComponent<NetworkObject>().Spawn();
        }
    }

    private void HandleScroll()
    {
        float scroll = _scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) < 0.01f) return;
        if (Slots.Count == 0) return;

        int dir  = scroll > 0 ? -1 : 1;
        int next = (ActiveSlot.Value + dir + Slots.Count) % Slots.Count;
        RequestActiveSlotServerRpc((byte)next);
    }

    // ── Item Yönetimi ─────────────────────────────────────────────────────────

    public bool TryAddItem(ushort itemId)
    {
        if (Slots.Count >= MaxSlots)  return false;
        if (IsCarryingCorpse.Value)   return false; // ceset taşırken item alınamaz
        AddItemServerRpc(itemId);
        return true;
    }

    public bool ServerTryAddItem(ushort itemId)
    {
        if (!IsServer) return false;
        if (Slots.Count >= MaxSlots) return false;
        if (IsCarryingCorpse.Value) return false;

        Slots.Add(itemId);
        return true;
    }

    public void RemoveAtSlot(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        RemoveAtSlotServerRpc(index);
    }

    public bool ServerTryRemoveAtSlot(int index, out ushort itemId)
    {
        itemId = 0;
        if (!IsServer) return false;
        if (index < 0 || index >= Slots.Count) return false;

        itemId = Slots[index];
        Slots.RemoveAt(index);
        if (ActiveSlot.Value >= Slots.Count && Slots.Count > 0)
            ActiveSlot.Value = (byte)(Slots.Count - 1);
        else if (Slots.Count == 0)
            ActiveSlot.Value = 0;

        return true;
    }

    public void RequestMarketFlashbangPurchase(Vector3 deliveryPosition, Vector3 deliveryForward)
    {
        if (!IsOwner) return;
        RequestMarketFlashbangPurchaseServerRpc(deliveryPosition, deliveryForward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestMarketFlashbangPurchaseServerRpc(Vector3 deliveryPosition, Vector3 deliveryForward, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (float.IsNaN(deliveryPosition.x) || float.IsNaN(deliveryPosition.y) || float.IsNaN(deliveryPosition.z)) return;
        if (float.IsNaN(deliveryForward.x) || float.IsNaN(deliveryForward.y) || float.IsNaN(deliveryForward.z)) return;

        if (Vector3.Distance(transform.position, deliveryPosition) > 8f)
        {
            SendInventoryMarketMessageRpc("Delivery point is too far away.");
            return;
        }

        ItemCatalog catalog = ItemCatalog.Instance;
        ItemDefinition flashDef = catalog != null ? catalog.GetById(flashbangItemId) : null;
        int price = flashDef != null ? flashDef.MarketPrice : 0;
        if (flashDef == null)
        {
            SendInventoryMarketMessageRpc("Flashbang not in ItemCatalog.");
            return;
        }
        if (s_ServerMarketCredits < price)
        {
            SendInventoryMarketMessageRpc("Not enough team credits.");
            return;
        }

        s_ServerMarketCredits -= price;
        ServerSpawnFlashbangPickup(deliveryPosition, deliveryForward);
        SendInventoryMarketMessageRpc($"Bought Flashbang. Team Credits: {s_ServerMarketCredits}");
    }

    // ── Corpse Carry (Sprint 2 — Yasin) ──────────────────────────────────────

    /// <summary>
    /// Ceset alınabilir mi? Slot dolu VEYA zaten ceset taşınıyorsa false.
    /// CorpseItem.OnCorpsePickedUp() çağırır.
    /// </summary>
    public bool IsFull()
    {
        return Slots.Count >= MaxSlots || IsCarryingCorpse.Value;
    }

    /// <summary>
    /// CorpseItem, ceset alındığında/bırakıldığında çağırır.
    /// Sadece Owner çağırabilir.
    /// </summary>
    public void SetCarryingCorpse(bool carrying)
    {
        if (!IsOwner) return;
        SetCarryingCorpseServerRpc(carrying);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetCarryingCorpseServerRpc(bool carrying)
    {
        IsCarryingCorpse.Value = carrying;
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AddItemServerRpc(ushort itemId)
    {
        if (Slots.Count >= MaxSlots) return;
        Slots.Add(itemId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RemoveAtSlotServerRpc(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        Slots.RemoveAt(index);
        if (ActiveSlot.Value >= Slots.Count && Slots.Count > 0)
            ActiveSlot.Value = (byte)(Slots.Count - 1);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestActiveSlotServerRpc(byte newSlot)
    {
        if (newSlot < Slots.Count)
            ActiveSlot.Value = newSlot;
    }

    private void UseActiveItem()
    {
        if (Slots.Count == 0) return;
        if (ActiveSlot.Value >= Slots.Count) return;

        Transform aimTransform = ResolveAimTransform();
        Vector3 direction = aimTransform != null ? aimTransform.forward : transform.forward;
        if (direction.sqrMagnitude < 0.0001f)
            direction = transform.forward;

        Vector3 origin = transform.position + Vector3.up * 1.45f + direction.normalized * 0.45f;
        UseActiveItemServerRpc(ActiveSlot.Value, origin, direction.normalized);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void UseActiveItemServerRpc(int slotIndex, Vector3 origin, Vector3 direction, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (slotIndex < 0 || slotIndex >= Slots.Count) return;
        if (direction.sqrMagnitude < 0.0001f) return;
        if (!Ludus.UsableItems.Core.FlashbangMath.IsThrowOriginValid(transform.position, origin, 4f))
        {
            SendInventoryMarketMessageRpc("Use request rejected.");
            return;
        }

        ushort itemId = Slots[slotIndex];
        ItemCatalog catalog = ItemCatalog.Instance;
        GameObject prefab = catalog != null ? catalog.GetPrefab(itemId) : null;
        if (prefab == null)
        {
            Debug.Log($"[Inventory] Item {itemId} is not a registered usable.");
            return;
        }

        // Defensive: a catalogued prefab must actually carry a UsableItem.
        if (prefab.GetComponent<UsableItem>() == null)
        {
            Debug.LogWarning($"[Inventory] Usable prefab for {itemId} has no UsableItem.");
            return;
        }

        // Consume the slot, spawn the item's world object, then hand off to it.
        ServerTryRemoveAtSlot(slotIndex, out _);

        GameObject instance = Instantiate(prefab, origin, Quaternion.LookRotation(direction));
        NetworkObject netObject = instance.GetComponent<NetworkObject>();
        if (netObject == null)
        {
            Debug.LogWarning($"[Inventory] Usable prefab for {itemId} has no NetworkObject.");
            Destroy(instance);
            return;
        }
        netObject.Spawn(true);

        var usable = instance.GetComponent<UsableItem>();
        var context = new UsableActivationContext(OwnerClientId, NetworkObject, origin, direction);
        usable.ServerActivate(context);
    }

    private Transform ResolveAimTransform()
    {
        var look = GetComponent<PlayerLook>();
        if (look != null && look.CameraTarget != null)
            return look.CameraTarget;

        Camera localCamera = GetComponentInChildren<Camera>();
        if (localCamera != null)
            return localCamera.transform;

        return transform;
    }

    private void ServerSpawnFlashbangPickup(Vector3 position, Vector3 forward)
    {
        ItemCatalog catalog = ItemCatalog.Instance;
        GameObject prefab = catalog != null ? catalog.GetPrefab(flashbangItemId) : null;
        if (prefab == null)
        {
            SendInventoryMarketMessageRpc("Flashbang prefab missing from ItemCatalog.");
            return;
        }
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;

        GameObject pickup = Instantiate(prefab, position, Quaternion.LookRotation(forward.normalized));
        NetworkObject netObject = pickup.GetComponent<NetworkObject>();
        if (netObject != null)
        {
            netObject.Spawn(true);
            if (pickup.TryGetComponent(out PhysicsObject physicsObject))
                physicsObject.ServerConfigureInventoryPickup(true, flashbangItemId);
        }

        Rigidbody rb = pickup.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce((Vector3.up + forward.normalized * 0.4f) * 1.5f, ForceMode.Impulse);
        }
    }

    [Rpc(SendTo.Owner)]
    private void SendInventoryMarketMessageRpc(string message)
    {
        MarketUIController ui = FindFirstObjectByType<MarketUIController>();
        if (ui != null)
            ui.SetExternalStatus(message);
        else
            Debug.Log($"[Market] {message}");
    }

    /// <summary>Cantadaki aktif esyayi siler ve ID'sini doner. UI extraction akisinda kullanilir.</summary>
    public bool TryTakeActiveItem(out ushort itemId)
    {
        itemId = 0;
        if (Slots.Count == 0) return false;

        itemId = Slots[ActiveSlot.Value];
        RemoveAtSlot(ActiveSlot.Value);
        return true;
    }
}
