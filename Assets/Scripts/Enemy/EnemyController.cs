using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour, IDamageable
{
    [Header("Patrol")]
    [SerializeField] private Transform[] _patrolWaypoints;

    [Header("Detection")]
    [SerializeField] private float _sightRange = 15f;
    [SerializeField] private float _attackRange = 1.8f;
    [SerializeField] private float _noiseDetectionRadius = 18f;
    [SerializeField] private LayerMask _playerLayer;

    [Header("Saglik")]
    [SerializeField] private float _maxHealth = 60f;
    [SerializeField] private float _priestMaxHealth = 1000f;

    [Header("Gorus Konisi")]
    [Range(30f, 360f)]
    [SerializeField] private float _fieldOfViewAngle = 110f;

    [Header("Davranis Modu")]
    [SerializeField] private bool _useWanderMode = false;
    [SerializeField] private bool _useRangedAttack = false;
    [SerializeField] private float _rangedAttackRange = 12f;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private GameObject _muzzleFlashPrefab;

    [Header("Priest Ambient (Type B)")]
    [SerializeField] private AudioClip _priestAmbientClip;
    [Range(0f, 1f)]
    [SerializeField] private float _priestAmbientVolume = 0.55f;
    [SerializeField] private float _priestAmbientMinDistance = 2f;
    [SerializeField] private float _priestAmbientMaxDistance = 18f;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public Transform FirePoint => _firePoint;
    public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;

    // True ise Type B (priest, yakin dovus, sesi duyar). False ise Type A (robot, ranged, sagir).
    public bool IsPriest => !_useRangedAttack;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] PatrolWaypoints => _patrolWaypoints;

    // 3 katmanli hedef oncelik: damage source -> carrier hint -> closest + hysteresis
    private const float TargetSwitchAdvantage = 3f;
    private const float DamageTargetDuration = 4f;

    private ulong _damageTargetClientId;
    private float _damageTargetExpiry;
    private bool _hasDamageTarget;

    private ulong _carrierTargetClientId;
    private float _carrierTargetExpiry;
    private bool _hasCarrierTarget;

    private Transform _lastClosestTarget;

    public Transform PlayerTransform
    {
        get
        {
            float now = Time.time;

            // 1) Hasar veren oyuncu 4sn oncelikli
            if (_hasDamageTarget && now < _damageTargetExpiry)
            {
                var dmgPlayer = PlayerStateMachine.GetServerPlayer(_damageTargetClientId);
                if (dmgPlayer != null) return dmgPlayer.transform;
                _hasDamageTarget = false;
            }
            else _hasDamageTarget = false;

            // 2) Carrier hint (Lure'dan / Chase'e devirde)
            if (_hasCarrierTarget && now < _carrierTargetExpiry)
            {
                var carrier = PlayerStateMachine.GetServerPlayer(_carrierTargetClientId);
                if (carrier != null) return carrier.transform;
                _hasCarrierTarget = false;
            }
            else _hasCarrierTarget = false;

            // 3) En yakin canli + 3m hysteresis
            return ResolveClosestWithHysteresis();
        }
    }

    private Transform ResolveClosestWithHysteresis()
    {
        var players = PlayerStateMachine.ServerPlayers;
        if (players == null || players.Count == 0)
        {
            _lastClosestTarget = null;
            return null;
        }

        Transform best = null;
        float bestSqr = float.PositiveInfinity;
        Vector3 here = transform.position;

        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null) continue;
            if (!p.IsAlive) continue;

            float sqr = (p.transform.position - here).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = p.transform;
            }
        }

        if (best == null) { _lastClosestTarget = null; return null; }

        if (_lastClosestTarget == null || !IsTransformAliveTarget(_lastClosestTarget))
        {
            _lastClosestTarget = best;
            return best;
        }
        if (best == _lastClosestTarget) return best;

        float lastSqr = (_lastClosestTarget.position - here).sqrMagnitude;
        float advantageSqr = TargetSwitchAdvantage * TargetSwitchAdvantage;
        if (lastSqr - bestSqr > advantageSqr)
        {
            _lastClosestTarget = best;
            return best;
        }

        return _lastClosestTarget;
    }

    private static bool IsTransformAliveTarget(Transform t)
    {
        if (t == null) return false;
        var psm = t.GetComponent<PlayerStateMachine>()
                  ?? t.GetComponentInParent<PlayerStateMachine>();
        return psm != null && psm.IsAlive;
    }

    public void SetDamageTargetPriority(ulong attackerClientId)
    {
        var attacker = PlayerStateMachine.GetServerPlayer(attackerClientId);
        if (attacker == null) return;
        _damageTargetClientId = attackerClientId;
        _damageTargetExpiry = Time.time + DamageTargetDuration;
        _hasDamageTarget = true;
    }

    public void SetCarrierTargetHint(ulong carrierClientId, float duration)
    {
        if (duration <= 0f) { _hasCarrierTarget = false; return; }
        var carrier = PlayerStateMachine.GetServerPlayer(carrierClientId);
        if (carrier == null) return;
        _carrierTargetClientId = carrierClientId;
        _carrierTargetExpiry = Time.time + duration;
        _hasCarrierTarget = true;
    }

    public void ClearCarrierTargetHint() => _hasCarrierTarget = false;

    public IEnemyBehavior CurrentBehavior => _current;
    public Transform CurrentTarget { get; set; }
    public int CurrentWaypointIndex { get; set; }
    public bool HeardNoise { get; set; }
    public Vector3 LastNoisePosition { get; private set; }

    public float AttackTriggerRange => _useRangedAttack ? _rangedAttackRange : _attackRange;

    private IEnemyBehavior _current;

    private float _baseAgentSpeed;
    private float _slowTimer;
    private bool _isSlowed;

    private float _currentHealth;
    private float _effectiveMaxHealth;
    public bool IsAlive => _currentHealth > 0f;
    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _effectiveMaxHealth;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        ConfigurePriestAmbientAudio();
    }

    private void ConfigurePriestAmbientAudio()
    {
        if (!IsPriest) return;
        if (_priestAmbientClip == null) return;

        var src = gameObject.AddComponent<AudioSource>();
        src.clip = _priestAmbientClip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.volume = _priestAmbientVolume;
        src.minDistance = _priestAmbientMinDistance;
        src.maxDistance = _priestAmbientMaxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.dopplerLevel = 0f;
        src.Play();
    }

    private void Start()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            if (Agent != null) Agent.enabled = false;
            this.enabled = false;
            return;
        }

        _baseAgentSpeed = Agent != null ? Agent.speed : 3.5f;
        _effectiveMaxHealth = IsPriest ? _priestMaxHealth : _maxHealth;
        _currentHealth = _effectiveMaxHealth;
        SwitchBehavior(CreateDefaultBehavior());
    }

    public IEnemyBehavior CreateDefaultBehavior()
    {
        return _useWanderMode ? new WanderingBehavior() : new PatrolBehavior();
    }

    public IEnemyBehavior CreateAttackBehavior()
    {
        return _useRangedAttack ? new RangedAimBehavior() : new AttackBehavior();
    }

    // Bas/govde/ayak hizasinda 3 noktadan LOS denemesi (alcak engellerin arkasinda hala goruyor mu)
    private static readonly float[] s_visibilitySampleHeights = { 1.7f, 1.0f, 0.3f };

    public bool CanSeePlayer()
    {
        if (PlayerTransform == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 playerBase = PlayerTransform.position;

        for (int i = 0; i < s_visibilitySampleHeights.Length; i++)
        {
            Vector3 targetPos = playerBase + Vector3.up * s_visibilitySampleHeights[i];
            Vector3 direction = targetPos - eyePos;
            float distance = direction.magnitude;
            if (distance > _sightRange) continue;
            if (distance < 0.0001f) continue;

            // FOV koni testi
            Vector3 flatDir = direction;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flatDir) > _fieldOfViewAngle * 0.5f)
                continue;

            if (!Physics.Raycast(eyePos, direction.normalized, out RaycastHit hit, distance))
                return true;

            if (hit.transform == PlayerTransform || hit.transform.IsChildOf(PlayerTransform))
                return true;
        }

        return false;
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
        GameEventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<NoiseEmittedEvent>(OnNoiseEvent);
        GameEventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
    }

    private void OnDestroy()
    {
        _current?.Exit(this);
        GameEventBus.Publish(new EnemyDiedEvent(GetInstanceID(), transform.position));
    }

    private void OnNoiseEvent(NoiseEmittedEvent evt)
    {
        OnNoiseHeard(evt.Position, evt.Range);
    }

    // Priest item alimi sezer, robot sezmez. Halihazirda goruyorsa veya Lure'daysa atla.
    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_useRangedAttack) return;
        if (evt.Item == null) return;
        if (!IsAlive) return;

        if (CanSeePlayer()) return;
        if (_current is LureBehavior) return;

        SwitchBehavior(new LureBehavior());
    }

    public void OnNoiseHeard(Vector3 source, float sourceRange)
    {
        // Robot sese sagir
        if (_useRangedAttack) return;

        float dist = Vector3.Distance(transform.position, source);
        if (dist > sourceRange) return;

        // Spam onleme, sadece ilk algida log
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

    public void SetWaypoints(Transform[] waypoints)
    {
        _patrolWaypoints = waypoints;
        CurrentWaypointIndex = 0;
    }

    public void SetupAsLootGuardian(float sightRange, float rangedAttackRange, Transform[] roomWaypoints)
    {
        _useWanderMode = false;
        if (sightRange > 0f) _sightRange = sightRange;
        if (rangedAttackRange > 0f) _rangedAttackRange = rangedAttackRange;
        if (roomWaypoints != null && roomWaypoints.Length > 0)
            SetWaypoints(roomWaypoints);
        Debug.Log($"[EnemyController] Loot guardian yapilandirildi (sight={_sightRange}, attack={_rangedAttackRange}, waypoints={(_patrolWaypoints != null ? _patrolWaypoints.Length : 0)}).");
    }

    public void SetBlinded(bool blinded, float duration)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        var netState = GetComponent<EnemyNetState>();
        if (netState != null && blinded)
            netState.ServerSetBlinded(duration);

        if (blinded)
        {
            Vector3 threat = PlayerTransform != null
                ? PlayerTransform.position
                : transform.position;

            SwitchBehavior(new FleeBehavior(threat, duration > 0f ? duration : 3f));
        }
    }

    public void SetStunned(bool stunned, float duration)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        if (stunned && Agent != null)
        {
            Agent.isStopped = true;
            Invoke(nameof(ResumeFromStun), duration > 0f ? duration : 2f);
            Debug.Log($"[EnemyController] Stun applied ({duration}s)");
        }
    }

    private void ResumeFromStun()
    {
        if (Agent != null)
            Agent.isStopped = false;

        SwitchBehavior(CreateDefaultBehavior());
        Debug.Log("[EnemyController] Stun ended, returning to default behavior.");
    }

    public void TakeDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!IsAlive) return;
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        Debug.Log($"[EnemyController] Hasar alindi: {amount:F0} (kalan: {_currentHealth:F0}/{_effectiveMaxHealth:F0})");

        // Hasar veren oyuncu 4sn oncelikli hedef
        SetDamageTargetPriority(attackerClientId);

        if (_currentHealth <= 0f)
        {
            Debug.Log("[EnemyController] Dusman oldu.");
            // Destroy'i bir frame ertele, physics callback icinde patlamasin
            Invoke(nameof(DieDelayed), 0f);
        }
    }

    private void DieDelayed()
    {
        Destroy(gameObject);
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

        Gizmos.color = Color.yellow;
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 left = Quaternion.Euler(0f, -_fieldOfViewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, _fieldOfViewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawRay(eye, left * _sightRange);
        Gizmos.DrawRay(eye, right * _sightRange);

        if (!_useRangedAttack)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            Gizmos.DrawSphere(transform.position, _noiseDetectionRadius);
        }
    }
#endif
}
