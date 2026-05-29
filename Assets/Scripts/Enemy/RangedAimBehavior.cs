using UnityEngine;

/// <summary>
/// Uzaktan saldiri oncesi kirmizi lazer "telegraph" davranisi. Type A (robot)
/// dusman oyuncuyu yeni gordugunde RangedAttackBehavior'a gecmeden once burada
/// kisa bir nisan alma penceresi (AimDuration) acar; bu sirada FirePoint'ten
/// oyuncuya kirmizi lazer cizilir. Pencere oyuncuya kacma / koruga gecme firsati
/// verir. Sure dolunca dogrudan RangedAttackBehavior'a devredilir.
///
/// Telegraph sirasinda gorus kaybi veya menzil disina cikis -> Chase'e doner
/// (atis yapilmaz). Ayni engagement'ta tekrar yakinlasilirsa Chase yeniden
/// bu davranisa baglar; yani her yeni temas icin yeni bir telegraph.
///
/// Lazer cizimi multiplayer'da hem host'ta hem client'larda gorunmek zorunda
/// oldugu icin LineRenderer'i bu davranis dogrudan olusturmaz; bunun yerine
/// EnemyNetState'teki NetworkVariable'lara aim durumu ve hedef pozisyonu
/// yazilir, gorsel cizimi her client kendi EnemyNetState.Update'inde yapar.
///
/// Strategy pattern'in 8. concrete davranisi.
/// </summary>
public class RangedAimBehavior : IEnemyBehavior
{
    private const float AimDuration    = 1.2f;   // lazerin gorundugu sure (telegraph)
    private const float DisengageRange = 16f;    // bu mesafenin disina cikilirsa Chase'e don
    private const float AimTurnSpeed   = 10f;    // nisan alirken govdeyi oyuncuya cevirme hizi
    private const float TargetEpsilon  = 0.25f;  // NetAimTarget'i tekrar yazma esigi (bandwidth)

    private float _aimTimer;
    private EnemyNetState _netState;
    private Vector3 _lastPublishedTarget;
    private bool _publishedOnce;

    public void Enter(EnemyController enemy)
    {
        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = true;

        _aimTimer = AimDuration;
        _publishedOnce = false;

        _netState = enemy.GetComponent<EnemyNetState>();
        if (_netState != null)
        {
            Vector3 initialTarget = enemy.PlayerTransform != null
                ? enemy.PlayerTransform.position + Vector3.up
                : enemy.transform.position + enemy.transform.forward * 4f;

            _netState.ServerStartAim(initialTarget);
            _lastPublishedTarget = initialTarget;
            _publishedOnce = true;
        }

        Debug.Log("[RangedAimBehavior] Lazer nisani basladi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        FacePlayer(enemy);

        // Mesafe disina ciktiysa nisani iptal et, tekrar kovala
        float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);
        if (dist > DisengageRange)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Gorus kaybi -> nisani iptal, atis yapmadan Chase'e
        if (!enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Hedefi guncelle (esik kadar hareket ettiyse network yazimi yap)
        Vector3 target = enemy.PlayerTransform.position + Vector3.up;
        if (_netState != null &&
            (!_publishedOnce || (target - _lastPublishedTarget).sqrMagnitude > TargetEpsilon * TargetEpsilon))
        {
            _netState.ServerUpdateAimTarget(target);
            _lastPublishedTarget = target;
            _publishedOnce = true;
        }

        _aimTimer -= Time.deltaTime;
        if (_aimTimer <= 0f)
        {
            // Telegraph bitti -> RangedAttackBehavior devral. Exit() lazeri kapatir,
            // RangedAttackBehavior kendi enter'inda ilk atisini ~0.6s sonra yapar.
            enemy.SwitchBehavior(new RangedAttackBehavior());
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
            enemy.Agent.isStopped = false;

        if (_netState != null)
            _netState.ServerStopAim();
    }

    private static void FacePlayer(EnemyController enemy)
    {
        Vector3 toPlayer = enemy.PlayerTransform.position - enemy.transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(toPlayer);
            enemy.transform.rotation = Quaternion.Slerp(
                enemy.transform.rotation, look, Time.deltaTime * AimTurnSpeed);
        }
    }
}
