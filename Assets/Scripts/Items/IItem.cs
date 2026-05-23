public enum ItemSize { Small, Medium, Large }

public interface IItem
{
    string   ItemName    { get; }
    ItemSize Size        { get; }
    int      Weight      { get; }
    float    CreditValue { get; }

    // --- UI ÝÇÝN EKLENEN YENÝ SATIR ---
    //UnityEngine.Sprite ItemIcon { get; }

    void OnPickup(PlayerInventory inventory);
    void OnDrop();
}