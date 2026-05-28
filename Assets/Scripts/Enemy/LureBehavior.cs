using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Priest (Type B) dusmanin "esya alindi" sinyalini duyup tasiyici oyuncuyu
/// kovalamaya basladigi davranis. EnemyController, GameEventBus uzerinden
/// ItemPickedUpEvent'i dinler ve haritanin neresinde olursa olsun priest'i
/// bu davranisa gecirir; mesafe kontrolu yoktur (priest dogaustu sezgi).
///
/// Davranis donguсu:
/// - Enter'da NavMeshAgent'a kucuk bir hiz boost'u uygulanir (sustained baski).
/// - Her Tick'te tasiyicinin guncel transform pozisyonu okunup, en yakin
///   gecerli NavMesh noktasina snap edilip SetDestination yazilir.
/// - Stuck tespiti POZISYONEL: 2 saniyelik pencerede priest 60cm'den az yer
///   degistirdiyse "stuck". Velocity tabanli check, repath spam'i nedeniyle
///   guvenilmez oldugu icin transform delta'ya bakiyoruz.
/// - 1.5sn stuck oldumu: tasiyiciya yakin (7-14m), oncelikli olarak tasiyici
///   LOS'unda OLMAYAN bir NavMesh noktasi aranir; bulamazsa LOS umursamadan
///   yakin bir gecerli nokta secilir. Bulunan noktaya Agent.Warp ile isinlanir
///   ("priest sezgisi" / horror shortcut). Toplam 8sn'de tek warp denenir;
///   hala stuck'sa default davranisa donulur.
/// - Tasiyici objeyi BIRAKIRSA (PhysicsObject.IsHeld false) lure aninda biter
///   ve default davranisa (Patrol/Wander) donulur.
/// - Tasiyici hat-of-sight'a girerse hemen ChaseBehavior'a devredilir.
///
/// Strategy pattern'in 9. concrete davranisi. Type A (robot) sagir oldugu icin
/// bu davranisa hic gecmez (subscribe tarafi _useRangedAttack kontrolu yapar).
/// </summary>
public class LureBehavior : IEnemyBehavior
{
    private const float SpeedBoostMult        = 1.4f;
    private const float RepathInterval        = 0.25f; // hareket halinde repath cadence
    private const float StuckRepathInterval   = 1f;    // stuck'ken yavasla
    private const float NavSampleRadius       = 4f;

    // Stuck tespiti (movement-window based)
    private const float MoveWindowSec         = 2f;
    private const float MoveStuckThresholdSqr = 0.6f * 0.6f; // 60cm pencere altinda stuck
    private const float StuckSecondsToWarp    = 1.5f;
    private const float StuckSecondsToGiveUp  = 8f;

    // Warp parametreleri
    private const float WarpMinRadius         = 7f;
    private const float WarpMaxRadius         = 14f;
    private const int   WarpSampleAttempts    = 16;

    private readonly PhysicsObject _trackedItem;
    private readonly ulong _grabberClientId;
    private readonly Vector3 _fallbackPosition;

    private float _repathTimer;
    private float _moveWindowTimer;
    private Vector3 _lastSamplePos;
    private float _stuckSeconds;
    private float _baseSpeed;
    private bool  _speedBoosted;
    private bool  _warpedOnce;

    public LureBehavior(PhysicsObject trackedItem, ulong grabberClientId, Vector3 fallbackPosition)
    {
        _trackedItem = trackedItem;
        _grabberClientId = grabberClientId;
        _fallbackPosition = fallbackPosition;
    }

    public void Enter(EnemyController enemy)
    {
        _repathTimer = 0f;
        _moveWindowTimer = 0f;
        _stuckSeconds = 0f;
        _warpedOnce = false;

        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
        {
            _baseSpeed = enemy.Agent.speed;
            enemy.Agent.speed = _baseSpeed * SpeedBoostMult;
            _speedBoosted = true;

            SetDestinationSnapped(enemy, ResolveCarrierPosition());
        }
        _lastSamplePos = enemy.transform.position;
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

        // Tasiyici hat-of-sight'ta -> Chase'e devret.
        if (enemy.PlayerTransform != null && enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // POZISYONEL stuck olcumu (her MoveWindowSec'te bir):
        // velocity tabanli check repath spam ile birlikte guvenilmezdi —
        // transform'un fiziksel olarak ne kadar yer degistirdigine bakiyoruz.
        _moveWindowTimer += Time.deltaTime;
        if (_moveWindowTimer >= MoveWindowSec)
        {
            float windowDeltaSqr = (enemy.transform.position - _lastSamplePos).sqrMagnitude;
            if (windowDeltaSqr < MoveStuckThresholdSqr)
            {
                _stuckSeconds += MoveWindowSec;

                if (!_warpedOnce && _stuckSeconds >= StuckSecondsToWarp)
                {
                    if (TryWarpNearCarrier(enemy))
                    {
                        _warpedOnce = true;
                        _stuckSeconds = 0f;
                        _repathTimer = 0f;
                        Debug.Log("[LureBehavior] Path tikalı, tasiyiciya yakin warp yapildi.");
                    }
                    else
                    {
                        Debug.Log("[LureBehavior] Warp icin uygun NavMesh noktasi bulunamadi.");
                    }
                }
                else if (_stuckSeconds >= StuckSecondsToGiveUp)
                {
                    Debug.Log("[LureBehavior] Tasiyiciya ulasilamadi, devriyeye donuluyor.");
                    enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
                    return;
                }
            }
            else
            {
                _stuckSeconds = 0f;
            }

            _lastSamplePos = enemy.transform.position;
            _moveWindowTimer = 0f;
        }

        // Destination'i kucuk araliklarla yenile. Stuck'ken yavas yenile
        // (faydasi yok, sadece dirty flag spam'i olur).
        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            SetDestinationSnapped(enemy, ResolveCarrierPosition());
            _repathTimer = _stuckSeconds > 0f ? StuckRepathInterval : RepathInterval;
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

    /// <summary>
    /// SetDestination'i ham pozisyonla degil, en yakin gecerli NavMesh noktasiyla yapar.
    /// </summary>
    private static void SetDestinationSnapped(EnemyController enemy, Vector3 raw)
    {
        if (NavMesh.SamplePosition(raw, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
            enemy.Agent.SetDestination(hit.position);
        else
            enemy.Agent.SetDestination(raw);
    }

    /// <summary>
    /// Tasiyici etrafinda WarpMinRadius..WarpMaxRadius'te yakin bir NavMesh
    /// noktasi bulup Agent.Warp ile priest'i isinlar. Once tasiyici LOS'unda
    /// olmayan ("saklanik warp") nokta aranir; bulunamazsa LOS umursamadan
    /// yakin bir gecerli noktaya isinlanir. Server-only kosulur,
    /// NetworkTransform pozisyon degisikligini tum client'lara replikator.
    /// </summary>
    private bool TryWarpNearCarrier(EnemyController enemy)
    {
        Vector3 carrierPos = ResolveCarrierPosition();
        Vector3 carrierEye = carrierPos + Vector3.up * 1.5f;

        if (TryFindNavPointNearCarrier(carrierPos, carrierEye, requireBlockedLos: true,  out Vector3 hidden))
        {
            enemy.Agent.Warp(hidden);
            return true;
        }

        // Fallback: LOS umursamadan herhangi bir gecerli yakin nokta. Iyi degil ama
        // sonsuz stuck'tan iyi; oyuncu warp'in olusunu gorse de en azindan priest
        // ulasabilir bir adada belirir.
        if (TryFindNavPointNearCarrier(carrierPos, carrierEye, requireBlockedLos: false, out Vector3 anyPoint))
        {
            enemy.Agent.Warp(anyPoint);
            return true;
        }

        return false;
    }

    private static bool TryFindNavPointNearCarrier(Vector3 carrierPos, Vector3 carrierEye,
                                                   bool requireBlockedLos, out Vector3 result)
    {
        for (int attempt = 0; attempt < WarpSampleAttempts; attempt++)
        {
            Vector2 planar = Random.insideUnitCircle.normalized;
            float radius = Random.Range(WarpMinRadius, WarpMaxRadius);
            Vector3 candidate = carrierPos + new Vector3(planar.x * radius, 0f, planar.y * radius);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, NavSampleRadius, NavMesh.AllAreas))
                continue;

            if (requireBlockedLos)
            {
                Vector3 candidateEye = navHit.position + Vector3.up * 1.5f;
                Vector3 losVec = candidateEye - carrierEye;
                float losDist = losVec.magnitude;
                if (losDist < 0.01f) continue;

                if (!Physics.Raycast(carrierEye, losVec.normalized, losDist))
                    continue; // tasiyici buradan candidate'i gorur — LOS'lu turda elenir
            }

            result = navHit.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }
}
