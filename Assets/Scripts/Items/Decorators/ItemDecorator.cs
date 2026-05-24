using UnityEngine;

// ItemDecorator.cs - Decorator Pattern Temeli
// Assets/Scripts/Items/Decorators/ItemDecorator.cs

/// <summary>
/// Tüm Decorator'ların türeyeceği abstract sınıf.
/// IItem'ı implement eder ve sarmaladığı (_inner) item'a delege eder.
/// Decorator'lar sadece değiştirmek istedikleri metodu override eder.
/// </summary>
public abstract class ItemDecorator : IItem
{
    protected IItem _inner;

    protected ItemDecorator(IItem inner)
    {
        _inner = inner;
    }

    public virtual string ItemName => _inner.ItemName;
    public virtual ItemSize Size => _inner.Size;
    public virtual int Weight => _inner.Weight;
    public virtual float CreditValue => _inner.CreditValue;

    public virtual void OnPickup(PlayerInventory inventory) => _inner.OnPickup(inventory);
    public virtual void OnDrop() => _inner.OnDrop();
}


// FlashbangDecorator

/// <summary>
/// Eşyaya Flashbang özelliği ekler.
/// OnPickup çağrıldığında yakındaki düşmanları geçici olarak körleştirir
/// ve PatrolBehavior'a geri döndürür.
///
/// KULLANIM:
/// IItem kutu = new BaseItem("Kutu", ItemSize.Small, 1, 10f);
/// IItem flashKutu = new FlashbangDecorator(kutu);
/// flashKutu.OnPickup(inventory); // hem normal pickup hem flashbang efekti
/// </summary>
public class FlashbangDecorator : ItemDecorator
{
    [Tooltip("Kaç birim içindeki düşmanlar etkilensin")]
    private float _flashRadius;

    [Tooltip("Körlük süresi (saniye)")]
    private float _blindDuration;

    public FlashbangDecorator(IItem inner, float flashRadius = 5f, float blindDuration = 3f)
        : base(inner)
    {
        _flashRadius = flashRadius;
        _blindDuration = blindDuration;
    }

    public override void OnPickup(PlayerInventory inventory)
    {
        // Önce normal pickup davranışı
        base.OnPickup(inventory);

        // Sonra flashbang efekti
        TriggerFlashbang(inventory);
    }

    private void TriggerFlashbang(PlayerInventory inventory)
    {
        if (inventory == null) return;

        // Server-authoritative: client sadece request atar; gercek OverlapSphere ve SetBlinded server'da olur.
        Vector3 origin = inventory.transform.position;
        inventory.RequestFlashbang(origin, _flashRadius, _blindDuration);

        Debug.Log($"[FlashbangDecorator] Flashbang istegi gonderildi. Merkez: {origin}, Yaricap: {_flashRadius}");
    }
}