using UnityEngine;

public class AttackBehavior : IEnemyBehavior
{
    private const float AttackRange = 1.8f;     // bu mesafeden uzakta = Chase
    private const float HitRange = 2.2f;        // hasar aralii (biraz tampon)
    private const float DamagePerHit = 50f;   // guclu yakin vurus (knockback'li slam)
    private const float HitCooldown = 1.2f;

    // Yakin vurus tepkisi (Type B / yakin dovus dusmani)
    private const float KnockbackForce      = 55f;   // geri firlatma hizi (m/s) — duvara yapistiracak kadar guclu
    private const float PlayerStunDuration  = 1.5f;  // oyuncunun hareketsiz kalma suresi (sn)
    private const float EnemySlowMultiplier = 0.5f;  // vurustan sonra dusmanin hiz carpani
    private const float EnemySlowDuration   = 10f;   // dusmanin yavas kalma suresi (sn)

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
        // Hedef yok / oldu — Chase'e devret; Chase tum-oyuncular-olu durumunda
        // kisa grace ile default davranisa doner, dusman cesedin ustunde takilmaz.
        if (enemy.PlayerTransform == null)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

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

        // Yakin vurus tepkisi: oyuncuyu uzaga firlat + kisa sure sersemlet.
        // dmg ile AYNI component kullanilir (PlayerStateMachine hem IDamageable hem
        // IKnockbackable). PlayerTransform'u TEKRAR OKUMA: oldurucu vurus sonrasi oyuncu
        // olur, PlayerTransform null doner ve NullReferenceException olur.
        var knock = dmg as IKnockbackable;
        knock?.ApplyKnockback(enemy.transform.position, KnockbackForce, PlayerStunDuration);

        // Guclu vurusun bedeli: dusman bir sure yavaslar -> oyuncuya kacis penceresi.
        enemy.ApplySelfSlow(EnemySlowMultiplier, EnemySlowDuration);

        Debug.Log($"[AttackBehavior] Vurus: {DamagePerHit} hasar + knockback; dusman {EnemySlowDuration:F0}sn yavasladi.");
    }
}
