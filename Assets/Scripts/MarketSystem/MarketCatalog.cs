using System.Collections.Generic;
using UnityEngine;

public class MarketCatalog : MonoBehaviour
{
    [SerializeField] private List<MarketCatalogItem> items = new();

    public IReadOnlyList<MarketCatalogItem> Items => items;

    public bool TryGetItem(ushort itemId, out MarketCatalogItem item)
    {
        foreach (MarketCatalogItem candidate in items)
        {
            if (candidate == null) continue;
            if (candidate.ItemId != itemId) continue;

            item = candidate;
            return true;
        }

        item = null;
        return false;
    }

    public int GetSellValue(ushort itemId)
    {
        if (TryGetItem(itemId, out MarketCatalogItem catalogItem) && catalogItem.CanSell)
            return catalogItem.SellValue;

        ItemDefinition def = ItemCatalog.Instance != null ? ItemCatalog.Instance.GetById(itemId) : null;
        return def != null ? Mathf.RoundToInt(def.CreditValue) : 0;
    }

    public bool CanSell(ushort itemId)
    {
        if (TryGetItem(itemId, out MarketCatalogItem catalogItem))
            return catalogItem.CanSell;

        return ItemCatalog.Instance != null && ItemCatalog.Instance.Contains(itemId);
    }

#if UNITY_EDITOR
    public void EditorAddOrReplace(MarketCatalogItem item)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].ItemId == item.ItemId)
            {
                items[i] = item;
                return;
            }
        }

        items.Add(item);
    }
#endif
}
