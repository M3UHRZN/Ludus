using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PhysicsObject : MonoBehaviour, IGrabbable, IInteractable
{
    [Header("Grab Ayarlari")]
    public float grabDistance = 4f;
    public float holdSpringStrength = 150f;
    public float holdDamping = 12f;
    public float throwForceMultiplier = 12f;

    [Header("Item")]
    [SerializeField] private float weight = 1f;

    [Header("Gorsel Geribildirim")]
    public Material highlightMaterial;

    private bool _isHeld;
    private bool _isHighlighted;
    private Rigidbody _rb;
    private Material _originalMaterial;
    private Renderer _rend;
    private float _originalDrag;
    private float _originalAngularDrag;
    private PlayerStateMachine _grabber;

    // --- IGrabbable ---
    public float Weight => weight;
    public bool IsHeld => _isHeld;

    // --- IInteractable ---
    public string InteractPrompt => "E — Pick up";
    public bool CanInteract(PlayerStateMachine player) => !_isHeld;

    public void Interact(PlayerStateMachine player)
    {
        OnGrab(player);
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

    public void SetHighlight(bool active)
    {
        if (_rend == null || highlightMaterial == null) return;
        if (_isHighlighted == active) return;

        _isHighlighted = active;
        _rend.material = active ? highlightMaterial : _originalMaterial;
    }

    public void OnGrab(PlayerStateMachine grabber)
    {
        _isHeld = true;
        _grabber = grabber;
        _rb.linearDamping = 8f;
        _rb.angularDamping = 8f;
    }

    public void OnRelease()
    {
        _isHeld = false;
        _grabber = null;
        _rb.linearDamping = _originalDrag;
        _rb.angularDamping = _originalAngularDrag;
        SetHighlight(false);
    }

    public void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _rb.position;
        float distance = direction.magnitude;

        Vector3 springForce = direction * holdSpringStrength;
        Vector3 dampingForce = -_rb.linearVelocity * holdDamping;
        _rb.AddForce(springForce + dampingForce, ForceMode.Force);

        if (distance > grabDistance * 2.5f && _grabber != null)
        {
            var interaction = _grabber.Interaction;
            if (interaction != null)
                interaction.DropObject();
        }
    }

    public void Throw(Vector3 direction, float chargeRatio)
    {
        OnRelease();

        float force = throwForceMultiplier * (1f + chargeRatio * 2f);
        _rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        Vector3 randomTorque = Random.insideUnitSphere * force * 0.3f;
        _rb.AddTorque(randomTorque, ForceMode.Impulse);
    }
}
