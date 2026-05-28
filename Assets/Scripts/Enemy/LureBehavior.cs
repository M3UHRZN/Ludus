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
///   gecerli NavMesh noktasina snap edilip SetDestination yazilir; tasiyici
///   hareket ettikce priest pesinden gelir.
/// - Path tikanik kalir (multi-floor / kopuk NavMesh: duvar ardinda hedef,
///   agent en yakin noktada donar): 3 saniye stuck'tan sonra tasiyiciya yakin,
///   tasiyicinin GORMEDIGI bir NavMesh noktasina warp edilir ("priest
///   sezgisi" / horror shortcut). Warp basarisizsa 8 saniyede vazgec.
/// - Tasiyici objeyi BIRAKIRSA (PhysicsObject.IsHeld false) lure aninda biter
///   ve default davranisa (Patrol/Wander) donulur.
/// - Tasiyici hat-of-sight'a girerse hemen ChaseBehavior'a devredilir; oradaki
///   son gorulen yer + give-up zinciri devam eder.
///
/// Strategy pattern'in 9. concrete davranisi. Type A (robot) sagir oldugu icin
/// bu davranisa hic gecmez (subscribe tarafi _useRangedAttack kontrolu yapar).
/// </summary>
public class LureBehavior : IEnemyBehavior
{
    private const float SpeedBoostMult     = 1.4f;  // sustained baski; priest "merak" hizi
    private const float RepathInterval     = 0.25f; // SetDestination spam'i yerine cadence
    private const float NavSampleRadius    = 4f;    // hedef noktayi NavMesh'e snap'lerken arama yaricapi

    // Stuck / warp parametreleri
    private const float StuckMovementEps   = 0.04f; // bu hizin altinda "duruyor" sayilir (m/s sqr)
    private const float StuckRemainingEps  = 1.5f;  // remainingDistance bunun altindayken hala duruyorsa stuck
    private const float StuckSecondsToWarp = 3f;    // ilk warp denemesi icin sure
    private const float StuckSecondsToGiveUp = 8f;  // toplam stuck tahammulu
    private const float WarpMinRadius      = 8f;    // tasiyici etrafindan en yakin warp mesafesi
    private const float WarpMaxRadius      = 15f;   // tasiyici etrafindan en uzak warp mesafesi
    private const int   WarpSampleAttempts = 12;    // random nokta deneme sayisi

    private readonly PhysicsObject _trackedItem;
    private readonly ulong _grabberClientId;
    private readonly Vector3 _fallbackPosition;

    private float _repathTimer;
    private float _stuckTimer;
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
        _stuckTimer = 0f;
        _warpedOnce = false;

        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
        {
            _baseSpeed = enemy.Agent.speed;
            enemy.Agent.speed = _baseSpeed * SpeedBoostMult;
            _speedBoosted = true;

            SetDestinationSnapped(enemy, ResolveCarrierPosition());
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

        // Tasiyici hat-of-sight'ta -> Chase'e devret.
        if (enemy.PlayerTransform != null && enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Stuck tespiti: agent neredeyse duruyor + path'in sonuna geldi/yakin.
        bool isStuck = !enemy.Agent.pathPending &&
                       enemy.Agent.velocity.sqrMagnitude < StuckMovementEps &&
                       enemy.Agent.remainingDistance < StuckRemainingEps;

        if (isStuck)
        {
            _stuckTimer += Time.deltaTime;

            // Ilk warp penceresi: tasiyicinin gormeyecegi yakin bir koruga isinla.
            if (!_warpedOnce && _stuckTimer >= StuckSecondsToWarp)
            {
                if (TryWarpBehindCover(enemy))
                {
                    _warpedOnce = true;
                    _stuckTimer = 0f;
                    _repathTimer = 0f; // bir sonraki Tick'te yeni destination yazilsin
                    Debug.Log("[LureBehavior] Path tikalı, tasiyiciya yakin bir koruga warp yapildi.");
                }
            }
            else if (_stuckTimer >= StuckSecondsToGiveUp)
            {
                Debug.Log("[LureBehavior] Tasiyiciya ulasilamadi, devriyeye donuluyor.");
                enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        // Destination'i kucuk araliklarla yenile (carving + carrier movement).
        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            SetDestinationSnapped(enemy, ResolveCarrierPosition());
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

    /// <summary>
    /// SetDestination'i ham pozisyonla degil, en yakin gecerli NavMesh noktasiyla yapar.
    /// Hedef kucuk bir NavMesh adasinin ortasindaysa (orn. duvar disinda) bu sayede
    /// agent en azindan ulasabilecegi en yakin noktaya yonelir.
    /// </summary>
    private static void SetDestinationSnapped(EnemyController enemy, Vector3 raw)
    {
        if (NavMesh.SamplePosition(raw, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
            enemy.Agent.SetDestination(hit.position);
        else
            enemy.Agent.SetDestination(raw);
    }

    /// <summary>
    /// Tasiyiciya WarpMinRadius..WarpMaxRadius arasinda, tasiyicinin LOS'unda
    /// OLMAYAN bir NavMesh noktasi arar ve bulursa Agent.Warp ile priest'i
    /// oraya isinlar. Server-only kosulur, NetworkTransform replikasyonu
    /// pozisyonu tum client'lara senkronize eder. Tasiyici tarafindan gorulen
    /// noktalar elenir, boylece warp "priest gozlerimin onunde belirdi" gibi
    /// gozukmez — kose arkasi / koridor ardi "supernatural shortcut" hissi olur.
    /// </summary>
    private bool TryWarpBehindCover(EnemyController enemy)
    {
        Vector3 carrierPos = ResolveCarrierPosition();
        Vector3 carrierEye = carrierPos + Vector3.up * 1.5f;

        for (int attempt = 0; attempt < WarpSampleAttempts; attempt++)
        {
            Vector2 dir = Random.insideUnitCircle.normalized;
            float radius = Random.Range(WarpMinRadius, WarpMaxRadius);
            Vector3 candidate = carrierPos + new Vector3(dir.x * radius, 0f, dir.y * radius);

            // NavMesh'te gecerli bir noktaya snap; degilse atla.
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, NavSampleRadius, NavMesh.AllAreas))
                continue;

            // Tasiyici bu noktayi GORMEMELI: carrierEye -> candidate yolunda engel olmali.
            Vector3 candidateEye = navHit.position + Vector3.up * 1.5f;
            Vector3 losVec = candidateEye - carrierEye;
            float losDist = losVec.magnitude;
            if (losDist < 0.01f) continue;

            if (!Physics.Raycast(carrierEye, losVec.normalized, losDist))
                continue; // tasiyici buradan candidate'i ack-acik gorur — warp etme

            // Tum kontrolleri gecti: priest'i bu noktaya isinla.
            enemy.Agent.Warp(navHit.position);
            return true;
        }
        return false;
    }
}
