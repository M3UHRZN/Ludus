using UnityEngine;

/// <summary>
/// Bir oda prefab'inin icine konulur. EnemySpawner harita uretildiginde
/// sahnedeki PatrolWaypointGroup'lari tarar ve EnemyController'lara
/// patrol noktasi olarak atar.
///
/// Anil ile kontrat: oda prefab'larinin icinde 1 adet bu component bulunur,
/// child Transform'lar (genellikle 3-5 tane) waypoint olarak referanslanir.
/// </summary>
public class PatrolWaypointGroup : MonoBehaviour
{
    [Header("Waypoint Listesi")]
    [Tooltip("Patrol noktalarini sirayla buraya surukle. Genellikle child transform'lar.")]
    [SerializeField] private Transform[] _waypoints;

    public Transform[] Waypoints => _waypoints;
    public int Count => _waypoints != null ? _waypoints.Length : 0;

    /// <summary>
    /// Inspector'da Reset / Sag tik ile cagrilirsa: tum direct child'lari
    /// otomatik olarak waypoint dizisine doldurur.
    /// </summary>
    [ContextMenu("Auto-fill from children")]
    private void AutoFillFromChildren()
    {
        int childCount = transform.childCount;
        _waypoints = new Transform[childCount];
        for (int i = 0; i < childCount; i++)
            _waypoints[i] = transform.GetChild(i);
    }

    /// <summary>
    /// Runtime'da waypoint dizisini set eder. Map adaptoru (MapEnemyBridge)
    /// prosedurel olarak uretilen odalara waypoint atarken kullanir.
    /// </summary>
    public void SetWaypoints(Transform[] waypoints)
    {
        _waypoints = waypoints;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;

        // Waypoint'leri yesil kure ile cizip aralarini cizgi ile bagla
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(_waypoints[i].position, 0.4f);

            int next = (i + 1) % _waypoints.Length;
            if (_waypoints[next] != null)
                Gizmos.DrawLine(_waypoints[i].position, _waypoints[next].position);
        }

        // Group merkezi
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.3f);
        Gizmos.DrawCube(transform.position + Vector3.up * 0.1f, new Vector3(0.5f, 0.1f, 0.5f));
    }
#endif
}
