using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CameraNameTestPlayer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -22f;

    [Header("Look")]
    [SerializeField] private Transform lookPivot;
    [SerializeField] private float mouseSensitivity = 1.8f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 75f;

    [Header("Visual")]
    [SerializeField] private GameObject thirdPersonVisual;

    private CharacterController _controller;
    private Vector3 _velocity;
    private float _pitch;

    public Transform LookPivot => lookPivot;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();

        if (lookPivot == null)
        {
            GameObject pivot = new GameObject("LookPivot");
            pivot.transform.SetParent(transform);
            pivot.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            pivot.transform.localRotation = Quaternion.identity;
            lookPivot = pivot.transform;
        }
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ToggleCursor();

        if (Cursor.lockState == CursorLockMode.Locked)
            ReadLookInput();

        ReadMovementInput();
    }

    public void SetThirdPersonVisualVisible(bool visible)
    {
        if (thirdPersonVisual != null)
            thirdPersonVisual.SetActive(visible);
    }

    public void SetThirdPersonVisual(GameObject visual)
    {
        thirdPersonVisual = visual;
    }

    private void ToggleCursor()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame)
            return;

        bool locked = Cursor.lockState == CursorLockMode.Locked;
        Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = locked;
    }

    private void ReadLookInput()
    {
        if (Mouse.current == null)
            return;

        Vector2 delta = Mouse.current.delta.ReadValue();
        float yaw = delta.x * mouseSensitivity * 0.1f;
        float pitch = delta.y * mouseSensitivity * 0.1f;

        transform.Rotate(Vector3.up * yaw);

        _pitch = Mathf.Clamp(_pitch - pitch, minPitch, maxPitch);
        lookPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void ReadMovementInput()
    {
        if (Keyboard.current == null)
            return;

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertical += 1f;

        Vector3 move = transform.right * horizontal + transform.forward * vertical;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        float speed = Keyboard.current.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;
        _controller.Move(move * speed * Time.deltaTime);

        if (_controller.isGrounded && _velocity.y < 0f)
            _velocity.y = -2f;

        if (Keyboard.current.spaceKey.wasPressedThisFrame && _controller.isGrounded)
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}
