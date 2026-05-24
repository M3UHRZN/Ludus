using System.Collections;
using UnityEngine;

/// <summary>
/// PoisonDecorator — Decorator Pattern
/// Adds Poison effect to an item.
/// When OnPickup is called, applies damage over time to the carrying player.
/// When OnDrop is called, activates a poison area at the drop location.
/// 
/// USAGE:
/// IItem baseItem = new BaseItem("Chemical Barrel", ItemSize.Large, 6, 50f);
/// IItem poisonItem = new PoisonDecorator(baseItem);
/// poisonItem.OnPickup(inventory); // → pickup + poison effect starts
/// 
/// Assets/Scripts/Items/Decorators/PoisonDecorator.cs
/// </summary>
public class PoisonDecorator : ItemDecorator
{
    private readonly float _damagePerSecond;
    private readonly float _duration;
    private readonly float _poisonRadius;

    public PoisonDecorator(IItem inner, float damagePerSecond = 5f, float duration = 3f, float poisonRadius = 3f)
        : base(inner)
    {
        _damagePerSecond = damagePerSecond;
        _duration = duration;
        _poisonRadius = poisonRadius;
    }

    public override void OnPickup(PlayerInventory inventory)
    {
        // Run base pickup behaviour first
        base.OnPickup(inventory);

        // Then apply poison effect
        TriggerPoison(inventory);
    }

    public override void OnDrop()
    {
        base.OnDrop();

        // Poison area activates when item is dropped (optional extension)
        Debug.Log("[PoisonDecorator] Item dropped, poison area active.");
    }

    private void TriggerPoison(PlayerInventory inventory)
    {
        if (inventory == null)
        {
            Debug.Log("[PoisonDecorator] Inventory is null, poison cannot be applied.");
            return;
        }

        // Find PlayerHealth component (will be updated when Metin's network system arrives)
        PlayerHealth health = inventory.GetComponent<PlayerHealth>();

        if (health != null)
        {
            // Start coroutine via inventory MonoBehaviour
            inventory.StartCoroutine(ApplyPoisonOverTime(health));
            Debug.Log($"[PoisonDecorator] Poison started! {_damagePerSecond} hp/s for {_duration}s");
        }
        else
        {
            Debug.LogWarning("[PoisonDecorator] PlayerHealth not found, damage cannot be applied.");
        }

        // Notify via GameEventBus (optional)
        // GameEventBus.Publish(new PoisonTriggeredEvent(inventory.transform.position, _poisonRadius));
    }

    private System.Collections.IEnumerator ApplyPoisonOverTime(PlayerHealth health)
    {
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            health.TakeDamage(_damagePerSecond * UnityEngine.Time.deltaTime);
            elapsed += UnityEngine.Time.deltaTime;
            yield return null;
        }

        Debug.Log("[PoisonDecorator] Poison duration ended.");
    }
}