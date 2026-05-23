// Assets/Scripts/Items/BaseItem.cs
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BaseItem : MonoBehaviour, IItem
{
    [Header("Item Config")]
    [SerializeField] private string   _itemName    = "Unknown Item";
    [SerializeField] private ItemSize _size        = ItemSize.Small;
    [SerializeField] private float    _creditValue = 10f;

    // --- UI ÝÇÝN EKLENEN RESÝM ---
    //[SerializeField] private Sprite _itemIcon;
    //public Sprite ItemIcon => _itemIcon;

    public string   ItemName    => _itemName;
    public ItemSize Size        => _size;
    public int      Weight      => _size switch {
        ItemSize.Small  => 1,
        ItemSize.Medium => 3,
        ItemSize.Large  => 6,
        _               => 1
    };
    public float CreditValue => _creditValue;

    public virtual void OnPickup(PlayerInventory inventory)
    {
        GetComponent<Rigidbody>().isKinematic = true;
        gameObject.SetActive(false);
        GameEventBus.Publish(new ItemPickedUpEvent(ItemName, Weight, CreditValue));
        Debug.Log($"[Item] {ItemName} picked up | Weight: {Weight} | Value: {CreditValue}cr");
    }

    public virtual void OnDrop()
    {
        GetComponent<Rigidbody>().isKinematic = false;
        gameObject.SetActive(true);
        Debug.Log($"[Item] {ItemName} dropped.");
    }
}