using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Uzaktan saldiri davranisi (Type A / robot). Belirli menzilde durup oyuncuya
/// gorunur mermi (EnemyProjectile) atar; mermi prefab yoksa gorunmez hitscan'e
/// duser. Sarjor (magazine) bitince reload yapar — reload sirasinda ates edemez,
/// bu oyuncuya saldiri/kacis penceresi acar. Oyuncu menzil disina cikarsa veya
/// gorus kaybolursa Chase'e doner.
///
/// Strategy pattern'in 6. concrete davranisi.
/// </summary>
public class RangedAttackBehavior : IEnemyBehavior
{
    private const float DamagePerShot  = 7f;    // azaltildi (oyuncu uzerindeki baskiyi dusur)
    private const float DisengageRange = 16f;   // bu mesafeden uzaklasirsa Chase'e don
    private const float AimTurnSpeed   = 8f;
    private const float LostSightGrace = 0.8f;  // gorus anlik kaybolursa hemen Chase'e gecme toleransi

    private const float FireCooldown   = 1.5f;  // sarjor icinde iki atis arasi (yavaslatildi)
    private const int   MagazineSize   = 5;     // bir sarjordeki atis sayisi
    private const float ReloadTime     = 2.5f;  // reload suresi (atessiz pencere)

    private float _cooldown;
    private int   _shotsLeft;
    private bool  _reloading;
    private float _reloadTimer;
    private float _lostSightGrace;

    public void Enter(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = true;

        _cooldown = 0.6f;
        _shotsLeft = MagazineSize;
        _reloading = false;
        _lostSightGrace = LostSightGrace;
        Debug.Log("[RangedAttackBehavior] Atis pozisyonu alindi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;

        // Oyuncuya nisan al (gorsel olarak govdeyi dondur)
        FacePlayer(enemy);

        float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);

        // Reload sirasinda: ates yok ama menzil/gorus kontrolu surer
        if (_reloading)
        {
            _reloadTimer -= Time.deltaTime;
            if (_reloadTimer <= 0f)
            {
                _reloading = false;
                _shotsLeft = MagazineSize;
                Debug.Log("[RangedAttackBehavior] Reload tamamlandi.");
            }
            return;
        }

        // Menzil disina cikti -> tekrar kovala
        if (dist > DisengageRange)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Gorus kaybi: kisa bir tolerans tani (anlik kaybolmada Chase'e gecip flapping yapma).
        // Tolerans dolarsa kovalamaya don; tolerans icinde ates etme ama davranis da degistirme.
        if (!enemy.CanSeePlayer())
        {
            _lostSightGrace -= Time.deltaTime;
            if (_lostSightGrace <= 0f)
            {
                enemy.SwitchBehavior(new ChaseBehavior());
                return;
            }
            return;
        }
        _lostSightGrace = LostSightGrace;   // gorunce toleransi tazele

        _cooldown -= Time.deltaTime;
        if (_cooldown <= 0f && _shotsLeft > 0)
        {
            Fire(enemy);
            _shotsLeft--;
            _cooldown = FireCooldown;

            if (_shotsLeft <= 0)
            {
                _reloading = true;
                _reloadTimer = ReloadTime;
                Debug.Log("[RangedAttackBehavior] Sarjor bitti, reload basliyor.");
            }
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = false;
    }

    private static void FacePlayer(EnemyController enemy)
    {
        Vector3 toPlayer = enemy.PlayerTransform.position - enemy.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer);
            enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, look, Time.deltaTime * AimTurnSpeed);
        }
    }

    private static void Fire(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;

        Vector3 origin = enemy.FirePoint != null
            ? enemy.FirePoint.position
            : enemy.transform.position + Vector3.up * 1.5f;
        Vector3 target = enemy.PlayerTransform.position + Vector3.up;
        Vector3 dir = (target - origin).normalized;

        // Namlu atesi efekti (opsiyonel)
        if (enemy.MuzzleFlashPrefab != null)
        {
            var flash = Object.Instantiate(enemy.MuzzleFlashPrefab, origin, Quaternion.LookRotation(dir));
            Object.Destroy(flash, 0.15f);
        }

        // Mermi prefab atanmissa gorunur mermi spawn et (hasar mermi carptiginda verilir)
        if (enemy.ProjectilePrefab != null)
        {
            var proj = Object.Instantiate(enemy.ProjectilePrefab, origin, Quaternion.LookRotation(dir));
            var netObj = proj.GetComponent<NetworkObject>();
            if (netObj != null) netObj.Spawn();

            var projScript = proj.GetComponent<EnemyProjectile>();
            if (projScript != null) projScript.Launch(dir, DamagePerShot);

            Debug.Log("[RangedAttackBehavior] Mermi atildi.");
            return;
        }

        // Fallback: gorunmez hitscan (mermi prefab yoksa)
        if (Physics.Raycast(origin, dir, out RaycastHit hit, DisengageRange))
        {
            var dmg = hit.transform.GetComponent<IDamageable>()
                      ?? hit.transform.GetComponentInParent<IDamageable>();

            if (dmg != null && dmg.IsAlive)
            {
                dmg.TakeDamage(DamagePerShot, hit.point, 0UL);
                Debug.Log($"[RangedAttackBehavior] Isabet (hitscan)! {DamagePerShot} hasar.");
            }
        }

#if UNITY_EDITOR
        Debug.DrawLine(origin, origin + dir * DisengageRange, Color.red, 0.15f);
#endif
    }
}
