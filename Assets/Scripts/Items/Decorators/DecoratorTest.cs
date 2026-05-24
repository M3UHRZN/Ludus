using UnityEngine;

/// <summary>
/// StunDecorator and PoisonDecorator test script.
/// Attach to an empty GameObject, press Play, check Console.
/// Remove from scene after testing — do not leave in production.
/// Assets/Scripts/Core/DecoratorTest.cs
/// </summary>
public class DecoratorTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== DECORATOR TEST STARTING ===");

        TestStunDecorator();
        TestPoisonDecorator();
        TestDecoratorChain();

        Debug.Log("=== DECORATOR TEST COMPLETE ===");
    }

    // ── StunDecorator Test ────────────────────────────────────────────────────
    void TestStunDecorator()
    {
        Debug.Log("--- StunDecorator Test ---");

        IItem baseItem = new TestItem("Metal Box", ItemSize.Medium, 3, 25f);
        IItem stunItem = new StunDecorator(baseItem, stunRadius: 4f, stunDuration: 2f);

        // Verify decorator does not change base item properties
        Debug.Assert(stunItem.ItemName == "Metal Box", "[✓] StunDecorator: ItemName preserved");
        Debug.Assert(stunItem.Weight == 3, "[✓] StunDecorator: Weight preserved");
        Debug.Assert(stunItem.CreditValue == 25f, "[✓] StunDecorator: CreditValue preserved");

        Debug.Log($"[✓] StunDecorator → {stunItem.ItemName}, Weight: {stunItem.Weight}");

        // OnPickup: base pickup runs, then stun triggers (no enemies in scene, so OverlapSphere returns empty)
        stunItem.OnPickup(null);

        Debug.Log("[✓] StunDecorator OnPickup completed without errors");
    }

    // ── PoisonDecorator Test ──────────────────────────────────────────────────
    void TestPoisonDecorator()
    {
        Debug.Log("--- PoisonDecorator Test ---");

        IItem baseItem = new TestItem("Chemical Barrel", ItemSize.Large, 6, 50f);
        IItem poisonItem = new PoisonDecorator(baseItem, damagePerSecond: 5f, duration: 3f);

        // Verify decorator does not change base item properties
        Debug.Assert(poisonItem.ItemName == "Chemical Barrel", "[✓] PoisonDecorator: ItemName preserved");
        Debug.Assert(poisonItem.Weight == 6, "[✓] PoisonDecorator: Weight preserved");

        Debug.Log($"[✓] PoisonDecorator → {poisonItem.ItemName}, Weight: {poisonItem.Weight}");

        // OnPickup with null inventory: should warn but not crash
        poisonItem.OnPickup(null);

        // OnDrop test
        poisonItem.OnDrop();

        Debug.Log("[✓] PoisonDecorator OnPickup and OnDrop completed without errors");
    }

    // ── Decorator Chain Test ──────────────────────────────────────────────────
    void TestDecoratorChain()
    {
        Debug.Log("--- Decorator Chain Test ---");

        // Chain: FlashbangDecorator(StunDecorator(baseItem))
        IItem baseItem = new TestItem("Mystery Box", ItemSize.Small, 1, 10f);
        IItem stunItem = new StunDecorator(baseItem, stunRadius: 4f, stunDuration: 2f);
        IItem comboItem = new FlashbangDecorator(stunItem, flashRadius: 5f, blindDuration: 3f);

        // Properties must still match base item
        Debug.Assert(comboItem.ItemName == "Mystery Box", "[✓] Chain: ItemName preserved through chain");
        Debug.Assert(comboItem.Weight == 1, "[✓] Chain: Weight preserved through chain");

        Debug.Log($"[✓] Chain → {comboItem.ItemName}, Weight: {comboItem.Weight}");

        // OnPickup: base → stun → flashbang (all three run)
        comboItem.OnPickup(null);

        Debug.Log("[✓] Decorator chain FlashbangDecorator(StunDecorator(base)) works correctly!");
    }
}

// ── Minimal test item (no dependency on Yasin's BaseItem) ────────────────────
/// <summary>
/// Minimal IItem implementation for testing only.
/// Delete when Yasin's BaseItem is merged.
/// </summary>
public class TestItem : IItem
{
    public string ItemName { get; }
    public ItemSize Size { get; }
    public int Weight { get; }
    public float CreditValue { get; }

    public TestItem(string name, ItemSize size, int weight, float creditValue)
    {
        ItemName = name;
        Size = size;
        Weight = weight;
        CreditValue = creditValue;
    }

    public void OnPickup(PlayerInventory inventory)
        => Debug.Log($"[TestItem] {ItemName} picked up.");

    public void OnDrop()
        => Debug.Log($"[TestItem] {ItemName} dropped.");
}