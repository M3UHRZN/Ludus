using UnityEngine;

// Type A robot ates etmeden once kisa bir lazer telegraph gosterir.
// Sure dolunca RangedAttackBehavior'a gecer. LOS kaybi veya menzil disi cikis Chase'e dondurur.
public class RangedAimBehavior : IEnemyBehavior
{
    private const float AimDuration = 1.2f;
    private const float DisengageRange = 16f;
    private const float AimTurnSpeed = 10f;
    private const float TargetEpsilon = 0.25f;

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

        // Menzil disina cikti, Chase
        float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);
        if (dist > DisengageRange)
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // LOS kayip, atis iptal
        if (!enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Hedef pozisyonunu network'e yaz (epsilon ile bandwidth)
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
