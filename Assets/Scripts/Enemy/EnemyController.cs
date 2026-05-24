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

    [Header("Gorus Konisi")]
    [Tooltip("TUM dusmanlar sadece bu acidaki koni icinde (yuzunun baktigi yer) gorur; " +
             "arkadan/yandan yaklasinca fark etmezler. Ek olarak ranged (Type A robot) dusman " +
             "sese SAGIRDIR, yakin dovus (Type B) ise sesi de duyar. Ses farki _useRangedAttack'tan gelir.")]
    [Range(30f, 360f)]
    [SerializeField] private float _fieldOfViewAngle = 110f;

    [Header("Davranis Modu")]
    [Tooltip("True ise enemy spawn'da WanderingBehavior (tum haritada gezen) ile baslar. " +
             "False ise PatrolBehavior (oda waypoint turu). Type B dusman icin true.")]
    [SerializeField] private bool _useWanderMode = false;

    [Tooltip("True ise uzaktan kursun atar (RangedAttackBehavior). False ise yakin dovus " +
             "(AttackBehavior). Type A (robot) icin true, Type B (priest) icin false.")]
    [SerializeField] private bool _useRangedAttack = false;

    [Tooltip("Uzaktan saldiri tetik mesafesi (sadece _useRangedAttack true ise gecerli)")]
    [SerializeField] private float _rangedAttackRange = 12f;

    [Tooltip("Ranged enemy'nin attigi mermi prefab'i (NetworkObject + EnemyProjectile). " +
             "Bos birakilirsa gorunmez hitscan kullanilir.")]
    [SerializeField] private GameObject _projectilePrefab;

    [Tooltip("Merminin ciktigi namlu noktasi (bos ise govdenin ust-on kismi kullanilir)")]
    [SerializeField] private Transform _firePoint;

    [Tooltip("Atis aninda namluda kisa sure gorunen efekt prefab'i (opsiyonel)")]
    [SerializeField] private GameObject _muzzleFlashPrefab;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public Transform FirePoint => _firePoint;
    public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] PatrolWaypoints => _patrolWaypoints;
    /// <summary>
    /// Sunucu tarafindan canli en yakin oyuncuya isaret eder. Multiplayer'da bu
    /// her erisimde yeniden hesaplanir; tek bir Start() snapshot'i degildir.
    /// Hicbir canli oyuncu yoksa null doner — tum tuketicilerin null kontrolu
    /// var (CanSeePlayer, AttackBehavior, ChaseBehavior, FleeBehavior).
    /// </summary>
    public Transform PlayerTransform
    {
        get
        {
            var players = PlayerStateMachine.ServerPlayers;
            if (players == null || players.Count == 0) return null;

            Transform best = null;
            float bestSqr = float.PositiveInfinity;
            Vector3 here = transform.position;

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;          // despawn arasi null slot
                if (!p.IsAlive) continue;          // olu oyuncuyu hedef alma

                float sqr = (p.transform.position - here).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = p.transform;
                }
            }
            return best;
        }
    }
    public Transform CurrentTarget { get; set; }
    public int CurrentWaypointIndex { get; set; }
    public bool HeardNoise { get; set; }
    public Vector3 LastNoisePosition { get; private set; }

    /// <summary>ChaseBehavior'in saldiri davranisina gectigi mesafe (ranged ise daha uzak).</summary>
    public float AttackTriggerRange => _useRangedAttack ? _rangedAttackRange : _attackRange;

    private IEnemyBehavior _current;

    // Gecici hiz dusurme (orn. guclu vurus sonrasi toparlanma) — davranistan bagimsiz,
    // EnemyController yonetir ki davranis degisse de (Attack->Chase) etki devam etsin.
    private float _baseAgentSpeed;
    private float _slowTimer;
    private bool  _isSlowed;

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

        _baseAgentSpeed = Agent != null ? Agent.speed : 3.5f;
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

    /// <summary>
    /// Saldiri davranisi fabrikasi. Type A (robot) uzaktan kursun atar
    /// (RangedAttackBehavior), Type B (priest) yakin dovus yapar (AttackBehavior).
    /// ChaseBehavior yakinlasinca bunu cagirir.
    /// </summary>
    public IEnemyBehavior CreateAttackBehavior()
    {
        return _useRangedAttack ? new RangedAttackBehavior() : new AttackBehavior();
    }

    public bool CanSeePlayer()
    {
        if (PlayerTransform == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 targetPos = PlayerTransform.position + Vector3.up;
        Vector3 direction = targetPos - eyePos;
        float distance = direction.magnitude;

        if (distance > _sightRange) return false;

        // Gorus konisi: TUM dusmanlar (hem Type A hem Type B) sadece yuzunun baktigi
        // koni icini gorur. Arkadan/yandan yaklasilirsa fark etmezler.
        Vector3 flatDir = direction;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.0001f &&
            Vector3.Angle(transform.forward, flatDir) > _fieldOfViewAngle * 0.5f)
            return false;

        if (Physics.Raycast(eyePos, direction.normalized, out RaycastHit hit, distance))
            return hit.transform == PlayerTransform || hit.transform.IsChildOf(PlayerTransform);

        return true;
    }

    private void Update()
    {
        TickSelfSlow();
        _current?.Tick(this);
    }

    private void TickSelfSlow()
    {
        if (!_isSlowed) return;
        _slowTimer -= Time.deltaTime;
        if (_slowTimer <= 0f)
        {
            if (Agent != null) Agent.speed = _baseAgentSpeed;
            _isSlowed = false;
        }
    }

    /// <summary>
    /// Dusmanin hareket hizini gecici olarak dusurur (orn. guclu vurus sonrasi
    /// toparlanma). Sure boyunca Agent.speed = base * multiplier; sure bitince
    /// eski hizina doner. Davranis degisse bile (Attack->Chase) etki surer.
    /// </summary>
    public void ApplySelfSlow(float multiplier, float duration)
    {
        if (Agent == null) return;
        Agent.speed = _baseAgentSpeed * Mathf.Clamp01(multiplier);
        _slowTimer = duration;
        _isSlowed = true;
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
        // Ranged (Type A robot) dusman sese SAGIRDIR; sadece gorusle algilar.
        if (_useRangedAttack) return;

        float dist = Vector3.Distance(transform.position, source);

        // Ses menzili belirleyici: kosma (genis menzil) uzaktan, yurume (dar) yakindan
        // duyulur. Comelmede oyuncu hic ses yaymaz, bu metod cagrilmaz.
        if (dist > sourceRange) return;

        // Sadece ilk algilamada logla (her footstep'te spam olmasin).
        // Davranis gecisi (Chase'e gecis) zaten Patrol/Wander log'unda gorunur.
        if (!HeardNoise)
            Debug.Log($"[EnemyController] Ses algilandi (mesafe={dist:F1}, menzil={sourceRange:F0}).");

        HeardNoise = true;
        LastNoisePosition = source;
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

        // Gorus konisi kenarlari (tum dusmanlar)
        Gizmos.color = Color.yellow;
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 left  = Quaternion.Euler(0f, -_fieldOfViewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f,  _fieldOfViewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(eye, left  * _sightRange);
        Gizmos.DrawRay(eye, right * _sightRange);

        // Ses algilama alani (sadece sesi duyan yakin dovus Type B dusmanda gosterilir)
        if (!_useRangedAttack)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, _noiseDetectionRadius);
        }
    }
#endif
}
