using UnityEngine;

/// <summary>
/// Priest (Type B) dusmanin "esya alindi" sinyalini duyarak grab'in olustugu
/// dunya konumuna gittigi davranis. EnemyController, GameEventBus uzerinden
/// ItemPickedUpEvent'i dinler ve yakinda olusan pickup'larda dusmani bu
/// davranisa gecirir.
///
/// Lure sirasinda:
/// - NavMeshAgent kucuk bir hiz boost'u ile hedef noktaya yonelir
///   (priest'in "merak / urpertici sezgi" hissi).
/// - Yol uzerinde oyuncuyu gorurse hemen ChaseBehavior'a devreder
///   (lure -> active pursuit gecisi temiz, flapping olusmaz).
/// - Hedef noktaya varilirsa kisa bir patience suresi etrafa bakar; bu
///   sure icinde oyuncuyu gormezse default davranisina (Patrol/Wander) doner.
///
/// Strategy pattern'in 9. concrete davranisi. Type A (robot) bu davranisi
/// hic kullanmaz; subscribe tarafi (EnemyController) zaten _useRangedAttack
/// kontrolu yapip Type A'yi disarida birakir.
/// </summary>
public class LureBehavior : IEnemyBehavior
{
    private const float ArrivalDistance = 2.0f;  // hedef noktaya "varildi" sayilan mesafe
    private const float LurePatience    = 6.0f;  // varinca etrafa bakma suresi
    private const float SpeedBoostMult  = 1.15f; // merak/urpertici sezgi hizi
    private const float RepathInterval  = 0.3f;  // surekli SetDestination spam'i yerine kucuk araliklarla yenile

    private readonly Vector3 _lureTarget;
    private readonly ulong   _grabberClientId;

    private float _patience;
    private float _repathTimer;
    private float _baseSpeed;
    private bool  _speedBoosted;
    private bool  _arrived;

    public LureBehavior(Vector3 lureTarget, ulong grabberClientId)
    {
        _lureTarget = lureTarget;
        _grabberClientId = grabberClientId;
    }

    public void Enter(EnemyController enemy)
    {
        _patience = LurePatience;
        _arrived = false;
        _repathTimer = 0f;

        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
        {
            _baseSpeed = enemy.Agent.speed;
            enemy.Agent.speed = _baseSpeed * SpeedBoostMult;
            _speedBoosted = true;
            enemy.Agent.SetDestination(_lureTarget);
        }
        Debug.Log($"[LureBehavior] Pickup sinyali alindi, kaynaga gidiliyor (clientId={_grabberClientId}).");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.Agent == null || !enemy.Agent.isOnNavMesh) return;

        // Yol uzerinde oyuncuyu gorduk -> Chase'e devret. ChaseBehavior'in tum
        // yakalama mantigini tekrar yazmayalim; Chase'e gecince oradaki konum
        // hatirlama + attack-range geciti dogal akar.
        if (enemy.PlayerTransform != null && enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Hedef konuma yaklasiyoruz (varis henuz olmadi): pathing devam etsin,
        // arada bir SetDestination'i yenile (oyuncu hareket etmedi ama
        // NavMeshObstacle carving guncel olabilir).
        if (!_arrived)
        {
            _repathTimer -= Time.deltaTime;
            if (_repathTimer <= 0f)
            {
                enemy.Agent.SetDestination(_lureTarget);
                _repathTimer = RepathInterval;
            }

            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < ArrivalDistance)
            {
                _arrived = true;
                Debug.Log("[LureBehavior] Kaynaga varildi, etrafa bakiliyor.");
            }
            return;
        }

        // Vardik, etrafa bakiyoruz. Patience dolarsa default davranisa don.
        _patience -= Time.deltaTime;
        if (_patience <= 0f)
            enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
    }

    public void Exit(EnemyController enemy)
    {
        if (_speedBoosted && enemy.Agent != null)
        {
            enemy.Agent.speed = _baseSpeed;
            _speedBoosted = false;
        }
    }
}
