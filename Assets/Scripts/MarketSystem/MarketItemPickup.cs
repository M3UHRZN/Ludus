using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to TestPlayer. Picks up spawned market delivery items on F key.
/// Raycasts forward, finds MarketDeliveryItem tag or any Rigidbody within range.
/// Test-only — no Netcode required.
/// </summary>
public class MarketItemPickup : MonoBehaviour
{
    [Tooltip("Max pickup distance in metres")]
    public float pickupDistance = 2.5f;

    [Tooltip("Item ID to add to inventory on pickup (100 = Flashbang)")]
    public ushort pickupItemId = 100;

    [Tooltip("Optional inventory to add item into")]
    public PlayerInventory inventory;

    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (!Keyboard.current.fKey.wasPressedThisFrame) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Ray ray = new Ray(_cam.transform.position, _cam.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, pickupDistance))
        {
            Debug.Log("[MarketItemPickup] Nothing in pickup range.");
            return;
        }

        // Check if it's a delivery item (has Rigidbody, not a wall/floor)
        Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.Log($"[MarketItemPickup] Hit '{hit.collider.name}' — not a pickup item.");
            return;
        }

        string itemName = hit.collider.name;
        Destroy(hit.collider.gameObject);

        if (inventory != null)
        {
            bool added = inventory.TryAddItem(pickupItemId);
            Debug.Log(added
                ? $"[MarketItemPickup] Picked up '{itemName}' → added to inventory as ID {pickupItemId}."
                : $"[MarketItemPickup] Picked up '{itemName}' but inventory is full.");
        }
        else
        {
            Debug.Log($"[MarketItemPickup] Picked up '{itemName}' (no inventory assigned).");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (Camera.main == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * pickupDistance);
    }
}
