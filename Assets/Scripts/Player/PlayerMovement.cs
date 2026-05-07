using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float runSpeed = 10f;
    [SerializeField] private float crouchSpeed = 2.5f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Crouch")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Crouch Visual")]
    [SerializeField] private Transform visualMesh;

    public readonly NetworkVariable<bool> NetCrouching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _isCrouching;
    private bool _wantCrouch;
    private float _speedMultiplier = 1f;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _crouchAction;
    private InputAction _sprintAction;

    private Action<InputAction.CallbackContext> _onCrouchStarted;
    private Action<InputAction.CallbackContext> _onCrouchCanceled;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            NetCrouching.OnValueChanged += OnNetCrouchingChanged;
            enabled = false;
            return;
        }

        var input = GetComponent<PlayerInput>();
        _moveAction   = input.actions["Gameplay/Move"];
        _jumpAction   = input.actions["Gameplay/Jump"];
        _crouchAction = input.actions["Gameplay/Crouch"];
        _sprintAction = input.actions["Gameplay/Sprint"];

        _onCrouchStarted  = _ => _wantCrouch = true;
        _onCrouchCanceled = _ => _wantCrouch = false;
        _crouchAction.started  += _onCrouchStarted;
        _crouchAction.canceled += _onCrouchCanceled;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            NetCrouching.OnValueChanged -= OnNetCrouchingChanged;
            return;
        }

        if (_crouchAction != null)
        {
            if (_onCrouchStarted  != null) _crouchAction.started  -= _onCrouchStarted;
            if (_onCrouchCanceled != null) _crouchAction.canceled -= _onCrouchCanceled;
        }
    }

    private void OnNetCrouchingChanged(bool prev, bool current) { }

    private void Update()
    {
        if (_moveAction == null) return;
        CheckGround();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }

    private void CheckGround()
    {
        _isGrounded = _controller.isGrounded ||
                      Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    private void HandleCrouch()
    {
        bool shouldCrouch = _wantCrouch;

        if (_isCrouching && !shouldCrouch && !CanStandUp())
            shouldCrouch = true;

        bool prev = _isCrouching;
        _isCrouching = shouldCrouch;

        if (_isCrouching != prev)
            NetCrouching.Value = _isCrouching;

        float targetHeight = _isCrouching ? crouchHeight : standingHeight;
        _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        Vector3 center = _controller.center;
        center.y = _controller.height / 2f;
        _controller.center = center;

    }

    private bool CanStandUp()
    {
        // Capsule'ın tepesinden başla — transform.position'dan başlarsa kendi kapsülüne çarpar
        float capsuleTop = _controller.height + 0.05f;
        float checkDist  = standingHeight - _controller.height;
        return !Physics.Raycast(transform.position + Vector3.up * capsuleTop, Vector3.up, checkDist);
    }

    private void HandleMovement()
    {
        Vector2 input = _moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        float speed;
        if (_isCrouching)
            speed = crouchSpeed;
        else if (_sprintAction.IsPressed())
            speed = runSpeed;
        else
            speed = walkSpeed;

        _controller.Move(move * (speed * _speedMultiplier * Time.deltaTime));
    }

    private void HandleJump()
    {
        if (_jumpAction.WasPressedThisFrame() && _isGrounded && !_isCrouching)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void ApplyGravity()
    {
        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
