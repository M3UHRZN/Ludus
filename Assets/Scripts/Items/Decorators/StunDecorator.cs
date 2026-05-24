using UnityEngine;

/// <summary>
/// StunDecorator — Decorator Pattern
/// Adds Stun effect to an item.
/// When OnPickup is called, temporarily freezes nearby enemies.
/// 
/// USAGE:
/// IItem baseItem = new BaseItem("Box", ItemSize.Small, 1, 10f);
/// IItem stunItem = new StunDecorator(baseItem);
/// stunItem.OnPickup(inventory); // → normal pickup + stun effect
/// 
/// CHAIN:
/// IItem combo = new FlashbangDecorator(new StunDecorator(baseItem));
/// 
/// Assets/Scripts/Items/Decorators/StunDecorator.cs
/// </summary>
public class StunDecorator : ItemDecorator
{
    private readonly float _stunRadius;
    private readonly float _stunDuration;

    public StunDecorator(IItem inner, float stunRadius = 4f, float stunDuration = 2f)
        : base(inner)
    {
        _stunRadius = stunRadius;
        _stunDuration = stunDuration;
    }

    public override void OnPickup(PlayerInventory inventory)
    {
        // Run base pickup behaviour first
        base.OnPickup(inventory);

        // Then apply stun effect
        TriggerStun(inventory);
    }

    private void TriggerStun(PlayerInventory inventory)
    {
        Vector3 origin = inventory != null
            ? inventory.transform.position
            : Vector3.zero;

        Collider[] hits = Physics.OverlapSphere(origin, _stunRadius);

        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponent<EnemyController>();
            if (enemy == null) continue;

            enemy.SetStunned(true, _stunDuration);
            Debug.Log($"[StunDecorator] {hit.name} stunned ({_stunDuration}s)");
        }

        // Notify via GameEventBus (optional)
        // GameEventBus.Publish(new StunTriggeredEvent(origin, _stunRadius));

        Debug.Log($"[StunDecorator] Stun triggered! Origin: {origin}, Radius: {_stunRadius}");
    }
}