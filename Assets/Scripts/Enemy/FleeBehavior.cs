using UnityEngine;

public class FleeBehavior : IEnemyBehavior
{
    private const float DefaultFleeDistance = 12f;
    private const float ArriveThreshold = 1f;

    private readonly Vector3 _threatPosition;
    private float _remainingDuration;
    private bool _hasDestination;

    public FleeBehavior(Vector3 threatPosition, float duration = 3f)
    {
        _threatPosition = threatPosition;
        _remainingDuration = duration;
    }

    public void Enter(EnemyController enemy)
    {
        if (!enemy.Agent.isOnNavMesh) return;

        // Tehdidin tersine bir hedef nokta sec
        Vector3 awayDir = (enemy.transform.position - _threatPosition);
        if (awayDir.sqrMagnitude < 0.01f)
            awayDir = Random.insideUnitSphere; // tam ustunde patladiysa rastgele savrul
        awayDir.y = 0f;
        awayDir.Normalize();

        Vector3 target = enemy.transform.position + awayDir * DefaultFleeDistance;

        if (UnityEngine.AI.NavMesh.SamplePosition(target, out var hit, DefaultFleeDistance, UnityEngine.AI.NavMesh.AllAreas))
        {
            enemy.Agent.SetDestination(hit.position);
            _hasDestination = true;
        }

        Debug.Log("[FleeBehavior] Kacis basladi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (!enemy.Agent.isOnNavMesh) return;

        _remainingDuration -= Time.deltaTime;

        // Sure doldu ya da hedefe vardi -> patrol'a don
        bool reached = _hasDestination
                       && !enemy.Agent.pathPending
                       && enemy.Agent.remainingDistance < ArriveThreshold;

        if (_remainingDuration <= 0f || reached)
        {
            enemy.SwitchBehavior(new PatrolBehavior());
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.ResetPath();

        Debug.Log("[FleeBehavior] Kacis bitti.");
    }
}
