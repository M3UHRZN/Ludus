using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Patrol'den farkli olarak sabit waypoint'lere bagli kalmaz; NavMesh uzerinde
/// rastgele uzak noktalara giderek tum haritada dolasir. Type B (gezgin/wanderer)
/// dusman bu davranisla spawn olur.
///
/// Strategy pattern'in 5. concrete davranisi. Gorus ve ses tetikleyicileri
/// PatrolBehavior ile aynidir (CanSeePlayer -> Chase, HeardNoise -> Chase).
/// </summary>
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

        // Gorus oncelikli — oyuncuyu gorursen kovala
        if (enemy.CanSeePlayer())
        {
            enemy.SwitchBehavior(new ChaseBehavior());
            return;
        }

        // Ses duydu — ses konumuna giderek aramaya basla
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

    /// <summary>
    /// NavMesh uzerinde mevcut konumdan 8-25 birim uzakta gecerli bir nokta bulur.
    /// Birkac deneme yapar; bulamazsa o frame atlar, sonraki Tick tekrar dener.
    /// </summary>
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
