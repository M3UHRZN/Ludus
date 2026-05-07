using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint, ulong attackerClientId);
    bool IsAlive { get; }
    float CurrentHealth { get; }
}
