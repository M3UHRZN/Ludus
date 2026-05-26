using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tests FlashbangDecorator + StunDecorator chain on a real EnemyController.
/// Attach to any GameObject in the scene.
/// Press F to trigger Flashbang, G to trigger Stun, H for chain.
/// Remove after testing.
/// Assets/Scripts/ChainDecoratorTest.cs
/// </summary>
public class ChainDecoratorTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private float _flashRadius = 5f;
    [SerializeField] private float _blindDuration = 3f;
    [SerializeField] private float _stunRadius = 4f;
    [SerializeField] private float _stunDuration = 2f;

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.fKey.wasPressedThisFrame) TestFlashbang();
        if (Keyboard.current.gKey.wasPressedThisFrame) TestStun();
        if (Keyboard.current.hKey.wasPressedThisFrame) TestChain();
    }

    private void TestFlashbang()
    {
        Debug.Log("[ChainDecoratorTest] Testing FlashbangDecorator...");
        IItem baseItem = new ChainTestItem("Test Box", ItemSize.Small, 1, 10f);
        IItem flashItem = new FlashbangDecorator(baseItem, _flashRadius, _blindDuration);
        flashItem.OnPickup(null);
        Debug.Log("[ChainDecoratorTest] FlashbangDecorator triggered.");
    }

    private void TestStun()
    {
        Debug.Log("[ChainDecoratorTest] Testing StunDecorator...");
        IItem baseItem = new ChainTestItem("Test Box", ItemSize.Small, 1, 10f);
        IItem stunItem = new StunDecorator(baseItem, _stunRadius, _stunDuration);
        stunItem.OnPickup(null);
        Debug.Log("[ChainDecoratorTest] StunDecorator triggered.");
    }

    private void TestChain()
    {
        Debug.Log("[ChainDecoratorTest] Testing chain: FlashbangDecorator(StunDecorator(base))...");
        IItem baseItem = new ChainTestItem("Mystery Box", ItemSize.Small, 1, 10f);
        IItem stunItem = new StunDecorator(baseItem, _stunRadius, _stunDuration);
        IItem comboItem = new FlashbangDecorator(stunItem, _flashRadius, _blindDuration);
        comboItem.OnPickup(null);
        Debug.Log("[ChainDecoratorTest] Chain triggered — both Flashbang and Stun should apply.");
    }
}

public class ChainTestItem : IItem
{
    public string ItemName { get; }
    public ItemSize Size { get; }
    public int Weight { get; }
    public float CreditValue { get; }

    public ChainTestItem(string name, ItemSize size, int weight, float creditValue)
    {
        ItemName = name;
        Size = size;
        Weight = weight;
        CreditValue = creditValue;
    }

    public void OnPickup(PlayerInventory inventory)
        => Debug.Log($"[ChainTestItem] {ItemName} picked up.");

    public void OnDrop()
        => Debug.Log($"[ChainTestItem] {ItemName} dropped.");
}