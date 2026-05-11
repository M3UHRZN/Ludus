using UnityEngine;

public class ChaseBehavior : IEnemyBehavior
{
    private const float LostSightDelay = 2f;
    private const float NoiseReachThreshold = 1.5f;

    private readonly Vector3? _noisePosition;
    private float _lostSightTimer;
    private bool _huntingNoise;

    public ChaseBehavior() { }

    /// <summary>Ses duyma sebebiyle Chase'e gecildiyse, hedef konum verilir.</summary>
    public ChaseBehavior(Vector3 noisePosition)
    {
        _noisePosition = noisePosition;
    }

    public void Enter(EnemyController enemy)
    {
        _lostSightTimer = LostSightDelay;

        // Ses kaynagi varsa ve oyuncu hala gorulmuyor ise oraya git
        if (_noisePosition.HasValue && !enemy.CanSeePlayer() && enemy.Agent.isOnNavMesh)
        {
            _huntingNoise = true;
            enemy.Agent.SetDestination(_noisePosition.Value);
            Debug.Log($"[ChaseBehavior] Ses kaynagina dogru hareket ediliyor: {_noisePosition.Value}");
        }
        else
        {
            Debug.Log("[ChaseBehavior] Kovalama basladi.");
        }
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;
        if (!enemy.Agent.isOnNavMesh) return;

        if (enemy.CanSeePlayer())
        {
            _lostSightTimer = LostSightDelay;
            _huntingNoise = false;

            // Yakinlasinca saldiriya gec
            float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);
            if (dist <= enemy.AttackTriggerRange)
            {
                enemy.SwitchBehavior(new AttackBehavior());
                return;
            }

            enemy.Agent.SetDestination(enemy.PlayerTransform.position);
        }
        else if (_huntingNoise)
        {
            // Ses kaynagina varilirsa noise modu kapan ve normal lost-sight sayacina don
            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < NoiseReachThreshold)
            {
                _huntingNoise = false;
                _lostSightTimer = LostSightDelay;
                Debug.Log("[ChaseBehavior] Ses kaynagina varildi, etrafa bakiliyor.");
            }
        }
        else
        {
            _lostSightTimer -= Time.deltaTime;
            if (_lostSightTimer <= 0f)
                enemy.SwitchBehavior(new PatrolBehavior());
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.ResetPath();
        Debug.Log("[ChaseBehavior] Kovalama bitti, devriye donuyor.");
    }
}
