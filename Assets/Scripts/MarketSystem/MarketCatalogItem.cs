using System;
using UnityEngine;

[Serializable]
public class MarketCatalogItem
{
    [SerializeField] private ushort itemId;
    [SerializeField] private string displayName = "New Market Item";
    [SerializeField] [TextArea] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private int buyPrice = 10;
    [SerializeField] private int sellValue = 5;
    [SerializeField] private bool canBuy = true;
    [SerializeField] private bool canSell = true;
    [SerializeField] private int maxStock = -1;
    [SerializeField] private GameObject deliveryPrefab;

    public ushort ItemId => itemId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int BuyPrice => Mathf.Max(0, buyPrice);
    public int SellValue => Mathf.Max(0, sellValue);
    public bool CanBuy => canBuy;
    public bool CanSell => canSell;
    public int MaxStock => maxStock;
    public GameObject DeliveryPrefab => deliveryPrefab;
    public bool HasLimitedStock => maxStock >= 0;

#if UNITY_EDITOR
    public void EditorSetDefaults(
        ushort newItemId,
        string newDisplayName,
        string newDescription,
        int newBuyPrice,
        int newSellValue,
        bool newCanBuy,
        bool newCanSell,
        int newMaxStock)
    {
        itemId = newItemId;
        displayName = newDisplayName;
        description = newDescription;
        buyPrice = newBuyPrice;
        sellValue = newSellValue;
        canBuy = newCanBuy;
        canSell = newCanSell;
        maxStock = newMaxStock;
    }

    public void EditorSetDeliveryPrefab(GameObject prefab)
    {
        deliveryPrefab = prefab;
    }
#endif
}
