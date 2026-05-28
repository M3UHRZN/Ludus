using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    public const int MaxSlots = 4;

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

    private InputAction _scrollAction;
    private InputAction _useAction;
    private InputAction _dropAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        var input = GetComponent<PlayerInput>();
        _scrollAction = input.actions["Gameplay/Scroll"];
        _useAction = input.actions["Gameplay/UseItem"];
        _dropAction = input.actions["Gameplay/Drop"];

        // === KÖPRÜYÜ BAĞLIYORUZ ===
        // Sunucu çantaya eşya koyduğunda veya slot değiştiğinde otomatik UI'ı tetikle
        Slots.OnListChanged += (changeEvent) => TriggerUIUpdate();
        ActiveSlot.OnValueChanged += (prev, current) => TriggerUIUpdate();

        // Oyun başladığında UI'ı sıfır haliyle bir kere çizdiriyoruz
        TriggerUIUpdate();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        // Obje silinirken abonelikleri temizle (hata almamak için şart)
        Slots.OnListChanged -= (changeEvent) => TriggerUIUpdate();
        ActiveSlot.OnValueChanged -= (prev, current) => TriggerUIUpdate();
    }

    // Arayüze "Çantam değişti, kendini yeniden çiz!" sinyali fırlatır
    private void TriggerUIUpdate()
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

        if (_useAction.WasPressedThisFrame())
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
        // Veritabanından gerçek Prefab'ı çek ve yere fırlat!
        GameObject itemPrefab = ItemDatabase.Instance.GetPrefab(itemId);
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

    public void RemoveAtSlot(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        RemoveAtSlotServerRpc(index);
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

    public void RequestFlashbang(Vector3 origin, float radius, float duration)
    {
        if (!IsOwner) return;
        FlashbangServerRpc(origin, radius, duration);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void FlashbangServerRpc(Vector3 origin, float radius, float duration)
    {
        if (!IsServer) return;
        if (radius <= 0f || radius > 50f) return;
        if (duration <= 0f || duration > 30f) return;
        if (float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z)) return;

        if (Vector3.Distance(transform.position, origin) > radius + 2f) return;

        Collider[] hits = Physics.OverlapSphere(origin, radius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyController>();
            if (enemy == null) continue;
            enemy.SetBlinded(true, duration);
        }
    }

    private void UseActiveItem()
    {
        if (Slots.Count == 0) return;
        Debug.Log($"[Inventory] Use item at slot {ActiveSlot.Value}: ID={Slots[ActiveSlot.Value]}");
    }

    // Çantadaki aktif eşyayı siler ve ID'sini döndürür
    public bool TryTakeActiveItem(out ushort itemId)
    {
        itemId = 0;
        if (Slots.Count == 0) return false; // Çanta boşsa çık

        itemId = Slots[ActiveSlot.Value]; // Eşyanın ID'sini al
        RemoveAtSlot(ActiveSlot.Value);   // Çantadan (ve UI'dan) sil
        return true;
    }
}