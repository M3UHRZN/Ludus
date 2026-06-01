using UnityEngine;

// Geri itilebilen ve stunlanabilen nesne. AttackBehavior temas aninda cagirir, PlayerStateMachine uygular.
public interface IKnockbackable
{
    void ApplyKnockback(Vector3 sourcePosition, float force, float stunDuration);
}
