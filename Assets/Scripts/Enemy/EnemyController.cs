using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Transform[] _patrolWaypoints;

    [Header("Detection")]
    [SerializeField] private float _sightRange = 15f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _noiseDetectionRadius = 8f;
    [SerializeField] private LayerMask _playerLayer;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] PatrolWaypoints => _patrolWaypoints;
    public Transform PlayerTransform { get; private set; }
    public Transform CurrentTarget { get; set; }
    public int CurrentWaypointIndex { get; set; }
    public bool HeardNoise { get; set; }

    private IEnemyBehavior _current;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        // Host-Client topology: AI yalnizca server'da kosar.
        // Sprint 2 TODO: NetworkBehaviour'a cevir, NetworkTransform ile pozisyon sync et.
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            if (Agent != null) Agent.enabled = false;
            this.enabled = false;
            return;
        }

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            PlayerTransform = playerObj.transform;
        else
            Debug.LogWarning("[EnemyController] 'Player' tag'li obje bulunamadı.");

        SwitchBehavior(new PatrolBehavior());
    }

    public bool CanSeePlayer()
    {
        if (PlayerTransform == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = PlayerTransform.position + Vector3.up;
        Vector3 direction = targetPos - eyePos;
        float distance = direction.magnitude;

        if (distance > _sightRange) return false;

        if (Physics.Raycast(eyePos, direction.normalized, out RaycastHit hit, distance))
            return hit.transform == PlayerTransform || hit.transform.IsChildOf(PlayerTransform);

        return true;
    }

    private void Update()
    {
        _current?.Tick(this);
    }

    private void OnDestroy()
    {
        _current?.Exit(this);
        GameEventBus.Publish(new EnemyDiedEvent(GetInstanceID(), transform.position));
    }

    public void SwitchBehavior(IEnemyBehavior next)
    {
        _current?.Exit(this);
        _current = next;
        _current.Enter(this);
    }

    public void SetBlinded(bool blinded, float duration)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        var netState = GetComponent<EnemyNetState>();
        if (netState != null && blinded)
            netState.ServerSetBlinded(duration);

        // Korlestirildiyse tehdit kaynagindan kac (oyuncu varsa onun konumunu, yoksa kendi konumunu kullan).
        if (blinded)
        {
            Vector3 threat = PlayerTransform != null
                ? PlayerTransform.position
                : transform.position;

            SwitchBehavior(new FleeBehavior(threat, duration > 0f ? duration : 3f));
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (_patrolWaypoints == null) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < _patrolWaypoints.Length; i++)
        {
            if (_patrolWaypoints[i] == null) continue;
            Gizmos.DrawSphere(_patrolWaypoints[i].position, 0.25f);
            int next = (i + 1) % _patrolWaypoints.Length;
            if (_patrolWaypoints[next] != null)
                Gizmos.DrawLine(_patrolWaypoints[i].position, _patrolWaypoints[next].position);
        }

        if (PlayerTransform != null && CanSeePlayer())
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, PlayerTransform.position + Vector3.up);
        }

        Gizmos.color = new Color(1f, 1f, 0f, 0.08f);
        Gizmos.DrawSphere(transform.position, _sightRange);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawSphere(transform.position, _noiseDetectionRadius);
    }
#endif
}
