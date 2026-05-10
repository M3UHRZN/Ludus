using UnityEngine;

public class ChaseBehavior : IEnemyBehavior
{
    private const float LostSightDelay = 2f;
    private const float AttackTriggerRange = 1.8f;
    private float _lostSightTimer;

    public void Enter(EnemyController enemy)
    {
        _lostSightTimer = LostSightDelay;
        Debug.Log("[ChaseBehavior] Kovalama başladı.");
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;
        if (!enemy.Agent.isOnNavMesh) return;

        if (enemy.CanSeePlayer())
        {
            _lostSightTimer = LostSightDelay;

            // Yakinlasinca saldiriya gec
            float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);
            if (dist <= AttackTriggerRange)
            {
                enemy.SwitchBehavior(new AttackBehavior());
                return;
            }

            enemy.Agent.SetDestination(enemy.PlayerTransform.position);
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
        enemy.Agent.ResetPath();
        Debug.Log("[ChaseBehavior] Kovalama bitti, devriye dönüyor.");
    }
}
