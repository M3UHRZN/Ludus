// Assets/Scripts/Items/ItemPickup.cs
using System;
using UnityEngine;

[RequireComponent(typeof(BaseItem))]
public class ItemPickup : MonoBehaviour
{
    [SerializeField] private float _interactRange = 2f;
    private BaseItem _item;

    private void Awake() => _item = GetComponent<BaseItem>();

    // Will be replaced by PlayerInventory.Update() in Sprint 1
    // For now, Space key triggers pickup for testing
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _item.OnPickup(null);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactRange);
    }
}
