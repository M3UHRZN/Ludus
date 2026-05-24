// Assets/Scripts/Items/WeightSystem.cs
using UnityEngine;

/// <summary>
/// Calculates movement speed based on carried item weight.
/// Attach to the same GameObject as PlayerInventory.
/// </summary>
public class WeightSystem : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float _baseSpeed = 5f;
    [SerializeField] private float _maxWeight = 10f;

    /// <summary>
    /// Formula: baseSpeed * (1 - totalWeight / maxWeight)
    /// Never drops below 20% of base speed.
    /// </summary>
    public float GetModifiedSpeed(int totalWeight)
    {
        float ratio    = Mathf.Clamp01((float)totalWeight / _maxWeight);
        float modified = _baseSpeed * (1f - ratio);
        return Mathf.Max(modified, _baseSpeed * 0.2f);
    }

    public int CalculateTotalWeight(PlayerInventory inventory)
    {
        int total = 0;
        foreach (ushort id in inventory.Slots)
            total += ItemRegistry.Instance != null ? ItemRegistry.Instance.GetWeight(id) : 0;
        return total;
    }
    
}