using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class PhysicsObject : NetworkBehaviour, IGrabbable, IInteractable
{
    private const ulong NoGrabberClientId = ulong.MaxValue;

    [Header("Grab Ayarlari")]
    public float grabDistance = 4f;
    public float holdSpringStrength = 150f;
    public float holdDamping = 12f;
    public float throwForceMultiplier = 12f;

    [Header("Item")]
    [SerializeField] private float weight = 1f;

    [Header("Gorsel Geribildirim")]
    public Material highlightMaterial;

    [Header("Firlatma Hasari")]
    [Tooltip("Bir firlatmanin 'hasarli' kabul edildigi pencere (sn). Bu sure icinde dusmana carparsa hasar verir.")]
    [SerializeField] private float _throwDamageWindow = 2f;
    [Tooltip("Hasar verebilmesi icin minimum carpma hizi (m/s). Yavasca dusurmek hasar vermez.")]
    [SerializeField] private float _throwDamageMinSpeed = 3f;
    [Tooltip("Temel hasar; carpma hiziyla artar.")]
    [SerializeField] private float _throwBaseDamage = 25f;
    [Tooltip("Vurus sonrasi dusmana uygulanan stun suresi (sn).")]
    [SerializeField] private float _throwStunDuration = 1.5f;

    public readonly NetworkVariable<bool> NetIsHeld = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<ulong> NetGrabberClientId = new(
        NoGrabberClientId,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private bool _isHighlighted;
    private Rigidbody _rb;
    private Material _originalMaterial;
    private Renderer _rend;
    private float _originalDrag;
    private float _originalAngularDrag;

    private Vector3 _serverHoldTarget;
    private bool _serverHasHoldTarget;

    private float _throwExpiry;         // server-side: bu zamana kadar 'firlatilmis' kabul edilir
    private UnityEngine.AI.NavMeshObstacle _navObstacle;  // dusmanlar etrafindan dolasabilsin

    // --- Hold target RPC throttle (client-side only) ---
    private Vector3 _lastSentHoldTarget;
    private float _nextHoldTargetSendTime;
    private const float HoldTargetSendInterval = 0.05f;  // 20 Hz max
    private const float HoldTargetMinDeltaSqr = 0.01f * 0.01f;  // 1cm threshold

    // --- IGrabbable ---
    public float Weight => weight;
    public bool IsHeld => NetIsHeld.Value;

    // --- IInteractable ---
    public string InteractPrompt => "E — Pick up";
    public bool CanInteract(PlayerStateMachine player) => !NetIsHeld.Value;

    public void Interact(PlayerStateMachine player)
    {
        // Caller (PlayerInteraction) artik dogrudan RequestGrabServerRpc atiyor.
        // Bu method legacy IInteractable yolu icin no-op.
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rend = GetComponent<Renderer>();

        _originalDrag = _rb.linearDamping;
        _originalAngularDrag = _rb.angularDamping;

        if (_rend != null)
            _originalMaterial = _rend.material;

        // Dusmanlar bu objenin etrafindan dolasabilsin: NavMeshObstacle (carve modu).
        // Yoksa runtime ekle; carving etkin ama yalniz yerlesince carve eder (hareket halinde
        // sadece avoidance — performans icin). Elde tutarken obstacle kapatilir (asagida).
        _navObstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
        if (_navObstacle == null)
            _navObstacle = gameObject.AddComponent<UnityEngine.AI.NavMeshObstacle>();
        _navObstacle.carving = true;
        _navObstacle.carveOnlyStationary = true;
        // Daha az sik carve et: birden fazla item ayni anda yere oturursa NavMesh
        // surekli yeniden bake olmasin (frame spike / donma onlenir).
        _navObstacle.carvingTimeToStationary = 2f;
        _navObstacle.carvingMoveThreshold = 0.5f;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetIsHeld.Value = false;
            NetGrabberClientId.Value = NoGrabberClientId;
            _rb.linearDamping = _originalDrag;
            _rb.angularDamping = _originalAngularDrag;
            _serverHasHoldTarget = false;
        }

        if (!IsServer)
        {
            // Client'lar fizik simule etmez; pozisyon NetworkTransform ile gelir.
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        NetGrabberClientId.OnValueChanged += OnGrabberChanged;
    }

    public override void OnNetworkDespawn()
    {
        NetGrabberClientId.OnValueChanged -= OnGrabberChanged;
    }

    private void OnGrabberChanged(ulong previous, ulong current)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        ulong localId = nm.LocalClientId;

        if (previous == localId && current != localId)
        {
            var pi = TryGetLocalPlayerInteraction();
            pi?.NotifyReleased(this);
        }
        else if (previous != localId && current == localId)
        {
            var pi = TryGetLocalPlayerInteraction();
            pi?.NotifyGrabbed(this);
        }
    }

    private static PlayerInteraction TryGetLocalPlayerInteraction()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        if (nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
        {
            var pi = nm.LocalClient.PlayerObject.GetComponent<PlayerInteraction>();
            if (pi != null) return pi;
        }

        // Fallback for manually placed or custom spawned players where PlayerObject is null
        var allInteractions = FindObjectsByType<PlayerInteraction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var pi in allInteractions)
        {
            if (pi.IsOwner)
                return pi;
        }

        return null;
    }

    public void SetHighlight(bool active)
    {
        if (_rend == null || highlightMaterial == null) return;
        if (_isHighlighted == active) return;

        _isHighlighted = active;
        _rend.material = active ? highlightMaterial : _originalMaterial;
    }

    // --- IGrabbable interface (legacy entry; PlayerInteraction direkt RPC atiyor) ---
    public void OnGrab(PlayerStateMachine grabber) { }
    public void OnRelease() { }
    public void Throw(Vector3 direction, float chargeRatio) { }

    // --- Server-side hold lifecycle (PlayerInteraction RPC handler'larindan cagrilir) ---

    public void ServerStartHold(ulong grabberClientId)
    {
        if (!IsServer) return;
        if (NetIsHeld.Value) return;

        NetGrabberClientId.Value = grabberClientId;
        NetIsHeld.Value = true;
        _rb.linearDamping = 8f;
        _rb.angularDamping = 8f;
        _serverHasHoldTarget = false;

        // Throttle state reset — yeni grab'de ilk RPC hemen gitsin.
        _lastSentHoldTarget = Vector3.zero;
        _nextHoldTargetSendTime = 0f;

        // Tasinirken NavMesh'i carve etme (oyuncu etrafinda surekli delik acmasin).
        // Bekleyen "throw penceresi sonu" reenable cagrisini de iptal et.
        CancelInvoke(nameof(ReenableNavObstacle));
        if (_navObstacle != null) _navObstacle.enabled = false;
    }

    public void ServerStopHold()
    {
        if (!IsServer) return;
        if (!NetIsHeld.Value) return;

        NetIsHeld.Value = false;
        NetGrabberClientId.Value = NoGrabberClientId;
        _rb.linearDamping = _originalDrag;
        _rb.angularDamping = _originalAngularDrag;
        _serverHasHoldTarget = false;

        // Tekrar carve etmeye basla (yere dustugunde dusmanlar etrafindan dolasir).
        if (_navObstacle != null) _navObstacle.enabled = true;
    }

    public void ServerSetHoldTarget(Vector3 target)
    {
        if (!IsServer) return;
        if (!NetIsHeld.Value) return;

        _serverHoldTarget = target;
        _serverHasHoldTarget = true;
    }

    public void ServerThrow(Vector3 direction, float chargeRatio)
    {
        if (!IsServer) return;

        ServerStopHold();

        chargeRatio = Mathf.Clamp01(chargeRatio);
        if (direction.sqrMagnitude < 0.0001f) return;

        float force = throwForceMultiplier * (1f + chargeRatio * 2f);
        _rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        Vector3 randomTorque = Random.insideUnitSphere * force * 0.3f;
        _rb.AddTorque(randomTorque, ForceMode.Impulse);

        // Firlatma hasari penceresi acildi: bu sure icinde dusmana carparsa hasar + stun verir.
        _throwExpiry = Time.time + _throwDamageWindow;

        // Pencere boyunca obstacle KAPALI tutulur: ucan obje NavMesh carve etmesin, yoksa
        // dusman carve alanindan disari "ışınlanır" (dodge gibi gorunen bug). Pencere bitince
        // ReenableNavObstacle acar (artik yere oturmus, dusman etrafindan dolanir).
        CancelInvoke(nameof(ReenableNavObstacle));
        if (_navObstacle != null) _navObstacle.enabled = false;
        Invoke(nameof(ReenableNavObstacle), _throwDamageWindow);
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;
        if (!NetIsHeld.Value || !_serverHasHoldTarget) return;

        Vector3 direction = _serverHoldTarget - _rb.position;
        float distance = direction.magnitude;

        Vector3 springForce = direction * holdSpringStrength;
        Vector3 dampingForce = -_rb.linearVelocity * holdDamping;
        _rb.AddForce(springForce + dampingForce, ForceMode.Force);

        if (distance > grabDistance * 2.5f)
            ServerStopHold();
    }

    // Geriye uyumluluk: PlayerInteraction.UpdateHeldObject hala MoveTowards cagiriyor.
    // Owner client tarafindan cagirilir; server'a forward eder.
    public void MoveTowards(Vector3 targetPosition)
    {
        if (IsServer)
        {
            ServerSetHoldTarget(targetPosition);
            return;
        }
        if (!IsOwnerOfHold()) return;

        // Rate limiter: saniyede en fazla 20 RPC
        if (Time.time < _nextHoldTargetSendTime) return;

        // Delta threshold: pozisyon yeterince degismediyse gonderme
        if ((targetPosition - _lastSentHoldTarget).sqrMagnitude < HoldTargetMinDeltaSqr) return;

        _lastSentHoldTarget = targetPosition;
        _nextHoldTargetSendTime = Time.time + HoldTargetSendInterval;
        SetHoldTargetServerRpc(targetPosition);
    }

    private bool IsOwnerOfHold()
    {
        return NetIsHeld.Value && NetGrabberClientId.Value == NetworkManager.Singleton.LocalClientId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SetHoldTargetServerRpc(Vector3 target, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!NetIsHeld.Value) return;
        if (rpcParams.Receive.SenderClientId != NetGrabberClientId.Value) return;

        ServerSetHoldTarget(target);
    }

    // ----- Firlatma hasari (server-only) -----
    // Firlattiktan kisa sure sonra dusmana carparsa hasar verir + stun yapar, sonra yok olur.
    // Duvar/oyuncu/baska items'a carpinca etkilenmez (sadece EnemyController hedef alinir),
    // boylece kendine ya da arkadasa hasar vermez (friendly-fire yok).
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        if (Time.time > _throwExpiry) return;                                    // firlatma penceresi disi
        if (collision.relativeVelocity.magnitude < _throwDamageMinSpeed) return;  // yavas carpma -> hasar yok

        var enemy = collision.gameObject.GetComponent<EnemyController>()
                    ?? collision.gameObject.GetComponentInParent<EnemyController>();
        if (enemy == null) return;       // dusman degilse atla (duvar/oyuncu/kendi)
        if (!enemy.IsAlive) return;

        Vector3 hitPoint = collision.contacts.Length > 0
            ? collision.contacts[0].point
            : enemy.transform.position;

        // Hasar = temel + hiz bonusu (siddetli vurus daha cok yapar). Hiz bonusu 30'da kapani.
        float speed = collision.relativeVelocity.magnitude;
        float damage = _throwBaseDamage + Mathf.Min(speed * 1.5f, 30f);

        enemy.TakeDamage(damage, hitPoint, NetGrabberClientId.Value);
        enemy.SetStunned(true, _throwStunDuration);

        Debug.Log($"[PhysicsObject] Firlatma vurusu: {damage:F0} hasar (hiz={speed:F1} m/s), dusman stunlandi.");

        // Tekrar tetiklenmesin (ertelenmis despawn surecinde baska collide patlatmasin).
        _throwExpiry = 0f;

        // Despawn'i bir sonraki frame'e ertele: OnCollisionEnter bir physics callback'idir;
        // NetworkObject'i callback icinde yok etmek frame stall / donma yapar. Invoke(0f)
        // physics step bitince guvenle calistirir.
        Invoke(nameof(DespawnSelf), 0f);
    }

    private void DespawnSelf()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(gameObject);
    }

    /// <summary>Firlatma penceresi bittiginde NavMesh obstacle'i yeniden acar (eger held degilse).</summary>
    private void ReenableNavObstacle()
    {
        if (_navObstacle != null && !NetIsHeld.Value)
            _navObstacle.enabled = true;
    }
}
