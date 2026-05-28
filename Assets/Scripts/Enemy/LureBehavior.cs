using UnityEngine;

/// <summary>
/// Priest (Type B) dusmanin "esya alindi" sinyalini duyup tasiyici oyuncuyu
/// kovalamaya basladigi davranis. EnemyController, GameEventBus uzerinden
/// ItemPickedUpEvent'i dinler ve haritanin neresinde olursa olsun priest'i
/// bu davranisa gecirir; mesafe kontrolu yoktur (priest dogaustu sezgi).
///
/// Davranis donguсu:
/// - Enter'da NavMeshAgent'a kucuk bir hiz boost'u uygulanir (sustained baski).
/// - Her Tick'te tasiyicinin guncel transform pozisyonu okunup SetDestination
///   olarak yazilir; tasiyici hareket ettikce priest pesinden gelir.
/// - Tasiyici objeyi BIRAKIRSA (PhysicsObject.IsHeld false) lure aninda biter
///   ve default davranisa (Patrol/Wander) donulur — "obje yere dustu, izi
///   kaybettim" hissi.
/// - Tasiyici hat-of-sight'a girerse hemen ChaseBehavior'a devredilir; oradaki
///   son gorulen yer + give-up zinciri devam eder.
/// - Tasiyici olur ya da despawn olursa da default davranisa donulur.
///
/// Strategy pattern'in 9. concrete davranisi. Type A (robot) sagir oldugu icin
/// bu davranisa hic gecmez (subscribe tarafi _useRangedAttack kontrolu yapar).
/// </summary>
public class LureBehavior : IEnemyBehavior
{
    private const float SpeedBoostMult = 1.4f;  // sustained baski; priest "merak" hizi
    private const float RepathInterval = 0.25f; // SetDestination spam'i yerine cadence

    private readonly PhysicsObject _trackedItem;
    private readonly ulong _grabberClientId;
    private readonly Vector3 _fallbackPosition;

    private float _repathTimer;
    private float _baseSpeed;
    private bool  _speedBoosted;

    public LureBehavior(PhysicsObject trackedItem, ulong grabberClientId, Vector3 fallbackPosition)
    {
        _trackedItem = trackedItem;
        _grabberClientId = grabberClientId;
        _fallbackPosition = fallbackPosition;
    }

    public void Enter(EnemyController enemy)
    {
        _repathTimer = 0f;

        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
        {
            _baseSpeed = enemy.Agent.speed;
            enemy.Agent.speed = _baseSpeed * SpeedBoostMult;
            _speedBoosted = true;

            Vector3 target = ResolveCarrierPosition();
            enemy.Agent.SetDestination(target);
        }
        Debug.Log($"[LureBehavior] Pickup sinyali alindi, tasiyici takip basliyor (clientId={_grabberClientId}).");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.Agent == null || !enemy.Agent.isOnNavMesh) return;

        // Obje birakildi (ya da despawn oldu) -> izi kaybet, devriyeye don.
        if (_trackedItem == null || !_trackedItem.IsHeld)
        {
            enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
            return;
        }

        // Tasiyici hat-of-sight'ta -> Chase'e devret (ChaseBehavior zaten
        // CanSeePlayer + AttackTriggerRange + give-up zincirini surduruyor).
        if (enemy.PlayerTransform != null && enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Tasiyici hareket ediyor; destination'i kucuk araliklarla yenile.
        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            Vector3 target = ResolveCarrierPosition();
            enemy.Agent.SetDestination(target);
            _repathTimer = RepathInterval;
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (_speedBoosted && enemy.Agent != null)
        {
            enemy.Agent.speed = _baseSpeed;
            _speedBoosted = false;
        }
    }

    /// <summary>
    /// Tasiyici oyuncunun guncel dunya konumunu bulur. ServerPlayers icindeki
    /// GrabberClientId esleseni canli ise onu doner; bulamazsa son tasarruf
    /// olarak objenin kendi pozisyonunu (elde / yere dustugu yer), o da yoksa
    /// olay anindaki fallback'i kullanir.
    /// </summary>
    private Vector3 ResolveCarrierPosition()
    {
        var players = PlayerStateMachine.ServerPlayers;
        if (players != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                if (!p.IsAlive) continue;
                if (p.OwnerClientId != _grabberClientId) continue;
                return p.transform.position;
            }
        }

        if (_trackedItem != null)
            return _trackedItem.transform.position;

        return _fallbackPosition;
    }
}
