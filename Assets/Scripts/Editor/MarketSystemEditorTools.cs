using UnityEditor;
using UnityEngine;

/// <summary>
/// Voidhaul market debug menu items. Eski "Setup Test Scene Objects" menu silindi
/// (artik MarketCanvas iskeleti scene-baked + execute_code ile kuruluyor).
/// </summary>
public static class MarketSystemEditorTools
{
    [MenuItem("VoidHaul/Market/Add 100 Credits")]
    public static void AddCredits()
    {
        MarketWallet wallet = Object.FindFirstObjectByType<MarketWallet>();
        if (wallet == null)
        {
            Debug.LogWarning("[MarketSystemEditorTools] MarketWallet not found.");
            return;
        }

        Undo.RecordObject(wallet, "Add Market Credits");
        wallet.AddCredits(100);
        EditorUtility.SetDirty(wallet);
    }

    [MenuItem("VoidHaul/Market/Reset Credits")]
    public static void ResetCredits()
    {
        MarketWallet wallet = Object.FindFirstObjectByType<MarketWallet>();
        if (wallet == null)
        {
            Debug.LogWarning("[MarketSystemEditorTools] MarketWallet not found.");
            return;
        }

        Undo.RecordObject(wallet, "Reset Market Credits");
        wallet.ResetToStartingCredits();
        EditorUtility.SetDirty(wallet);
    }

    [MenuItem("VoidHaul/Market/Clear Local Inventory")]
    public static void ClearLocalInventory()
    {
        PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("[MarketSystemEditorTools] PlayerInventory not found.");
            return;
        }

        for (int i = inventory.Slots.Count - 1; i >= 0; i--)
            inventory.RemoveAtSlot(i);

        Debug.Log("[MarketSystemEditorTools] Inventory clear requested.");
    }
}
