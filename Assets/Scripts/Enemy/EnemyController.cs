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
    [SerializeField] private float _attackRange = 1.8f;
    [SerializeField] private float _noiseDetectionRadius = 18f;
    [SerializeField] private LayerMask _playerLayer;

    [Header("Davranis Modu")]
    [Tooltip("True ise enemy spawn'da WanderingBehavior (tum haritada gezen) ile baslar. " +
             "False ise PatrolBehavior (oda waypoint turu). Type B dusman icin true.")]
    [SerializeField] private bool _useWanderMode = false;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] PatrolWaypoints => _patrolWaypoints;
    public Transform PlayerTransform { get; private set; }
    public Transform CurrentTarget { get; set; }
    public int CurrentWaypointIndex { get; set; }
    public bool HeardNoise { get; set; }
    public Vector3 LastNoisePosition { get; private set; }

    /// <summary>ChaseBehavior'in AttackBehavior'a gectigi mesafe.</summary>
    public float AttackTriggerRange => _attackRange;

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

        SwitchBehavior(CreateDefaultBehavior());
    }

    /// <summary>
    /// Enemy'nin "bos" durumdaki varsayilan davranisi. Type A patrol, Type B wander.
    /// Chase / Flee davranislari isleri bittiginde Patrol yerine bunu cagirir,
    /// boylece her enemy tipi kendi temel davranisina doner.
    /// </summary>
    public IEnemyBehavior CreateDefaultBehavior()
    {
        return _useWanderMode ? new WanderingBehavior() : new PatrolBehavior();
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

    private void OnEnable()
    {
        GameEventBus.Subscribe<NoiseEmittedEvent>(OnNoiseEvent);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<NoiseEmittedEvent>(OnNoiseEvent);
    }

    private void OnDestroy()
    {
        _current?.Exit(this);
        GameEventBus.Publish(new EnemyDiedEvent(GetInstanceID(), transform.position));
    }

    /// <summary>
    /// GameEventBus uzerinden ses olaylarini dinler. Yayinlanan ses
    /// hem ses kaynaginin kendi range'i hem de dusmanin noiseDetectionRadius'u
    /// icindeyse HeardNoise tetiklenir, PatrolBehavior bunu Chase'e cevirir.
    /// </summary>
    private void OnNoiseEvent(NoiseEmittedEvent evt)
    {
        OnNoiseHeard(evt.Position, evt.Range);
    }

    /// <summary>
    /// Manuel olarak ses kaynagi bildirimi yapmak isteyen sistemler
    /// (test scriptleri, direkt cagri) bunu kullanabilir.
    /// </summary>
    public void OnNoiseHeard(Vector3 source, float sourceRange)
    {
        float dist = Vector3.Distance(transform.position, source);

        // Ses menzili belirleyici: kosma (genis menzil) uzaktan, yurume (dar) yakindan
        // duyulur. Comelmede oyuncu hic ses yaymaz, bu metod cagrilmaz.
        if (dist > sourceRange) return;

        HeardNoise = true;
        LastNoisePosition = source;
        Debug.Log($"[EnemyController] Ses algilandi (mesafe={dist:F1}, menzil={sourceRange:F0}).");
    }

    public void SwitchBehavior(IEnemyBehavior next)
    {
        _current?.Exit(this);
        _current = next;
        _current.Enter(this);
    }

    /// <summary>
    /// EnemySpawner tarafindan runtime'da cagrilir. Inspector'dan elle
    /// atamak yerine harita uretildikten sonra otomatik baglanir.
    /// </summary>
    public void SetWaypoints(Transform[] waypoints)
    {
        _patrolWaypoints = waypoints;
        CurrentWaypointIndex = 0;
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
