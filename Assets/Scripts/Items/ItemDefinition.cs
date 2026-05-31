using UnityEngine;
using Ludus.UsableItems.Core;

/// <summary>
/// Bir item'ın tek veri kaynağı (id, ad, boyut, değer, icon, world prefab, market).
/// Davranış world prefab'ındaki BaseItem-türevli bileşendedir; bu SADECE veridir.
/// </summary>
[CreateAssetMenu(fileName = "ItemDefinition", menuName = "Ludus/Item Definition")]
public class ItemDefinition : ScriptableObject, IItemRecord
{
    [SerializeField] private ushort id;
    [SerializeField] private string displayName = "Unknown Item";
    [SerializeField] private ItemSize size = ItemSize.Small;
    [SerializeField] private float creditValue = 10f;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject worldPrefab;

    [Header("Market")]
    [SerializeField] private bool isBuyable;
    [SerializeField] private int marketPrice;
    [SerializeField] private bool isSellable = true;
    [SerializeField] private int sellPrice;

    public ushort Id => id;
    public string DisplayName => displayName;
    public ItemSize Size => size;
    public float CreditValue => creditValue;
    public Sprite Icon => icon;
    public GameObject WorldPrefab => worldPrefab;
    public bool IsBuyable => isBuyable;
    public int MarketPrice => marketPrice;
    public bool IsSellable => isSellable;
    public int SellPrice => sellPrice;

    // BaseItem'ın orijinal Small=1 / Medium=3 / Large=6 kuralı.
    public int Weight => size switch
    {
        ItemSize.Small => 1,
        ItemSize.Medium => 3,
        ItemSize.Large => 6,
        _ => 1
    };
}
