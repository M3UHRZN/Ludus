using UnityEngine;

public class PatrolBehavior : IEnemyBehavior
{
    private const float ReachThreshold = 0.5f;

    public void Enter(EnemyController enemy)
    {
        enemy.CurrentWaypointIndex = 0;
        MoveToWaypoint(enemy);
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PatrolWaypoints == null || enemy.PatrolWaypoints.Length == 0) return;
        if (!enemy.Agent.isOnNavMesh) return;
        if (enemy.Agent.pathPending) return;

        if (enemy.Agent.remainingDistance < ReachThreshold)
        {
            enemy.CurrentWaypointIndex = (enemy.CurrentWaypointIndex + 1) % enemy.PatrolWaypoints.Length;
            MoveToWaypoint(enemy);
        }

        // TODO Sprint 1: if (enemy.HeardNoise) enemy.SwitchBehavior(new ChaseBehavior());
    }

    public void Exit(EnemyController enemy)
    {
        enemy.Agent.ResetPath();
    }

    private static void MoveToWaypoint(EnemyController enemy)
    {
        if (enemy.PatrolWaypoints == null || enemy.PatrolWaypoints.Length == 0) return;

        Transform wp = enemy.PatrolWaypoints[enemy.CurrentWaypointIndex];
        if (wp != null)
            enemy.Agent.SetDestination(wp.position);
        else
            Debug.LogWarning($"[PatrolBehavior] Waypoint {enemy.CurrentWaypointIndex} is null.");
    }
}
