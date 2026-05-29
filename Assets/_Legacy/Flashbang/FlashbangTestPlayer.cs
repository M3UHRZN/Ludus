using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone local player for flashbang testing.
/// Supports temporary movement and look penalties from FlashbangEffect.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FlashbangTestPlayer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    private CharacterController _controller;
    private Transform _cameraTransform;
    private float _xRotation;
    private Vector3 _velocity;
    private float _moveMultiplier = 1f;
    private float _lookMultiplier = 1f;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _cameraTransform = Camera.main != null ? Camera.main.transform : null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        HandleLook();
        HandleMove();
        HandleGravity();
    }

    public void SetMoveMultiplier(float multiplier)
    {
        _moveMultiplier = Mathf.Clamp01(multiplier);
    }

    public void SetLookMultiplier(float multiplier)
    {
        _lookMultiplier = Mathf.Clamp01(multiplier);
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked || Mouse.current == null)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        float mouseX = delta.x * mouseSensitivity * 0.1f * _lookMultiplier;
        float mouseY = delta.y * mouseSensitivity * 0.1f * _lookMultiplier;

        _xRotation -= mouseY;
        _xRotation = Mathf.Clamp(_xRotation, -80f, 80f);

        if (_cameraTransform != null)
            _cameraTransform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMove()
    {
        if (Keyboard.current == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal = -1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal = 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical = -1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical = 1f;

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        _controller.Move(move * walkSpeed * _moveMultiplier * Time.deltaTime);

        if (Keyboard.current.spaceKey.wasPressedThisFrame && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void HandleGravity()
    {
        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}
