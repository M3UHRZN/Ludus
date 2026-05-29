using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour, ISpeedModifiable
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

    [Header("Crouch Camera")]
    [Tooltip("Cömelince kameranin inecegi pivot — PlayerLook'taki cameraTarget ile ayni transform")]
    [SerializeField] private Transform cameraTarget;
    [Tooltip("Cömelince kameranin ayakta hizasindan ne kadar asagi inecegi (metre)")]
    [SerializeField] private float crouchCamDrop = 1f;

    private float _baseCamY;

    public readonly NetworkVariable<bool> NetCrouching = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public readonly NetworkVariable<bool> NetGrounded = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _isCrouching;
    private bool _wantCrouch;
    // FearSystem (lokal korku tabanli ceza) kanali
    private float _speedMultiplier = 1f;
    // Dis kaynak (orn. dusman mermisi) tabanli gecici slow kanali — FearSystem'den
    // bagimsiz; ikisi carpilarak final hiz uygulanir. ApplyTemporarySlow set eder,
    // _externalSlowTimer her frame azalir, sifirlanip 1f'e doner.
    private float _externalSlowMultiplier = 1f;
    private float _externalSlowTimer;

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

        if (cameraTarget != null)
            _baseCamY = cameraTarget.localPosition.y;
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
        TickExternalSlow();
        CheckGround();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void TickExternalSlow()
    {
        if (_externalSlowTimer <= 0f) return;
        _externalSlowTimer -= Time.deltaTime;
        if (_externalSlowTimer <= 0f)
        {
            _externalSlowMultiplier = 1f;
            _externalSlowTimer = 0f;
        }
    }

    public bool IsGrounded => _isGrounded;
    public float RunSpeed   => runSpeed;

    // Aktif yatay hız (m/s) — animatör blend tree direkt bunu okur.
    // Mermi slow'u da dahil edilmis efektif hizdir.
    public float CurrentSpeed
    {
        get
        {
            if (_moveAction == null) return 0f;
            if (_moveAction.ReadValue<Vector2>().magnitude < 0.1f) return 0f;
            float baseSpeed = _isCrouching
                ? crouchSpeed
                : (_sprintAction != null && _sprintAction.IsPressed() ? runSpeed : walkSpeed);
            return baseSpeed * _externalSlowMultiplier;
        }
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = multiplier;
    }

    /// <summary>
    /// Dis kaynak (orn. dusman mermisi) tabanli gecici yavaslama. FearSystem'in
    /// _speedMultiplier'iyla CARPILIR (compose), birbirini ezmez. Yeni cagri:
    ///   - daha agir multiplier (daha kucuk) ise onceliklenir,
    ///   - duration max-extend edilir (daha uzun olan tutulur).
    /// Bu sayede arka arkaya iki mermi vurursa daha sert/uzun slow uygulanir.
    /// </summary>
    public void ApplyTemporarySlow(float multiplier, float duration)
    {
        if (duration <= 0f) return;
        multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
        if (multiplier < _externalSlowMultiplier) _externalSlowMultiplier = multiplier;
        if (duration > _externalSlowTimer) _externalSlowTimer = duration;
    }

    /// <summary>
    /// Dis kaynakli anlik yer degistirme (knockback gibi). CharacterController'i
    /// dogrudan iter. Bu script stun sirasinda disabled olsa bile calisir cunku
    /// CharacterController ayri ve hala enabled bir component'tir.
    /// </summary>
    public void ApplyExternalMove(Vector3 delta)
    {
        if (_controller != null && _controller.enabled)
            _controller.Move(delta);
    }

    private void CheckGround()
    {
        _isGrounded = _controller.isGrounded ||
                      Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (NetGrounded.Value != _isGrounded)
            NetGrounded.Value = _isGrounded;
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

        if (cameraTarget != null)
        {
            float targetCamY = _isCrouching ? _baseCamY - crouchCamDrop : _baseCamY;
            Vector3 camPos = cameraTarget.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            cameraTarget.localPosition = camPos;
        }
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

        // Final hiz: FearSystem (_speedMultiplier) ile dis kaynak (_externalSlowMultiplier)
        // CARPILIR — biri digerini ezmez, ikisi de etkili. Ornek: korku 0.7x + mermi slow 0.55x
        // -> efektif 0.385x. (Ihtimaliyat icin bir taban sinir koyabiliriz; simdilik dogal birakildi.)
        _controller.Move(move * (speed * _speedMultiplier * _externalSlowMultiplier * Time.deltaTime));
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
