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
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return null;
        return nm.LocalClient.PlayerObject.GetComponent<PlayerInteraction>();
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
}
