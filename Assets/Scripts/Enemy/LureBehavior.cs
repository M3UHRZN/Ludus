using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Priest (Type B) dusmanin "esya alindi" sinyalini duyup tasiyici oyuncuyu
/// kovalamaya basladigi davranis. Multiplayer'a tam adapte:
/// - Tum aktif tasiyicilari (PhysicsObject.HeldItems registry'sinden) bilir.
/// - Her tick en yakin tasiyiciyi hedef alir; 3m hysteresis ile flap olmaz.
/// - Hedef carrier'i EnemyController'a hint olarak yazar boylece Chase'e
///   gecince (oyuncu gorus'e girince) hedef kaybolmaz.
/// - 4 oyuncu ayni anda grab etse priest en yakin olani secer; tasiyici
///   kaciyor ve uzaklasiyorsa kalan tasiyicilar arasinda yeniden degerlendirir.
/// - Tum tasiyicilar birakirsa default davranisa doner (Patrol/Wander).
///
/// Davranis Enter'da NavMeshAgent'a hiz boost'u uygular, Tick'te tasiyiciyi
/// kovalar, path tikanik kalirsa "supernatural warp" yapar (tasiyiciya yakin
/// LOS-blocked NavMesh noktasi). Stuck detection pozisyonel (transform delta).
///
/// Strategy pattern'in 9. concrete davranisi. Type A sagir oldugu icin bu
/// davranisa hic gecmez.
/// </summary>
public class LureBehavior : IEnemyBehavior
{
    private const float SpeedBoostMult        = 1.4f;
    private const float RepathInterval        = 0.25f;
    private const float StuckRepathInterval   = 1f;
    private const float NavSampleRadius       = 4f;

    // Stuck tespiti (movement-window based)
    private const float MoveWindowSec         = 2f;
    private const float MoveStuckThresholdSqr = 0.6f * 0.6f;
    private const float StuckSecondsToWarp    = 1.5f;
    private const float StuckSecondsToGiveUp  = 8f;

    // Warp parametreleri
    private const float WarpMinRadius         = 7f;
    private const float WarpMaxRadius         = 14f;
    private const int   WarpSampleAttempts    = 16;

    // Multi-carrier degerlendirme
    private const float CarrierReevalInterval     = 0.5f;
    private const float CarrierSwitchAdvantage    = 3f;  // yeni tasiyici 3m+ yakinsa switch et
    private const float CarrierHintRefreshSeconds = 1.5f; // EnemyController hint duration (her reeval'de yenilenir)

    private PhysicsObject _currentItem;
    private ulong _currentCarrierClientId;
    private Vector3 _lastCarrierPos;

    private float _repathTimer;
    private float _reevalTimer;
    private float _moveWindowTimer;
    private Vector3 _lastSamplePos;
    private float _stuckSeconds;
    private float _baseSpeed;
    private bool  _speedBoosted;
    private bool  _warpedOnce;

    public LureBehavior() { }

    public void Enter(EnemyController enemy)
    {
        _repathTimer = 0f;
        _reevalTimer = 0f;
        _moveWindowTimer = 0f;
        _stuckSeconds = 0f;
        _warpedOnce = false;

        if (!TryPickBestCarrier(enemy, out _currentItem, out _currentCarrierClientId, out _lastCarrierPos))
        {
            // Pickup event geldi ama registry zaten bos — fallback default
            enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
            return;
        }

        if (enemy.Agent != null && enemy.Agent.isOnNavMesh)
        {
            _baseSpeed = enemy.Agent.speed;
            enemy.Agent.speed = _baseSpeed * SpeedBoostMult;
            _speedBoosted = true;
            SetDestinationSnapped(enemy, _lastCarrierPos);
        }

        enemy.SetCarrierTargetHint(_currentCarrierClientId, CarrierHintRefreshSeconds);
        _lastSamplePos = enemy.transform.position;

        Debug.Log($"[LureBehavior] Pickup sinyali alindi, en yakin tasiyici takip basliyor (clientId={_currentCarrierClientId}).");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.Agent == null || !enemy.Agent.isOnNavMesh) return;

        // Tum tasiyicilar birakti -> patrol'a don
        if (PhysicsObject.HeldItems.Count == 0)
        {
            enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
            return;
        }

        // Tasiyici degerlendirmesi: her CarrierReevalInterval'de bir yeniden bak.
        _reevalTimer -= Time.deltaTime;
        if (_reevalTimer <= 0f)
        {
            _reevalTimer = CarrierReevalInterval;
            ReevaluateBestCarrier(enemy);

            // Hint'i refresh et — duration kisa ki Lure'dan cikilirsa carrier
            // gerekirse Chase devralana kadar dogal sonlanir.
            enemy.SetCarrierTargetHint(_currentCarrierClientId, CarrierHintRefreshSeconds);
        }

        // Eldeki item null oldu (despawn) veya artik tutulmuyor -> degerlendir
        if (_currentItem == null || !_currentItem.IsHeld)
        {
            if (!TryPickBestCarrier(enemy, out _currentItem, out _currentCarrierClientId, out _lastCarrierPos))
            {
                enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
                return;
            }
            enemy.SetCarrierTargetHint(_currentCarrierClientId, CarrierHintRefreshSeconds);
        }

        // Hat-of-sight'ta birini gordu mu? -> Chase'e carrier hint ile devret
        if (enemy.PlayerTransform != null && enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior(_currentCarrierClientId));
            return;
        }

        // Pozisyonel stuck olcumu (her MoveWindowSec'te)
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

        // Destination yenile
        _repathTimer -= Time.deltaTime;
        if (_repathTimer <= 0f)
        {
            _lastCarrierPos = ResolveCarrierPosition(_currentCarrierClientId, _currentItem);
            SetDestinationSnapped(enemy, _lastCarrierPos);
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
        // Carrier hint'i ATMIYORUZ: Chase'e devredildiyse onun da kullanmasi
        // gerekebilir; hint zaten kendi expiry'siyle dogal soner.
    }

    /// <summary>
    /// HeldItems registry'sinden tum aktif tasiyicilar arasinda priest'e en yakin
    /// olani secer. Yeni secim mevcut secimden CarrierSwitchAdvantage'tan daha yakin
    /// degilse mevcut korunur (flap onleme).
    /// </summary>
    private void ReevaluateBestCarrier(EnemyController enemy)
    {
        if (!TryPickBestCarrier(enemy, out var bestItem, out var bestClientId, out var bestPos))
            return;

        if (bestItem == _currentItem)
        {
            _lastCarrierPos = bestPos;
            return;
        }

        // Mevcut tasiyici hala gecerli mi? (despawn olmus / birakmis olabilir)
        bool currentValid = _currentItem != null && _currentItem.IsHeld;
        if (!currentValid)
        {
            _currentItem = bestItem;
            _currentCarrierClientId = bestClientId;
            _lastCarrierPos = bestPos;
            Debug.Log($"[LureBehavior] Mevcut tasiyici dustu, yeni en yakini secildi (clientId={bestClientId}).");
            return;
        }

        // Hysteresis: yeni daha yakin mi yeterince?
        float currentSqr = (enemy.transform.position
                           - ResolveCarrierPosition(_currentCarrierClientId, _currentItem)).sqrMagnitude;
        float bestSqr = (enemy.transform.position - bestPos).sqrMagnitude;
        float advantageSqr = CarrierSwitchAdvantage * CarrierSwitchAdvantage;

        if (currentSqr - bestSqr > advantageSqr)
        {
            _currentItem = bestItem;
            _currentCarrierClientId = bestClientId;
            _lastCarrierPos = bestPos;
            Debug.Log($"[LureBehavior] Daha yakin yeni tasiyici secildi (clientId={bestClientId}).");
        }
    }

    /// <summary>
    /// HeldItems icindeki tum aktif tasiyicilardan priest'e en yakini bulur.
    /// Tasiyici clientId server-side ServerPlayers'ta yoksa o item atlanir.
    /// </summary>
    private static bool TryPickBestCarrier(EnemyController enemy, out PhysicsObject bestItem,
                                            out ulong bestClientId, out Vector3 bestPos)
    {
        bestItem = null;
        bestClientId = 0UL;
        bestPos = Vector3.zero;
        float bestSqr = float.PositiveInfinity;
        Vector3 here = enemy.transform.position;

        var items = PhysicsObject.HeldItems;
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null || !item.IsHeld) continue;

            ulong carrierId = item.NetGrabberClientId.Value;
            Vector3 carrierPos = ResolveCarrierPosition(carrierId, item);

            float sqr = (carrierPos - here).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestItem = item;
                bestClientId = carrierId;
                bestPos = carrierPos;
            }
        }

        return bestItem != null;
    }

    /// <summary>
    /// clientId'den canli oyuncu transform'unu bulur; bulamazsa item'in oldugu
    /// yere (elinde ya da dustugu) dusurur. Item da yoksa Vector3.zero.
    /// </summary>
    private static Vector3 ResolveCarrierPosition(ulong clientId, PhysicsObject item)
    {
        var psm = PlayerStateMachine.GetServerPlayer(clientId);
        if (psm != null) return psm.transform.position;
        if (item != null) return item.transform.position;
        return Vector3.zero;
    }

    private static void SetDestinationSnapped(EnemyController enemy, Vector3 raw)
    {
        if (NavMesh.SamplePosition(raw, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
            enemy.Agent.SetDestination(hit.position);
        else
            enemy.Agent.SetDestination(raw);
    }

    private bool TryWarpNearCarrier(EnemyController enemy)
    {
        Vector3 carrierPos = ResolveCarrierPosition(_currentCarrierClientId, _currentItem);
        Vector3 carrierEye = carrierPos + Vector3.up * 1.5f;

        if (TryFindNavPointNearCarrier(carrierPos, carrierEye, requireBlockedLos: true,  out Vector3 hidden))
        {
            enemy.Agent.Warp(hidden);
            return true;
        }
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
                    continue;
            }

            result = navHit.position;
            return true;
        }

        result = Vector3.zero;
        return false;
    }
}
