using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MarketTransactionService : NetworkBehaviour
{
    [SerializeField] private MarketWallet wallet;
    [SerializeField] private Transform deliveryPoint;
    [SerializeField] private float deliveryImpulse = 1.5f;

    private readonly List<ItemDefinition> _buyableCache = new();

    public MarketWallet Wallet => wallet;
    public Vector3 DeliveryPosition => deliveryPoint != null
        ? deliveryPoint.position
        : transform.position + transform.forward * 1.25f + Vector3.up * 0.5f;
    public Vector3 DeliveryForward => deliveryPoint != null ? deliveryPoint.forward : transform.forward;

    private void Awake()
    {
        if (wallet == null)
            wallet = GetComponent<MarketWallet>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && GetComponent<NetworkObject>() == null)
            gameObject.AddComponent<NetworkObject>();
    }
#endif

    public bool TryBuy(ushort itemId, out string message)
    {
        return TryBuyInternal(itemId, out message);
    }

    public void RequestBuy(ushort itemId, PlayerInventory buyerInventory)
    {
        if (buyerInventory == null || buyerInventory.NetworkObject == null)
            return;

        RequestBuyServerRpc(itemId, new NetworkObjectReference(buyerInventory.NetworkObject));
    }

    public void RequestSellOne(PlayerInventory sellerInventory, int slotIndex)
    {
        if (sellerInventory == null || sellerInventory.NetworkObject == null)
            return;

        RequestSellOneServerRpc(new NetworkObjectReference(sellerInventory.NetworkObject), slotIndex);
    }

    public void RequestSellAll(PlayerInventory sellerInventory)
    {
        if (sellerInventory == null || sellerInventory.NetworkObject == null)
            return;

        RequestSellAllServerRpc(new NetworkObjectReference(sellerInventory.NetworkObject));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestBuyServerRpc(ushort itemId, NetworkObjectReference buyerInventoryRef, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        if (!buyerInventoryRef.TryGet(out NetworkObject buyerObject))
        {
            SendMarketMessageRpc(rpcParams.Receive.SenderClientId, "Buyer inventory could not be resolved.");
            return;
        }

        PlayerInventory inventory = buyerObject.GetComponent<PlayerInventory>();
        if (inventory == null || inventory.OwnerClientId != rpcParams.Receive.SenderClientId)
        {
            SendMarketMessageRpc(rpcParams.Receive.SenderClientId, "Buyer inventory is invalid.");
            return;
        }

        bool success = TryBuyInternal(itemId, out string message);
        SendMarketMessageRpc(rpcParams.Receive.SenderClientId, message);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSellOneServerRpc(NetworkObjectReference sellerInventoryRef, int slotIndex, RpcParams rpcParams = default)
    {
        if (!TryResolveRequesterInventory(sellerInventoryRef, rpcParams.Receive.SenderClientId, out PlayerInventory inventory, out string error))
        {
            SendMarketMessageRpc(rpcParams.Receive.SenderClientId, error);
            return;
        }

        TrySellOne(inventory, slotIndex, out string message);
        SendMarketMessageRpc(rpcParams.Receive.SenderClientId, message);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSellAllServerRpc(NetworkObjectReference sellerInventoryRef, RpcParams rpcParams = default)
    {
        if (!TryResolveRequesterInventory(sellerInventoryRef, rpcParams.Receive.SenderClientId, out PlayerInventory inventory, out string error))
        {
            SendMarketMessageRpc(rpcParams.Receive.SenderClientId, error);
            return;
        }

        int soldCount = SellAll(inventory, out int totalCredits);
        string message = soldCount > 0
            ? $"Sold {soldCount} item(s) for {totalCredits} credits."
            : "No sellable items found.";
        SendMarketMessageRpc(rpcParams.Receive.SenderClientId, message);
    }

    private bool TryResolveRequesterInventory(
        NetworkObjectReference inventoryRef,
        ulong requesterClientId,
        out PlayerInventory inventory,
        out string message)
    {
        inventory = null;

        if (!inventoryRef.TryGet(out NetworkObject inventoryObject))
        {
            message = "Inventory could not be resolved.";
            return false;
        }

        inventory = inventoryObject.GetComponent<PlayerInventory>();
        if (inventory == null || inventory.OwnerClientId != requesterClientId)
        {
            message = "Inventory is invalid.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool TryBuyInternal(ushort itemId, out string message)
    {
        if (wallet == null)
        {
            message = "Market wallet is missing.";
            return false;
        }

        ItemDefinition def = ItemCatalog.Instance?.GetById(itemId);
        if (def == null)
        {
            message = "Item not in catalog.";
            return false;
        }

        if (!def.IsBuyable)
        {
            message = $"{def.DisplayName} cannot be bought.";
            return false;
        }

        if (def.WorldPrefab == null)
        {
            message = $"{def.DisplayName} is missing a world prefab.";
            Debug.LogError($"[Market] {def.DisplayName} cannot be bought because ItemDefinition.WorldPrefab is missing.");
            return false;
        }

        if (!wallet.TrySpend(def.MarketPrice))
        {
            message = "Not enough credits.";
            return false;
        }

        SpawnPurchasedItem(def);
        message = $"Bought {def.DisplayName}.";
        return true;
    }

    public bool TrySellOne(PlayerInventory inventory, int slotIndex, out string message)
    {
        if (!TryGetInventoryItem(inventory, slotIndex, out ushort itemId, out message))
            return false;

        if (!CanSellItem(itemId, out int sellValue, out message))
            return false;

        if (IsServer)
            inventory.ServerTryRemoveAtSlot(slotIndex, out _);
        else
            inventory.RemoveAtSlot(slotIndex);
        wallet.AddCredits(sellValue);
        message = $"Sold item {itemId} for {sellValue} credits.";
        return true;
    }

    public int SellAll(PlayerInventory inventory, out int totalCredits)
    {
        totalCredits = 0;
        if (inventory == null)
            return 0;

        int soldCount = 0;
        for (int i = inventory.Slots.Count - 1; i >= 0; i--)
        {
            ushort itemId = inventory.Slots[i];
            if (!CanSellItem(itemId, out int sellValue, out _))
                continue;

            if (IsServer)
                inventory.ServerTryRemoveAtSlot(i, out _);
            else
                inventory.RemoveAtSlot(i);
            totalCredits += sellValue;
            soldCount++;
        }

        if (totalCredits > 0)
            wallet.AddCredits(totalCredits);

        return soldCount;
    }

    public void SetWallet(MarketWallet value)
    {
        wallet = value;
    }

    public void SetDeliveryPoint(Transform value)
    {
        deliveryPoint = value;
    }

    /// <summary>UI buy listesini doldurmak icin; her cagrida tazelenir.</summary>
    public IReadOnlyList<ItemDefinition> GetBuyableItems()
    {
        _buyableCache.Clear();
        if (ItemCatalog.Instance != null)
            ItemCatalog.Instance.CollectBuyable(_buyableCache);
        return _buyableCache;
    }

    /// <summary>UI'da fiyati gostermek icin; satilamaz ise 0.</summary>
    public int GetSellPriceFor(ushort itemId)
    {
        ItemDefinition def = ItemCatalog.Instance?.GetById(itemId);
        return (def != null && def.IsSellable) ? def.SellPrice : 0;
    }

    /// <summary>UI'da fiyati gostermek icin; satin alinamaz ise 0.</summary>
    public int GetBuyPriceFor(ushort itemId)
    {
        ItemDefinition def = ItemCatalog.Instance?.GetById(itemId);
        return (def != null && def.IsBuyable) ? def.MarketPrice : 0;
    }

    private bool TryGetInventoryItem(PlayerInventory inventory, int slotIndex, out ushort itemId, out string message)
    {
        itemId = 0;

        if (inventory == null)
        {
            message = "Player inventory is missing.";
            return false;
        }

        if (slotIndex < 0 || slotIndex >= inventory.Slots.Count)
        {
            message = "Invalid inventory slot.";
            return false;
        }

        itemId = inventory.Slots[slotIndex];
        if (itemId == 0)
        {
            message = "Slot is empty.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool CanSellItem(ushort itemId, out int sellValue, out string message)
    {
        sellValue = 0;

        if (wallet == null)
        {
            message = "Market wallet is missing.";
            return false;
        }

        ItemDefinition def = ItemCatalog.Instance?.GetById(itemId);
        if (def == null)
        {
            message = "Item not in catalog.";
            return false;
        }

        if (!def.IsSellable)
        {
            message = "This item cannot be sold.";
            return false;
        }

        sellValue = def.SellPrice;
        if (sellValue <= 0)
        {
            message = "This item has no sell value.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void SpawnPurchasedItem(ItemDefinition def)
    {
        Vector3 position = deliveryPoint != null
            ? deliveryPoint.position
            : transform.position + transform.forward * 1.25f + Vector3.up * 0.5f;

        Quaternion rotation = deliveryPoint != null ? deliveryPoint.rotation : Quaternion.identity;
        GameObject spawned = Instantiate(def.WorldPrefab, position, rotation);
        // localScale prefab'tan miras alinir (1f); item-specific scale uygulamiyoruz.

        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb == null)
            rb = spawned.AddComponent<Rigidbody>();

        NetworkObject netObject = spawned.GetComponent<NetworkObject>();
        if (IsServer && netObject != null && !netObject.IsSpawned)
        {
            netObject.Spawn(true);
            if (spawned.TryGetComponent(out PhysicsObject physicsObject))
                physicsObject.ServerConfigureInventoryPickup(true, def.Id);
        }
        else if (IsServer && netObject == null)
        {
            Debug.LogWarning($"[Market] Delivery prefab for {def.DisplayName} is missing NetworkObject.");
        }

        rb.AddForce((Vector3.up + transform.forward * 0.4f) * deliveryImpulse, ForceMode.Impulse);
    }

    [Rpc(SendTo.Everyone)]
    private void SendMarketMessageRpc(ulong targetClientId, string message)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.LocalClientId != targetClientId) return;

        MarketUIController ui = FindFirstObjectByType<MarketUIController>();
        if (ui != null)
            ui.SetExternalStatus(message);
    }
}
