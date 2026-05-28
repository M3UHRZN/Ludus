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
    [Tooltip("Type A (robot) maksimum cani. 0'a inerse Destroy + EnemyDiedEvent.")]
    [SerializeField] private float _maxHealth = 60f;
    [Tooltip("Type B (priest) maksimum cani. Robotun cok ustunde: priest 'boss' tier — " +
             "oldurulebilir ama buyuk koordinasyon gerektirir. Sadece IsPriest ise kullanilir.")]
    [SerializeField] private float _priestMaxHealth = 1000f;

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

    [Header("Priest Ambient (sadece Type B icin)")]
    [Tooltip("Priest etrafinda loop'lu calan ambient klip (zil/diapaz). Bos birakilirsa sessiz kalir. " +
             "3D spatial audio kullanilir: yaklasinca yukselir, uzaklasinca solar.")]
    [SerializeField] private AudioClip _priestAmbientClip;
    [Tooltip("Ambient klibin temel ses seviyesi.")]
    [Range(0f, 1f)]
    [SerializeField] private float _priestAmbientVolume = 0.55f;
    [Tooltip("Ses tam ses seviyesinde duyulmaya basladigi en yakin mesafe.")]
    [SerializeField] private float _priestAmbientMinDistance = 2f;
    [Tooltip("Sesin tamamen kayboldugu en uzak mesafe.")]
    [SerializeField] private float _priestAmbientMaxDistance = 18f;

    public GameObject ProjectilePrefab => _projectilePrefab;
    public Transform FirePoint => _firePoint;
    public GameObject MuzzleFlashPrefab => _muzzleFlashPrefab;

    /// <summary>
    /// True ise bu enemy yakin dovus / "priest" tipi (Type B): sesi duyar, esya
    /// alimini sezer, ambient klip calar. False ise uzaktan saldiri (Type A robot):
    /// sagir + sezgi yok + ambient ses yok. FearSystem priest yakinligini bu
    /// flag uzerinden filtreler.
    /// </summary>
    public bool IsPriest => !_useRangedAttack;

    public NavMeshAgent Agent { get; private set; }
    public Transform[] PatrolWaypoints => _patrolWaypoints;

    // --- Hedef oncelik state'i (multiplayer adaptasyonu) ---
    // 3 katmanli oncelik: damage source (en yuksek, kisa sure) -> carrier hint
    // (orta, davranisin refresh ettigi sure boyunca) -> closest alive + hysteresis.
    private const float TargetSwitchAdvantage = 3f;   // closest fallback'inda yeni hedefe gecmek icin gereken m avantaji
    private const float DamageTargetDuration  = 4f;   // hasar veren oyuncu kac saniye oncelikli kalir

    private ulong _damageTargetClientId;
    private float _damageTargetExpiry;
    private bool  _hasDamageTarget;

    private ulong _carrierTargetClientId;
    private float _carrierTargetExpiry;
    private bool  _hasCarrierTarget;

    private Transform _lastClosestTarget;   // hysteresis'te onceki secimi tut

    /// <summary>
    /// Hedef oncelik zinciri: (1) son 4sn icinde hasar veren oyuncu varsa, (2) yoksa
    /// taraflanmis carrier hint (LureBehavior tarafindan refreshlenir) varsa, (3) yoksa
    /// en yakin canli oyuncu — 3m'lik hysteresis ile flap onlenir.
    /// Multiplayer'da bu sayede:
    /// - 2 oyuncu esit mesafede dururken kafa titremesi olmaz
    /// - Oyuncu A robotu vurursa robot B'yi birakip A'ya doner (4sn)
    /// - Priest lure'da carrier'i takip eder, Chase'e gecince hint korunur
    /// Hicbir canli oyuncu yoksa null doner.
    /// </summary>
    public Transform PlayerTransform
    {
        get
        {
            float now = Time.time;

            // 1) Damage source priority — hasar veren son 4sn oncelikli
            if (_hasDamageTarget && now < _damageTargetExpiry)
            {
                var dmgPlayer = PlayerStateMachine.GetServerPlayer(_damageTargetClientId);
                if (dmgPlayer != null) return dmgPlayer.transform;
                _hasDamageTarget = false;
            }
            else _hasDamageTarget = false;

            // 2) Carrier hint — LureBehavior / ChaseBehavior tarafindan refresh edilir
            if (_hasCarrierTarget && now < _carrierTargetExpiry)
            {
                var carrier = PlayerStateMachine.GetServerPlayer(_carrierTargetClientId);
                if (carrier != null) return carrier.transform;
                _hasCarrierTarget = false;
            }
            else _hasCarrierTarget = false;

            // 3) Closest alive + hysteresis
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

        // Hysteresis: onceki hedefi yeterince yakindaysa koru, flap onle
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

    /// <summary>
    /// Hasar kaynagini 4sn boyunca oncelikli hedef olarak isaretle. TakeDamage
    /// otomatik cagirir; attacker server'da bilinen bir canli oyuncu degilse no-op.
    /// </summary>
    public void SetDamageTargetPriority(ulong attackerClientId)
    {
        var attacker = PlayerStateMachine.GetServerPlayer(attackerClientId);
        if (attacker == null) return;
        _damageTargetClientId = attackerClientId;
        _damageTargetExpiry = Time.time + DamageTargetDuration;
        _hasDamageTarget = true;
    }

    /// <summary>
    /// Carrier hint: LureBehavior'in takip ettigi oyuncuyu, ChaseBehavior'a
    /// devredildiginde de hedef olarak korumak icin kullanilir. Duration boyunca
    /// PlayerTransform damage prio yoksa bu oyuncuyu doner.
    /// </summary>
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

    /// <summary>Aktif davranisi disardan sorgulamak icin (orn. OnItemPickedUp idempotency).</summary>
    public IEnemyBehavior CurrentBehavior => _current;
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

    // Saglik (IDamageable implementasyonu)
    private float _currentHealth;
    private float _effectiveMaxHealth;   // tipe gore secilmis maksimum (Start'ta hesaplanir)
    public bool IsAlive => _currentHealth > 0f;
    public float CurrentHealth => _currentHealth;
    public float MaxHealth => _effectiveMaxHealth;

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        ConfigurePriestAmbientAudio();
    }

    /// <summary>
    /// Type B (priest) icin runtime 3D AudioSource olusturup loop'lu calar. Awake'te
    /// kosulur boylece hem host hem tum client'lar kendi taraflarinda sesi duyar
    /// (network sync gerekmez — pozisyon zaten NetworkTransform ile sync). Klip
    /// atanmamissa veya enemy robot ise no-op.
    /// </summary>
    private void ConfigurePriestAmbientAudio()
    {
        if (!IsPriest) return;
        if (_priestAmbientClip == null) return;

        var src = gameObject.AddComponent<AudioSource>();
        src.clip = _priestAmbientClip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;                           // tamamen 3D
        src.volume = _priestAmbientVolume;
        src.minDistance = _priestAmbientMinDistance;
        src.maxDistance = _priestAmbientMaxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;       // Linear: tahmin edilebilir azalim
        src.dopplerLevel = 0f;                            // priest hareketi pitch'i bozmasin
        src.Play();
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
        // Type B (priest) "boss" tier: cok daha tank — Type A robotun ufak
        // _maxHealth'i kullanmasi yerine kendi _priestMaxHealth alanini alir.
        _effectiveMaxHealth = IsPriest ? _priestMaxHealth : _maxHealth;
        _currentHealth = _effectiveMaxHealth;
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
    /// Saldiri davranisi fabrikasi. Type A (robot) once kirmizi lazerle nisan alir
    /// (RangedAimBehavior), telegraph dolunca RangedAttackBehavior'a devreder.
    /// Type B (priest) yakin dovus yapar (AttackBehavior). ChaseBehavior her yeni
    /// engagement'ta bunu cagirir; yani lazer her temasta bastan oynar.
    /// </summary>
    public IEnemyBehavior CreateAttackBehavior()
    {
        return _useRangedAttack ? new RangedAimBehavior() : new AttackBehavior();
    }

    // Oyuncunun ayak (PlayerTransform.position) hizasindan yukari dogru ornekleme
    // noktalari. Kisa engellerin (ornk masa, alcak duvar) arkasinda sadece basi
    // cikan oyuncuyu yakalamak ve come durumunda raycast'in basinin uzerinden
    // gecip "goruldu" sanmasini engellemek icin birden fazla nokta deneriz; biri
    // bile engelsiz gorus saglarsa oyuncu goruldu sayilir.
    private static readonly float[] s_visibilitySampleHeights = { 1.7f, 1.0f, 0.3f };

    public bool CanSeePlayer()
    {
        if (PlayerTransform == null) return false;

        Vector3 eyePos = transform.position + Vector3.up * 1.5f;
        Vector3 playerBase = PlayerTransform.position;

        // Birden fazla ornekleme noktasi: bas / govde / ayak hizasi. Her nokta icin
        // mesafe + FOV koni + LOS raycast tek tek denenir; engelsiz bir yol bulunan
        // ilk noktada "goruldu" doneriz. Boylece tek bir nokta (eski sampling) icin
        // olusan kor noktalar (kisa duvarin uzerinden sadece bas gorunmesi, come
        // durumunda raycast'in basin ustunden gecmesi) kapanir.
        for (int i = 0; i < s_visibilitySampleHeights.Length; i++)
        {
            Vector3 targetPos = playerBase + Vector3.up * s_visibilitySampleHeights[i];
            Vector3 direction = targetPos - eyePos;
            float distance = direction.magnitude;
            if (distance > _sightRange) continue;
            if (distance < 0.0001f) continue;

            // Gorus konisi: TUM dusmanlar (hem Type A hem Type B) sadece yuzunun
            // baktigi koni icini gorur. Arkadan/yandan yaklasilirsa fark etmezler.
            Vector3 flatDir = direction;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.0001f &&
                Vector3.Angle(transform.forward, flatDir) > _fieldOfViewAngle * 0.5f)
                continue;

            if (!Physics.Raycast(eyePos, direction.normalized, out RaycastHit hit, distance))
                return true; // hicbir engele degmedi; bos havadan direkt goruldu

            if (hit.transform == PlayerTransform || hit.transform.IsChildOf(PlayerTransform))
                return true; // engele degil player'in kendi collider'ina vurdu
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
    /// GameEventBus uzerinden grab olaylarini dinler. Type A (robot) sagirdir;
    /// Type B (priest) ise grab'i haritanin neresinde olursa olsun "sezer" ve
    /// LureBehavior'a gecerek tasiyiciyi kovalar (priest dogaustu sezgi —
    /// mesafe sinirsiz). Halihazirda oyuncuyu net goruyorsa veya zaten Lure
    /// davranisindaysa olay yutulur — Lure HeldItems registry'sinden tum
    /// tasiyicilari zaten goruyor ve en yakini secebiliyor.
    /// </summary>
    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (_useRangedAttack) return;                       // Type A grab sezmez
        if (evt.Item == null) return;                        // konum/ref bilgisi olmayan eski yayinlar
        if (!IsAlive) return;

        // Halihazirda oyuncuyu net goruyorsa daha guclu sinyal var; lure bunu bozmasin.
        if (CanSeePlayer()) return;

        // Zaten Lure'daysa yeni LureBehavior insa etmiyoruz — mevcut Lure
        // registry'den yeni tasiyiciyi de gorur, gerekirse hedef switch eder.
        if (_current is LureBehavior) return;

        SwitchBehavior(new LureBehavior());
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

    /// <summary>
    /// Loot odası bekçisi olarak yapılandır: wander mode kapali (oda disina cikmaz),
    /// gorus mesafesi kisaltilmis (oyuncu sneak edebilsin), ranged saldiri tetik
    /// mesafesi kucultulmus, waypoint'ler odaya ait noktalar. EnemySpawner loot
    /// odalarini bulduktan sonra bu fabrikatik metodu cagirir; davranis kendisi
    /// (Strategy) degismez, sadece konfigurasyon parametreleri farklilar.
    /// </summary>
    public void SetupAsLootGuardian(float sightRange, float rangedAttackRange, Transform[] roomWaypoints)
    {
        _useWanderMode = false;
        if (sightRange > 0f) _sightRange = sightRange;
        if (rangedAttackRange > 0f) _rangedAttackRange = rangedAttackRange;
        if (roomWaypoints != null && roomWaypoints.Length > 0)
            SetWaypoints(roomWaypoints);
        Debug.Log($"[EnemyController] Loot guardian olarak yapilandirildi (sight={_sightRange}, attack={_rangedAttackRange}, waypoints={(_patrolWaypoints != null ? _patrolWaypoints.Length : 0)}).");
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

    /// <summary>
    /// IDamageable — disaridan hasar al (oyuncunun firlattigi obje, mermi vb.).
    /// Server-only kosulur. HP 0'a inerse dusman yok edilir; OnDestroy EnemyDiedEvent
    /// yayinlar, EnemySpawner alive sayisini gunceller.
    /// </summary>
    public void TakeDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!IsAlive) return;
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - amount);
        Debug.Log($"[EnemyController] Hasar alindi: {amount:F0} (kalan: {_currentHealth:F0}/{_effectiveMaxHealth:F0})");

        // Hasar veren oyuncuyu 4sn icin oncelikli hedef yap — "beni vurdun, ben sana donerim"
        // multiplayer'da arkadan vur-kac taktigini kirar, AI tepkisel hisset.
        SetDamageTargetPriority(attackerClientId);

        if (_currentHealth <= 0f)
        {
            Debug.Log("[EnemyController] Dusman oldu.");
            // Destroy'i bir sonraki frame'e ertele: TakeDamage'i tetikleyen kaynak
            // bir physics callback'i olabilir (orn. PhysicsObject.OnCollisionEnter);
            // o callback icinde gameObject Destroy etmek frame stall / donma yapar.
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
