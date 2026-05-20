using UnityEngine;

/// <summary>
/// Uzaktan saldiri davranisi (Type A / robot). Belirli menzilde durup oyuncuya
/// raycast (hitscan) ile "kursun" atar, cooldown'li hasar verir. Oyuncu menzil
/// disina cikarsa veya gorus kaybolursa Chase'e doner.
///
/// Strategy pattern'in 6. concrete davranisi. EnemyController.CreateAttackBehavior()
/// _useRangedAttack flag'ine gore bu davranisi veya yakin dovus AttackBehavior'i secer.
/// </summary>
public class RangedAttackBehavior : IEnemyBehavior
{
    private const float FireCooldown   = 1.5f;   // iki atis arasi sure
    private const float DamagePerShot  = 10f;
    private const float DisengageRange = 16f;     // bu mesafeden uzaklasirsa Chase'e don
    private const float AimTurnSpeed   = 8f;

    private float _cooldown;

    public void Enter(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = true;

        _cooldown = 0.6f; // ilk atis icin kisa nisan gecikmesi
        Debug.Log("[RangedAttackBehavior] Atis pozisyonu alindi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;

        // Oyuncuya nisan al (govdeyi dondur)
        Vector3 toPlayer = enemy.PlayerTransform.position - enemy.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer);
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, look, Time.deltaTime * AimTurnSpeed);
        }

        float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);

        // Gorus kaybedildi veya menzil disi -> tekrar kovala
        if (!enemy.CanSeePlayer() || dist > DisengageRange)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f)
        {
            Fire(enemy);
            _cooldown = FireCooldown;
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = false;
    }

    private static void Fire(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;

        Vector3 origin = enemy.transform.position + Vector3.up * 1.5f;
        Vector3 target = enemy.PlayerTransform.position + Vector3.up;
        Vector3 dir = (target - origin).normalized;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, DisengageRange))
        {
            var dmg = hit.transform.GetComponent<IDamageable>()
                      ?? hit.transform.GetComponentInParent<IDamageable>();

            if (dmg != null && dmg.IsAlive)
            {
                // Enemy server'da kostugu icin attacker olarak server client id (0)
                dmg.TakeDamage(DamagePerShot, hit.point, 0UL);
                Debug.Log($"[RangedAttackBehavior] Isabet! {DamagePerShot} hasar.");
            }
            else
            {
                Debug.Log("[RangedAttackBehavior] Ates edildi (isabet yok / engel).");
            }
        }

#if UNITY_EDITOR
        // Editor'da atis izini goster (gorsel mermi/VFX Sprint 3'te eklenecek)
        Debug.DrawLine(origin, origin + dir * DisengageRange, Color.red, 0.15f);
#endif
    }
}
