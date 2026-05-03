using UnityEngine;

/// <summary>
/// Tüm item'larýn uygulamasý gereken temel interface.
/// Yasin'in final versiyonu gelince bu dosya güncellenir.
/// </summary>
public interface IItem
{
    string ItemName { get; }
    ItemSize Size { get; }   // Small / Medium / Large
    int Weight { get; }   // Small=1, Medium=3, Large=6
    float CreditValue { get; }

    void OnPickup(PlayerInventory inventory);
    void OnDrop();
}

/// <summary>
/// Eþya boyut kategorileri.
/// </summary>
public enum ItemSize
{
    Small,
    Medium,
    Large
}