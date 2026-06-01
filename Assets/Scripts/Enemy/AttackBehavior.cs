using UnityEngine;

public class AttackBehavior : IEnemyBehavior
{
    private const float AttackRange = 1.8f;
    private const float HitRange = 2.2f;
    private const float DamagePerHit = 50f;
    private const float HitCooldown = 1.2f;

    // Yakin vurus tepkisi (Type B / yakin dovus)
    private const float KnockbackForce = 55f;
    private const float PlayerStunDuration = 1.5f;
    private const float EnemySlowMultiplier = 0.5f;
    private const float EnemySlowDuration = 10f;

    private float _cooldown;

    public void Enter(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = true;

        _cooldown = 0.4f;
        Debug.Log("[AttackBehavior] Saldiri menziline girildi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        Vector3 toPlayer = enemy.PlayerTransform.position - enemy.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer);
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, look, Time.deltaTime * 8f);
        }

        float dist = toPlayer.magnitude;

        if (dist > AttackRange + 0.6f)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f && dist <= HitRange)
        {
            TryHit(enemy);
            _cooldown = HitCooldown;
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = false;
    }

    private static void TryHit(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;

        var dmg = enemy.PlayerTransform.GetComponent<IDamageable>()
                  ?? enemy.PlayerTransform.GetComponentInParent<IDamageable>();

        if (dmg == null || !dmg.IsAlive)
            return;

        Vector3 hitPoint = enemy.PlayerTransform.position;
        dmg.TakeDamage(DamagePerHit, hitPoint, 0UL);

        // PlayerTransform'u tekrar okumayalim, oldurucu vurusta null donebilir
        var knock = dmg as IKnockbackable;
        knock?.ApplyKnockback(enemy.transform.position, KnockbackForce, PlayerStunDuration);

        enemy.ApplySelfSlow(EnemySlowMultiplier, EnemySlowDuration);

        Debug.Log($"[AttackBehavior] Vurus: {DamagePerHit} hasar + knockback; dusman {EnemySlowDuration:F0}sn yavasladi.");
    }
}
