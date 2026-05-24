using UnityEngine;

/// <summary>
/// STUB — Placeholder until Metin's real PlayerHealth system is merged.
/// Delete or replace this file when Metin's code is integrated.
/// Assets/Scripts/Player/PlayerHealth.cs
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHP = 100f;
    private float _currentHP;

    private void Awake()
    {
        _currentHP = maxHP;
    }

    public void TakeDamage(float amount)
    {
        _currentHP -= amount;
        _currentHP = Mathf.Clamp(_currentHP, 0f, maxHP);

        Debug.Log($"[PlayerHealth] Damage taken: {amount:F1}, Remaining HP: {_currentHP:F1}");

        // Notify via GameEventBus
        GameEventBus.Publish(new PlayerDamagedEvent(0, amount, _currentHP));

        if (_currentHP <= 0f)
        {
            Debug.Log("[PlayerHealth] Player died!");
            GameEventBus.Publish(new PlayerDiedEvent(0, transform.position));
        }
    }

    public float GetCurrentHP() => _currentHP;
    public float GetMaxHP() => maxHP;
}