using UnityEngine;

/// <summary>
/// Sahneye koyulan spawn noktasi marker'i.
/// EnemySpawner bu objeleri bulur ve enemy spawn eder.
/// Inspector'dan kac enemy cikmasi gerektigini ve patrol yaricapini ayarla.
/// </summary>
public class EnemySpawnPoint : MonoBehaviour
{
    [Header("Spawn Ayarlari")]
    [Tooltip("Bu noktadan kac enemy spawn olacak")]
    [SerializeField] private int _enemyCount = 1;

    [Tooltip("Patrol waypoint'lerinin olusturulacagi yaricap")]
    [SerializeField] private float _patrolRadius = 8f;

    [Tooltip("Olusturulacak waypoint sayisi")]
    [SerializeField] private int _waypointCount = 4;

    public int EnemyCount => _enemyCount;
    public float PatrolRadius => _patrolRadius;
    public int WaypointCount => _waypointCount;

    /// <summary>
    /// Spawn noktasi etrafinda esit aralikli waypoint pozisyonlari uretir.
    /// </summary>
    public Vector3[] GenerateWaypointPositions()
    {
        var positions = new Vector3[_waypointCount];
        float angleStep = 360f / _waypointCount;

        for (int i = 0; i < _waypointCount; i++)
        {
            float angle = angleStep * i * Mathf.Deg2Rad;
            positions[i] = transform.position + new Vector3(
                Mathf.Cos(angle) * _patrolRadius,
                0f,
                Mathf.Sin(angle) * _patrolRadius);
        }
        return positions;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Spawn noktasi
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawIcon(transform.position, "d_winbtn_mac_close", true);

        // Patrol yaricapi
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _patrolRadius);

        // Waypoint onizleme
        Gizmos.color = Color.cyan;
        var wps = GenerateWaypointPositions();
        for (int i = 0; i < wps.Length; i++)
        {
            Gizmos.DrawSphere(wps[i], 0.2f);
            int next = (i + 1) % wps.Length;
            Gizmos.DrawLine(wps[i], wps[next]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Secildiginde daha belirgin goster
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 0.5f);

        UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
        UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.up, _patrolRadius);
    }
#endif
}
