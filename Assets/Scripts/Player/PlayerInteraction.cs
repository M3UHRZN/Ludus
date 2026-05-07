using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Camera playerCamera;

    [Header("Tutma Ayarlari")]
    [SerializeField] private float holdDistance = 2.5f;
    [SerializeField] private float minHoldDistance = 1f;
    [SerializeField] private float maxHoldDistance = 5f;

    [Header("Firlatma")]
    [SerializeField] private float maxChargeTime = 1.5f;

    [Header("Raycast")]
    [SerializeField] private LayerMask interactLayers = ~0;
    [SerializeField] private float interactRange = 4f;

    [Header("UI")]
    [SerializeField] private GameObject interactPromptUI;

    public readonly NetworkVariable<bool> IsHolding = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private IGrabbable _heldObject;
    private IInteractable _lookedInteractable;
    private bool _isChargingThrow;
    private float _chargeStartTime;

    private InputAction _interactAction;
    private InputAction _throwAction;
    private InputAction _scrollAction;
    private InputAction _flashlightAction;
    private InputAction _dropAction;

    private PlayerStateMachine _machine;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (playerCamera == null)
            playerCamera = Camera.main;

        _machine = GetComponent<PlayerStateMachine>();

        var input = GetComponent<PlayerInput>();
        _interactAction = input.actions["Gameplay/Interact"];
        _throwAction = input.actions["Gameplay/Throw"];
        _scrollAction = input.actions["Gameplay/Scroll"];
        _flashlightAction = input.actions["Gameplay/Flashlight"];
        _dropAction = input.actions["Gameplay/Drop"];
    }

    private void Update()
    {
        if (_interactAction == null) return;
        HandleLook();
        HandleInput();
        UpdateHoldDistance();
    }

    private void FixedUpdate()
    {
        if (_heldObject != null)
            UpdateHeldObject();
    }

    private void HandleLook()
    {
        if (_lookedInteractable != null && _lookedInteractable != _heldObject as IInteractable)
        {
            var ph = _lookedInteractable as Component;
            if (ph != null && ph.TryGetComponent<PhysicsObject>(out var po))
                po.SetHighlight(false);
            _lookedInteractable = null;
        }

        if (_heldObject != null)
        {
            ShowPrompt(false);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayers))
        {
            var interactable = hit.collider.GetComponent<IInteractable>();
            if (interactable != null && interactable.CanInteract(_machine))
            {
                _lookedInteractable = interactable;

                if (hit.collider.TryGetComponent<PhysicsObject>(out var po))
                    po.SetHighlight(true);

                ShowPrompt(true);
                return;
            }
        }

        ShowPrompt(false);
    }

    private void HandleInput()
    {
        if (_interactAction.WasPressedThisFrame())
        {
            if (_heldObject == null)
                TryGrab();
            else
                DropObject();
        }

        if (_dropAction.WasPressedThisFrame() && _heldObject != null)
            DropObject();

        if (_throwAction.WasPressedThisFrame() && _heldObject != null)
        {
            _isChargingThrow = true;
            _chargeStartTime = Time.time;
        }

        if (_throwAction.WasReleasedThisFrame() && _isChargingThrow && _heldObject != null)
            ThrowObject();
    }

    private void UpdateHoldDistance()
    {
        if (_heldObject == null) return;
        float scroll = _scrollAction.ReadValue<float>();
        holdDistance = Mathf.Clamp(holdDistance + scroll * 0.01f, minHoldDistance, maxHoldDistance);
    }

    private void TryGrab()
    {
        if (_lookedInteractable == null) return;

        var grabbable = (_lookedInteractable as Component)?.GetComponent<IGrabbable>();
        if (grabbable != null && !grabbable.IsHeld)
        {
            _heldObject = grabbable;
            _heldObject.OnGrab(_machine);
            SetHoldingServerRpc(true);

            if (grabbable.Weight >= 6f && _machine != null)
                _machine.ChangeState(new CarryingState());

            return;
        }

        _lookedInteractable.Interact(_machine);
    }

    public void DropObject()
    {
        if (_heldObject == null) return;

        _heldObject.OnRelease();
        _heldObject = null;
        _isChargingThrow = false;
        SetHoldingServerRpc(false);

        if (_machine != null && _machine.LocalState == PlayerStateEnum.Carrying)
            _machine.ChangeState(new AliveState());
    }

    private void ThrowObject()
    {
        if (_heldObject == null) return;

        float chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
        Vector3 throwDir = playerCamera.transform.forward;

        _heldObject.Throw(throwDir, chargeRatio);
        _heldObject = null;
        _isChargingThrow = false;
        SetHoldingServerRpc(false);

        if (_machine != null && _machine.LocalState == PlayerStateEnum.Carrying)
            _machine.ChangeState(new AliveState());
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetHoldingServerRpc(bool value)
    {
        IsHolding.Value = value;
    }

    private void UpdateHeldObject()
    {
        Vector3 targetPos = playerCamera.transform.position
                          + playerCamera.transform.forward * holdDistance;

        var comp = _heldObject as Component;
        if (comp != null && comp.TryGetComponent<PhysicsObject>(out var po))
            po.MoveTowards(targetPos);
    }

    private void ShowPrompt(bool active)
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(active);
    }
}
