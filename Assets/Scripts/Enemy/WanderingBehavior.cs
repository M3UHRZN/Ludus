using UnityEngine;
using UnityEngine.AI;

// Type B icin. NavMesh'te rastgele uzak noktalara gider. Gorus/ses tetikleyicileri Patrol ile ayni.
public class WanderingBehavior : IEnemyBehavior
{
    private const float MinWanderDistance = 8f;
    private const float MaxWanderDistance = 25f;
    private const float ArriveThreshold = 1.5f;
    private const int SampleAttempts = 8;

    public void Enter(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            PickNewDestination(enemy);
        Debug.Log("[WanderingBehavior] Gezinme basladi.");
    }

    public void Tick(EnemyController enemy)
    {
        if (!enemy.Agent.isOnNavMesh) return;

        // Gorus oncelikli
        if (enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Ses duydu, kaynaga git
        if (enemy.HeardNoise)
        {
            enemy.HeardNoise = false;
            Debug.Log("[WanderingBehavior] Ses duyuldu, Chase'e geciliyor.");
            enemy.SwitchBehavior(new ChaseBehavior(enemy.LastNoisePosition));
            return;
        }

        // Hedefe vardiysak yeni rastgele nokta sec
        if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < ArriveThreshold)
            PickNewDestination(enemy);
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.ResetPath();
    }

    // 8-25 birim uzakta gecerli bir nokta bul, bulamazsa frame atla
    private static void PickNewDestination(EnemyController enemy)
    {
        Vector3 origin = enemy.transform.position;

        for (int i = 0; i < SampleAttempts; i++)
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(MinWanderDistance, MaxWanderDistance);
            Vector3 candidate = origin + new Vector3(circle.x, 0f, circle.y);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, MaxWanderDistance, NavMesh.AllAreas))
            {
                if ((hit.position - origin).sqrMagnitude >= MinWanderDistance * MinWanderDistance)
                {
                    enemy.Agent.SetDestination(hit.position);
                    return;
                }
            }
        }
    }
}
