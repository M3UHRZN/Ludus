using Unity.Netcode;
using UnityEngine;

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
    [SerializeField] private LayerMask groundMask = ~0; // varsayılan: her şeyi algıla

    [Header("Crouch Visual")]
    [SerializeField] private Transform visualMesh; // kapsülün mesh objesi

    private CharacterController _controller;
    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _isCrouching;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (!IsOwner) return;

        CheckGround();
        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void CheckGround()
    {
        // groundMask atanmamışsa CharacterController'ın kendi kontrolünü de kullan
        _isGrounded = _controller.isGrounded ||
                      Physics.CheckSphere(groundCheck.position, groundCheckRadius, groundMask);

        if (_isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;
    }

    private void HandleCrouch()
    {
        bool wantCrouch = Input.GetKey(KeyCode.LeftControl);

        // Prevent standing up if blocked above
        if (_isCrouching && !wantCrouch && !CanStandUp())
            wantCrouch = true;

        _isCrouching = wantCrouch;

        float targetHeight = _isCrouching ? crouchHeight : standingHeight;
        _controller.height = Mathf.Lerp(_controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);

        Vector3 center = _controller.center;
        center.y = _controller.height / 2f;
        _controller.center = center;

        // Görsel mesh'i de küçült (atanmışsa)
        if (visualMesh != null)
        {
            float targetScaleY = _isCrouching ? 0.5f : 1f;
            Vector3 s = visualMesh.localScale;
            s.y = Mathf.Lerp(s.y, targetScaleY, Time.deltaTime * crouchTransitionSpeed);
            visualMesh.localScale = s;
        }
    }

    private bool CanStandUp()
    {
        // Spherecast upward to check for ceiling
        return !Physics.Raycast(transform.position, Vector3.up, standingHeight + 0.1f);
    }

    private void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;

        float speed;
        if (_isCrouching)
            speed = crouchSpeed;
        else if (Input.GetKey(KeyCode.LeftShift))
            speed = runSpeed;
        else
            speed = walkSpeed;

        _controller.Move(move * (speed * Time.deltaTime));
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && _isGrounded && !_isCrouching)
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
