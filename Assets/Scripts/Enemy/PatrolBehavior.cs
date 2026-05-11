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

        // Gorus oncelikli
        if (enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Ses duydu: oyuncuyu hala goremeyebiliriz ama Chase'e gecip
        // ses konumuna dogru hareket ederek aramayi baslat
        if (enemy.HeardNoise)
        {
            enemy.HeardNoise = false; // tek seferlik tetik
            Debug.Log("[PatrolBehavior] Ses duyuldu, Chase'e geciliyor.");
            enemy.SwitchBehavior(new ChaseBehavior(enemy.LastNoisePosition));
            return;
        }

        if (enemy.Agent.remainingDistance < ReachThreshold)
        {
            enemy.CurrentWaypointIndex = (enemy.CurrentWaypointIndex + 1) % enemy.PatrolWaypoints.Length;
            MoveToWaypoint(enemy);
        }
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
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
