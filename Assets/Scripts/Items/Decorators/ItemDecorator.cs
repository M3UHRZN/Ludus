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
    // RequestFlashbang was removed in the NetworkUsableItem refactor (Task 5).
    // FlashbangDecorator's activate-on-pickup path is obsolete; flashbang
    // behaviour now lives in ThrownFlashbang.ServerActivate.
    public FlashbangDecorator(IItem inner, float flashRadius = 5f, float blindDuration = 3f)
        : base(inner) { }
}