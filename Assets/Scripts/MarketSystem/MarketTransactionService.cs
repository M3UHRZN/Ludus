using UnityEngine;

public class MarketTransactionService : MonoBehaviour
{
    [SerializeField] private MarketCatalog catalog;
    [SerializeField] private MarketWallet wallet;
    [SerializeField] private Transform deliveryPoint;
    [SerializeField] private float deliveryImpulse = 1.5f;

    public MarketWallet Wallet => wallet;
    public MarketCatalog Catalog => catalog;

    private void Awake()
    {
        if (catalog == null)
            catalog = GetComponent<MarketCatalog>();

        if (wallet == null)
            wallet = GetComponent<MarketWallet>();
    }

    public bool TryBuy(ushort itemId, out string message)
    {
        if (catalog == null)
        {
            message = "Market catalog is missing.";
            return false;
        }

        if (wallet == null)
        {
            message = "Market wallet is missing.";
            return false;
        }

        if (!catalog.TryGetItem(itemId, out MarketCatalogItem item))
        {
            message = "Item is not in the market catalog.";
            return false;
        }

        if (!item.CanBuy)
        {
            message = $"{item.DisplayName} cannot be bought.";
            return false;
        }

        if (!wallet.TrySpend(item.BuyPrice))
        {
            message = "Not enough credits.";
            return false;
        }

        SpawnPurchasedItem(item);
        message = $"Bought {item.DisplayName}.";
        return true;
    }

    public bool TrySellOne(PlayerInventory inventory, int slotIndex, out string message)
    {
        if (!TryGetInventoryItem(inventory, slotIndex, out ushort itemId, out message))
            return false;

        if (!CanSellItem(itemId, out int sellValue, out message))
            return false;

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

            inventory.RemoveAtSlot(i);
            totalCredits += sellValue;
            soldCount++;
        }

        if (totalCredits > 0)
            wallet.AddCredits(totalCredits);

        return soldCount;
    }

    public void SetCatalog(MarketCatalog value)
    {
        catalog = value;
    }

    public void SetWallet(MarketWallet value)
    {
        wallet = value;
    }

    public void SetDeliveryPoint(Transform value)
    {
        deliveryPoint = value;
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
        message = string.Empty;
        return true;
    }

    private bool CanSellItem(ushort itemId, out int sellValue, out string message)
    {
        sellValue = 0;

        if (catalog == null)
        {
            message = "Market catalog is missing.";
            return false;
        }

        if (wallet == null)
        {
            message = "Market wallet is missing.";
            return false;
        }

        if (!catalog.CanSell(itemId))
        {
            message = "This item cannot be sold.";
            return false;
        }

        sellValue = catalog.GetSellValue(itemId);
        if (sellValue <= 0)
        {
            message = "This item has no sell value.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void SpawnPurchasedItem(MarketCatalogItem item)
    {
        Vector3 position = deliveryPoint != null
            ? deliveryPoint.position
            : transform.position + transform.forward * 1.25f + Vector3.up * 0.5f;

        Quaternion rotation = deliveryPoint != null ? deliveryPoint.rotation : Quaternion.identity;
        GameObject spawned;

        if (item.DeliveryPrefab != null)
        {
            spawned = Instantiate(item.DeliveryPrefab, position, rotation);
        }
        else
        {
            spawned = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spawned.name = $"{item.DisplayName} Delivery";
            spawned.transform.SetPositionAndRotation(position, rotation);
            spawned.transform.localScale = new Vector3(0.25f, 0.25f, 0.45f);

            Renderer renderer = spawned.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(0.92f, 0.95f, 1f);
        }

        Rigidbody rb = spawned.GetComponent<Rigidbody>();
        if (rb == null)
            rb = spawned.AddComponent<Rigidbody>();

        rb.AddForce((Vector3.up + transform.forward * 0.4f) * deliveryImpulse, ForceMode.Impulse);
    }
}
