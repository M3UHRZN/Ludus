using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Standalone test player — no Netcode required.
/// WASD movement, mouse look, Space to jump.
/// FearSystem calls SetSpeedMultiplier() to apply speed penalty.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TestPlayer : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed        = 5f;
    public float mouseSensitivity = 2f;
    public float gravity          = -20f;
    public float jumpHeight       = 1.2f;

    private CharacterController _cc;
    private Transform           _cam;
    private float               _xRotation;
    private Vector3             _velocity;
    private float               _speedMultiplier = 1f;
    private bool                _inputEnabled    = true;

    private void Start()
    {
        _cc  = GetComponent<CharacterController>();
        _cam = Camera.main?.transform;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void Update()
    {
        if (!_inputEnabled) return;

        // Escape — toggle cursor lock
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool locked      = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = locked;
        }

        // Mouse look
        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 delta  = Mouse.current.delta.ReadValue();
            float   mouseX = delta.x * mouseSensitivity * 0.1f;
            float   mouseY = delta.y * mouseSensitivity * 0.1f;

            _xRotation -= mouseY;
            _xRotation  = Mathf.Clamp(_xRotation, -80f, 80f);

            if (_cam != null)
                _cam.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);

            transform.Rotate(Vector3.up * mouseX);
        }

        // WASD movement
        if (Keyboard.current != null)
        {
            float h = 0f, v = 0f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)  h = -1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h =  1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)  v = -1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)    v =  1f;

            Vector3 move = transform.right * h + transform.forward * v;
            _cc.Move(move * walkSpeed * _speedMultiplier * Time.deltaTime);

            if (Keyboard.current.spaceKey.wasPressedThisFrame && _cc.isGrounded)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        // Gravity
        if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
        _velocity.y += gravity * Time.deltaTime;
        _cc.Move(_velocity * Time.deltaTime);
    }

    /// <summary>Called by FearSystem to apply speed penalty. 0 = stop, 1 = normal.</summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        _speedMultiplier = Mathf.Clamp01(multiplier);
    }

    /// <summary>Called by MarketUIController to freeze/unfreeze player input.</summary>
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
    }
}
