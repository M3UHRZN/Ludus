using UnityEngine;

public class MarketDebugTools : MonoBehaviour
{
    [SerializeField] private MarketWallet wallet;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private int creditsToAdd = 100;
    [SerializeField] private ushort debugItemId = 1;
    [SerializeField] private ushort flashbangItemId = 100;

    private void Reset()
    {
        wallet = FindFirstObjectByType<MarketWallet>();
        inventory = FindFirstObjectByType<PlayerInventory>();
    }

    [ContextMenu("Market/Add Credits")]
    public void AddCredits()
    {
        if (wallet == null) wallet = FindFirstObjectByType<MarketWallet>();
        if (wallet == null)
        {
            Debug.LogWarning("[MarketDebugTools] MarketWallet not found.");
            return;
        }

        wallet.AddCredits(creditsToAdd);
        Debug.Log($"[MarketDebugTools] Added {creditsToAdd} credits. Current={wallet.CurrentCredits}");
    }

    [ContextMenu("Market/Reset Credits")]
    public void ResetCredits()
    {
        if (wallet == null) wallet = FindFirstObjectByType<MarketWallet>();
        if (wallet == null)
        {
            Debug.LogWarning("[MarketDebugTools] MarketWallet not found.");
            return;
        }

        wallet.ResetToStartingCredits();
        Debug.Log($"[MarketDebugTools] Credits reset. Current={wallet.CurrentCredits}");
    }

    [ContextMenu("Inventory/Fill With Debug Item")]
    public void FillInventoryWithDebugItem()
    {
        if (!ResolveInventory())
            return;

        while (inventory.Slots.Count < PlayerInventory.MaxSlots)
        {
            if (!inventory.TryAddItem(debugItemId))
                break;
        }

        Debug.Log($"[MarketDebugTools] Filled inventory with item ID {debugItemId}.");
    }

    [ContextMenu("Inventory/Add Flashbang")]
    public void AddFlashbang()
    {
        if (!ResolveInventory())
            return;

        bool added = inventory.TryAddItem(flashbangItemId);
        Debug.Log(added
            ? $"[MarketDebugTools] Added flashbang item ID {flashbangItemId}."
            : "[MarketDebugTools] Could not add flashbang.");
    }

    [ContextMenu("Inventory/Clear Inventory")]
    public void ClearInventory()
    {
        if (!ResolveInventory())
            return;

        for (int i = inventory.Slots.Count - 1; i >= 0; i--)
            inventory.RemoveAtSlot(i);

        Debug.Log("[MarketDebugTools] Inventory clear requested.");
    }

    private bool ResolveInventory()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();

        if (inventory != null)
            return true;

        Debug.LogWarning("[MarketDebugTools] PlayerInventory not found.");
        return false;
    }
}
