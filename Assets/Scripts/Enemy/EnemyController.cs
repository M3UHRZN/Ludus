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
        SwitchBehavior(new PatrolBehavior());
    }

    private void Update()
    {
        _current?.Tick(this);
    }

    private void OnDestroy()
    {
        _current?.Exit(this);
    }

    public void SwitchBehavior(IEnemyBehavior next)
    {
        _current?.Exit(this);
        _current = next;
        _current.Enter(this);
    }

    public void SetBlinded(bool blinded, float duration)
    {
        // TODO Sprint 2: FleeBehavior entegrasyonu
    }
}
