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
        if (IsServer)
        {
            if (s_ServerInventorySnapshot.TryGetValue(OwnerClientId, out var saved))
            {
                Slots.Clear();
                for (int i = 0; i < MaxSlots; i++)
                {
                    if (i < saved.slots.Length)
                        Slots.Add(saved.slots[i]);
                    else
                        Slots.Add(0);
                }
                ActiveSlot.Value = (byte)Mathf.Min(saved.active, (byte)(MaxSlots - 1));
                s_ServerInventorySnapshot.Remove(OwnerClientId);
            }
            else
            {
                Slots.Clear();
                for (int i = 0; i < MaxSlots; i++)
                    Slots.Add(0);
                ActiveSlot.Value = 0;
            }
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
        if (IsServer)
        {
            // EquippableItem (torch vb.) elde tutuluyorsa sahne gecisinde kaybolmasin
            // diye snapshot ALMADAN ONCE envantere geri ekle. Sonra world objesini
            // despawn ediyoruz; OnNetworkSpawn yeni sahnede restore edip oyuncu
            // E-equip ile tekrar elde alabilsin.
            TryMigrateHeldEquippableToInventory();

            if (Slots != null && Slots.Count > 0)
            {
                var snap = new ushort[Slots.Count];
                for (int i = 0; i < Slots.Count; i++) snap[i] = Slots[i];
                s_ServerInventorySnapshot[OwnerClientId] = (snap, ActiveSlot.Value);
            }
        }

        if (!IsOwner) return;

        Slots.OnListChanged -= OnSlotsChanged;
        ActiveSlot.OnValueChanged -= OnActiveSlotChanged;
    }

    /// <summary>
    /// Server-side: oyuncunun elinde EquippableItem (torch vb.) varsa, scene
    /// gecisinde kaybolmasin diye envantere geri ekle ve world objesini despawn et.
    /// </summary>
    private void TryMigrateHeldEquippableToInventory()
    {
        if (!IsServer) return;

        PhysicsObject[] all = FindObjectsByType<PhysicsObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int migrated = 0;
        for (int i = 0; i < all.Length; i++)
        {
            PhysicsObject po = all[i];
            if (po == null) continue;
            if (!po.NetIsHeld.Value) continue;
            if (po.NetGrabberClientId.Value != OwnerClientId) continue;
            if (po.GetComponent<EquippableItem>() == null) continue;

            BaseItem baseItem = po.GetComponent<BaseItem>();
            if (baseItem == null) continue;
            ushort itemId = baseItem.ItemId;
            if (itemId == 0) continue;

            // Bos slot bul ve ekle. Slots boyutu sabit MaxSlots; 0 = bos.
            bool added = false;
            int targetSlot = -1;
            for (int s = 0; s < Slots.Count; s++)
            {
                if (Slots[s] == 0)
                {
                    Slots[s] = itemId;
                    targetSlot = s;
                    added = true;
                    break;
                }
            }
            if (!added && Slots.Count < MaxSlots)
            {
                Slots.Add(itemId);
                targetSlot = Slots.Count - 1;
                added = true;
            }
            // Yeni sahnede E ile direkt geri alabilmesi icin aktif slotu torch'a kaydir.
            if (added && targetSlot >= 0 && targetSlot < MaxSlots)
                ActiveSlot.Value = (byte)targetSlot;

            if (added)
            {
                migrated++;
                Debug.Log($"[PlayerInventory] Migrated held equippable id={itemId} -> slot {targetSlot} (clientId={OwnerClientId}).");
            }
            else
            {
                Debug.LogWarning($"[PlayerInventory] Held equippable id={itemId} migration FAILED — envanter dolu, item kayboldu.");
            }

            // World objesini despawn et: yeni sahnede yeni instance spawn edilecek.
            NetworkObject netObj = po.NetworkObject;
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(true);
        }

        if (migrated > 0)
            Debug.Log($"[PlayerInventory] OnNetworkDespawn migrated {migrated} equippable(s) to inventory for client {OwnerClientId}.");
    }

    private void OnSlotsChanged(Unity.Netcode.NetworkListEvent<ushort> changeEvent) => TriggerUIUpdate();
    private void OnActiveSlotChanged(byte prev, byte current) => TriggerUIUpdate();

    /// <summary>UI'a "cantam degisti, kendini yeniden ciz" sinyali fırlatır.</summary>
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

        bool usePressed = _useAction != null && _useAction.WasPressedThisFrame();
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
        // Elimizde fiziksel bir eşya tutuyorsak, çantadan eşya atmayalım!
        var interaction = GetComponent<PlayerInteraction>();
        if (interaction != null && interaction.HeldObject != null)
        {
            return;
        }

        if (ActiveSlot.Value >= Slots.Count) return;

        // Çantanın seçili slotu boşsa hiçbir şey yapma
        if (Slots[ActiveSlot.Value] == 0) return;

        // 1. Atılacak eşyanın ID'sini al
        ushort itemIdToDrop = Slots[ActiveSlot.Value];

        // 2. Eşyayı çantadan sil (0 yap)
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
        if (IsFull()) return false;
        AddItemServerRpc(itemId);
        return true;
    }

    public bool ServerTryAddItem(ushort itemId)
    {
        if (!IsServer) return false;
        if (IsFull()) return false;

        // Önce seçili (aktif) slot boş mu kontrol et, boşsa ona koy!
        if (ActiveSlot.Value < Slots.Count && Slots[ActiveSlot.Value] == 0)
        {
            Slots[ActiveSlot.Value] = itemId;
            return true;
        }

        // Aktif slot doluysa, ilk boş slotu bul
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i] == 0)
            {
                Slots[i] = itemId;
                return true;
            }
        }
        return false;
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
        if (Slots[index] == 0) return false;

        itemId = Slots[index];
        Slots[index] = 0;
        return true;
    }

    public void RequestMarketFlashbangPurchase(Vector3 deliveryPosition, Vector3 deliveryForward)
    {
        if (!IsOwner) return;
        RequestMarketItemPurchase(flashbangItemId, deliveryPosition, deliveryForward);
    }

    public void RequestMarketItemPurchase(ushort itemId, Vector3 deliveryPosition, Vector3 deliveryForward)
    {
        if (!IsOwner) return;
        RequestMarketItemPurchaseServerRpc(itemId, deliveryPosition, deliveryForward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestMarketFlashbangPurchaseServerRpc(Vector3 deliveryPosition, Vector3 deliveryForward, RpcParams rpcParams = default)
    {
        ServerHandleMarketItemPurchase(flashbangItemId, deliveryPosition, deliveryForward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestMarketItemPurchaseServerRpc(ushort itemId, Vector3 deliveryPosition, Vector3 deliveryForward, RpcParams rpcParams = default)
    {
        ServerHandleMarketItemPurchase(itemId, deliveryPosition, deliveryForward);
    }

    private void ServerHandleMarketItemPurchase(ushort itemId, Vector3 deliveryPosition, Vector3 deliveryForward)
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
        ItemDefinition itemDef = catalog != null ? catalog.GetById(itemId) : null;
        int price = itemDef != null ? itemDef.MarketPrice : 0;
        if (itemDef == null)
        {
            SendInventoryMarketMessageRpc("Item is not in ItemCatalog.");
            return;
        }
        if (!itemDef.IsBuyable)
        {
            SendInventoryMarketMessageRpc($"{itemDef.DisplayName} cannot be bought.");
            return;
        }
        if (s_ServerMarketCredits < price)
        {
            SendInventoryMarketMessageRpc("Not enough team credits.");
            return;
        }

        s_ServerMarketCredits -= price;
        ServerSpawnMarketPickup(itemDef.Id, itemDef.DisplayName, deliveryPosition, deliveryForward);
        SendInventoryMarketMessageRpc($"Bought {itemDef.DisplayName}. Team Credits: {s_ServerMarketCredits}");
    }

    // ── Corpse Carry (Sprint 2 — Yasin) ──────────────────────────────────────

    /// <summary>
    /// Ceset alınabilir mi? Slot dolu VEYA zaten ceset taşınıyorsa false.
    /// CorpseItem.OnCorpsePickedUp() çağırır.
    /// </summary>
    public bool IsFull()
    {
        if (IsCarryingCorpse.Value) return true;
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i] == 0) return false;
        }
        return true;
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
        // Önce seçili (aktif) slot boş mu kontrol et, boşsa ona koy!
        if (ActiveSlot.Value < Slots.Count && Slots[ActiveSlot.Value] == 0)
        {
            Slots[ActiveSlot.Value] = itemId;
            return;
        }

        // Aktif slot doluysa, ilk boş slotu bul
        for (int i = 0; i < Slots.Count; i++)
        {
            if (Slots[i] == 0)
            {
                Slots[i] = itemId;
                break;
            }
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RemoveAtSlotServerRpc(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        Slots[index] = 0;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestActiveSlotServerRpc(byte newSlot)
    {
        if (newSlot < Slots.Count)
            ActiveSlot.Value = newSlot;
    }

    private void UseActiveItem()
    {
        // Yeni akış: kullanım ELDEKİ usable item'ı "arm" eder (cooking). Spawn yok.
        // Flashbang'i önce F ile ele al, sonra Use ile arm et, sonra sağtık ile fırlat.
        var interaction = GetComponent<PlayerInteraction>();
        PhysicsObject held = interaction != null ? interaction.HeldObject : null;
        if (held == null) return;
        if (held.GetComponent<UsableItem>() == null) return;
        if (held.NetworkObject == null) return;
        UseHeldItemServerRpc(new NetworkObjectReference(held.NetworkObject));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void UseHeldItemServerRpc(NetworkObjectReference heldRef, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!heldRef.TryGet(out NetworkObject heldObj)) return;
        if (!heldObj.TryGetComponent(out PhysicsObject physObj)) return;
        if (!heldObj.TryGetComponent(out UsableItem usable)) return;
        // Yalnız item'ı tutan oyuncu arm edebilir (anti-cheat).
        if (physObj.NetGrabberClientId.Value != rpcParams.Receive.SenderClientId) return;

        var context = new UsableActivationContext(OwnerClientId, NetworkObject, transform.position, transform.forward);
        usable.ServerActivate(context);
    }

    private void ServerSpawnFlashbangPickup(Vector3 position, Vector3 forward)
    {
        ServerSpawnMarketPickup(flashbangItemId, "Flashbang", position, forward);
    }

    private void ServerSpawnMarketPickup(ushort itemId, string itemName, Vector3 position, Vector3 forward)
    {
        ItemCatalog catalog = ItemCatalog.Instance;
        GameObject prefab = catalog != null ? catalog.GetPrefab(itemId) : null;
        if (prefab == null)
        {
            SendInventoryMarketMessageRpc($"{itemName} prefab missing from ItemCatalog.");
            return;
        }
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;

        GameObject pickup = Instantiate(prefab, position, Quaternion.LookRotation(forward.normalized));
        NetworkObject netObject = pickup.GetComponent<NetworkObject>();
        if (netObject != null)
        {
            netObject.Spawn(true);
            if (pickup.TryGetComponent(out PhysicsObject physicsObject))
                physicsObject.ServerConfigureInventoryPickup(true, itemId);
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
        if (ActiveSlot.Value >= Slots.Count) return false;
        if (Slots[ActiveSlot.Value] == 0) return false;

        itemId = Slots[ActiveSlot.Value];
        RemoveAtSlot(ActiveSlot.Value);
        return true;
    }

    public void ClearInventory()
    {
        if (IsServer)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                Slots[i] = 0;
            }
        }
    }
}
