using UnityEngine;

public class AttackBehavior : IEnemyBehavior
{
    private const float AttackRange = 1.8f;     // bu mesafeden uzakta = Chase
    private const float HitRange = 2.2f;        // hasar aralii (biraz tampon)
    private const float DamagePerHit = 15f;
    private const float HitCooldown = 1.2f;

    private float _cooldown;

    public void Enter(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = true;

        _cooldown = 0.4f; // ilk vurus icin kucuk gecikme (animasyon yerini tutsun)
        Debug.Log("[AttackBehavior] Saldiri menziline girildi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;

        // Oyuncuya don
        Vector3 toPlayer = enemy.PlayerTransform.position - enemy.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer);
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, look, Time.deltaTime * 8f);
        }

        float dist = toPlayer.magnitude;

        // Oyuncu uzaklasti -> tekrar kovala
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

        // Oyuncuda IDamageable var mi?
        var dmg = enemy.PlayerTransform.GetComponent<IDamageable>()
                  ?? enemy.PlayerTransform.GetComponentInParent<IDamageable>();

        if (dmg == null || !dmg.IsAlive)
            return;

        Vector3 hitPoint = enemy.PlayerTransform.position;
        // Dusman server'da calistigi icin attacker olarak server client id (0) gecilebilir
        dmg.TakeDamage(DamagePerHit, hitPoint, 0UL);

        Debug.Log($"[AttackBehavior] Vurus: {DamagePerHit} hasar.");
    }
}
