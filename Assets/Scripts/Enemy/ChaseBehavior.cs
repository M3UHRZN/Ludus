using UnityEngine;

// Oyuncuyu kovalar. LOS varsa direkt takip + AttackTriggerRange'e girince saldiri.
// LOS kayboldugunda son gorulen yere veya ses kaynagina gider, etrafa bakar, sonra default'a doner.
public class ChaseBehavior : IEnemyBehavior
{
    private const float GiveUpDelay = 4f;
    private const float ReachThreshold = 1.5f;

    // Onumuze cikan kucuk fizikli objeleri it (path takilmasi olmasin)
    private const float PushScanRange = 1.45f;
    private const float PushScanRadius = 0.45f;
    private const float PushForce = 110f;
    private const float MaxPushableMass = 30f;
    private const float MinAgentSpeedToPush = 0.2f;

    private const float CarrierHintHoldDuration = 8f;

    private readonly Vector3? _noisePosition;
    private readonly ulong? _carrierHintClientId;
    private float _lostSightTimer;
    private Vector3 _searchPoint;
    private bool _hasSearchPoint;
    private bool _huntingNoise;

    public ChaseBehavior() { }

    public ChaseBehavior(Vector3 noisePosition)
    {
        _noisePosition = noisePosition;
    }

    public ChaseBehavior(ulong carrierHintClientId)
    {
        _carrierHintClientId = carrierHintClientId;
    }

    public void Enter(EnemyController enemy)
    {
        _lostSightTimer = GiveUpDelay;

        // Lure'dan devralindiysa, carrier hint olarak isaretle
        if (_carrierHintClientId.HasValue)
            enemy.SetCarrierTargetHint(_carrierHintClientId.Value, CarrierHintHoldDuration);

        if (_noisePosition.HasValue && !enemy.CanSeePlayer() && enemy.Agent.isOnNavMesh)
        {
            _searchPoint = _noisePosition.Value;
            _hasSearchPoint = true;
            _huntingNoise = true;
            enemy.Agent.SetDestination(_searchPoint);
            Debug.Log("[ChaseBehavior] Ses kaynagina dogru gidiliyor.");
        }
        else
        {
            if (enemy.CanSeePlayer())
            {
                _searchPoint = enemy.PlayerTransform.position;
                _hasSearchPoint = true;
            }
            Debug.Log("[ChaseBehavior] Kovalama basladi.");
        }
    }

    private const float DeadPlayerDisengageGrace = 1.5f;

    public void Tick(EnemyController enemy)
    {
        if (!enemy.Agent.isOnNavMesh) return;

        if (enemy.Agent.velocity.sqrMagnitude > MinAgentSpeedToPush * MinAgentSpeedToPush)
            PushObstaclesAhead(enemy);

        // Tum oyuncular oldu, kisa grace ile vazgec
        if (enemy.PlayerTransform == null)
        {
            if (_lostSightTimer > DeadPlayerDisengageGrace)
                _lostSightTimer = DeadPlayerDisengageGrace;
            TickSearchOrGiveUp(enemy);
            return;
        }

        // LOS varsa direkt kovala
        if (enemy.CanSeePlayer())
        {
            _lostSightTimer = GiveUpDelay;
            _huntingNoise = false;
            _searchPoint = enemy.PlayerTransform.position;
            _hasSearchPoint = true;

            float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);
            if (dist <= enemy.AttackTriggerRange)
            {
                enemy.SwitchBehavior(enemy.CreateAttackBehavior());
                return;
            }

            enemy.Agent.SetDestination(enemy.PlayerTransform.position);
            return;
        }

        // LOS yok, son ize git, yoksa vazgec
        TickSearchOrGiveUp(enemy);
    }

    private void TickSearchOrGiveUp(EnemyController enemy)
    {
        if (_hasSearchPoint)
        {
            enemy.Agent.SetDestination(_searchPoint);
            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < ReachThreshold)
            {
                _hasSearchPoint = false;
                if (_huntingNoise)
                {
                    _huntingNoise = false;
                    Debug.Log("[ChaseBehavior] Ize varildi, etrafa bakiliyor.");
                }
            }
            return;
        }

        // Iz tuketildi, bir sure etrafa bak, sonra default
        _lostSightTimer -= Time.deltaTime;
        if (_lostSightTimer <= 0f)
            enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.ResetPath();
    }

    // Kucuk fizikli engelleri yoldan it (varil, kasa). Buyukleri, oyuncuyu, dusmani ve aktif firlatilmis cisimleri atla.
    private static void PushObstaclesAhead(EnemyController enemy)
    {
        Vector3 fwd = enemy.Agent.velocity;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) return;
        fwd.Normalize();

        Vector3 origin = enemy.transform.position + Vector3.up * 0.9f;

        if (!Physics.SphereCast(origin, PushScanRadius, fwd, out RaycastHit hit,
                PushScanRange, ~0, QueryTriggerInteraction.Ignore))
            return;

        var rb = hit.rigidbody;
        if (rb == null) return;
        if (rb.isKinematic) return;
        if (rb.mass > MaxPushableMass) return;

        if (hit.transform.GetComponentInParent<PlayerStateMachine>() != null) return;
        if (hit.transform.GetComponentInParent<EnemyController>() != null) return;

        var po = hit.transform.GetComponentInParent<PhysicsObject>();
        if (po != null && po.IsActiveThrow) return;

        rb.AddForce(fwd * PushForce, ForceMode.Force);
    }
}
