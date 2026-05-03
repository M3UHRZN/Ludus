using UnityEngine;

/// <summary>
/// GameEventBus ve ItemDecorator test scripti.
/// Boş bir GameObject'e ekleyip, Play'e basılır, Console'da test izlenir.
/// Assets/Scripts/Core/EventBusTest.cs
/// </summary>
public class EventBusTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== GameEventBus TEST BAŞLIYOR ===");

        // 1. EnemyDiedEvent testi
        GameEventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
        GameEventBus.Publish(new EnemyDiedEvent(42, new Vector3(1, 0, 3)));

        // 2. ItemPickedUpEvent testi
        GameEventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
        GameEventBus.Publish(new ItemPickedUpEvent("Demir Kutu", 3, 25f));

        // 3. PlayerDamagedEvent testi
        GameEventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        GameEventBus.Publish(new PlayerDamagedEvent(1, 20f, 80f));

        // 4. TimerEventTriggered testi
        GameEventBus.Subscribe<TimerEventTriggered>(OnTimerEvent);
        GameEventBus.Publish(new TimerEventTriggered(8f));   // 8s  IsUrgent: true

        // 5. PlayerDiedEvent testi
        GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        GameEventBus.Publish(new PlayerDiedEvent(1, new Vector3(0, 0, 5)));

        // 6. Unsubscribe testi
        GameEventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
        GameEventBus.Publish(new EnemyDiedEvent(99, Vector3.zero));
        // Bu publish için hiçbir şey yazdırılmamalı 

        // 7. Decorator testi
        TestDecorator();

        Debug.Log("=== TEST TAMAMLANDI ===");
    }

    // Event Handler'lar

    void OnEnemyDied(EnemyDiedEvent e)
        => Debug.Log($"[✓] EnemyDiedEvent → ID: {e.EnemyId}, Pozisyon: {e.Position}");

    void OnItemPickedUp(ItemPickedUpEvent e)
        => Debug.Log($"[✓] ItemPickedUpEvent → {e.ItemName}, Ağırlık: {e.Weight}, Kredi: {e.CreditValue}");

    void OnPlayerDamaged(PlayerDamagedEvent e)
        => Debug.Log($"[✓] PlayerDamagedEvent → Oyuncu {e.PlayerId}, Hasar: {e.Damage}, Kalan HP: {e.RemainingHP}");

    void OnTimerEvent(TimerEventTriggered e)
        => Debug.Log($"[✓] TimerEventTriggered → Kalan: {e.RemainingSeconds}s, Acil: {e.IsUrgent}");

    void OnPlayerDied(PlayerDiedEvent e)
        => Debug.Log($"[✓] PlayerDiedEvent → Oyuncu {e.PlayerId}, Konum: {e.DeathPosition}");

    // Decorator Testi

    void TestDecorator()
    {
        Debug.Log("--- Decorator Testi ---");

        // BaseItem stub ile test
        IItem baseItem = new TestBaseItem("Demir Kutu", ItemSize.Medium, 3, 25f);
        Debug.Log($"[✓] BaseItem → {baseItem.ItemName}, Ağırlık: {baseItem.Weight}");

        // FlashbangDecorator ile sar
        IItem flashItem = new FlashbangDecorator(baseItem, flashRadius: 5f, blindDuration: 3f);
        Debug.Log($"[✓] FlashbangDecorator → {flashItem.ItemName}, Ağırlık: {flashItem.Weight}");

        // OnPickup çağır (inventory null olduğu için origin (0,0,0) olacak)
        flashItem.OnPickup(null);

        Debug.Log("[✓] Decorator zinciri çalışıyor!");
    }
}

// Test için minimal BaseItem

public class TestBaseItem : IItem
{
    public string ItemName { get; }
    public ItemSize Size { get; }
    public int Weight { get; }
    public float CreditValue { get; }

    public TestBaseItem(string name, ItemSize size, int weight, float creditValue)
    {
        ItemName = name;
        Size = size;
        Weight = weight;
        CreditValue = creditValue;
    }

    public void OnPickup(PlayerInventory inventory)
        => Debug.Log($"[TestBaseItem] {ItemName} alındı.");

    public void OnDrop()
        => Debug.Log($"[TestBaseItem] {ItemName} bırakıldı.");
}