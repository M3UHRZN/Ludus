using UnityEngine;

/// <summary>
/// Test ortamı için basit FPS CharacterController hareketi.
/// Bu scripti Player objesine ekle.
/// 
/// HİYERARŞİ:
/// ─ Player          (CharacterController + PlayerController)
///   └─ PlayerCamera (Camera + GrabSystem)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Hareket")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.8f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    [Header("Kamera Rotasyonu")]
    public float mouseSensitivity = 2f;
    [Tooltip("Dikey bakış açısı sınırı (±derece)")]
    public float verticalClampAngle = 85f;

    [Header("Referanslar")]
    public Transform playerCamera;   // PlayerCamera Transform

    // ── İç Durum ──────────────────────────────────────────────────────────────
    private CharacterController cc;
    private Vector3 velocity;
    private float xRotation = 0f;

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        // Otomatik kamera bul
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        // İmleci kilitle
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleMouseLook();
        HandleMovement();

        // Escape → imleç kilidi aç/kapat (debug için)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None
                : CursorLockMode.Locked;
        }
    }

    // ── Fare ile Bakış ────────────────────────────────────────────────────────
    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Dikey rotasyon (kamera)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -verticalClampAngle, verticalClampAngle);

        if (playerCamera != null)
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Yatay rotasyon (player gövdesi)
        transform.Rotate(Vector3.up * mouseX);
    }

    // ── WASD Hareket ──────────────────────────────────────────────────────────
    void HandleMovement()
    {
        bool isGrounded = cc.isGrounded;

        // Yerde yerçekimini sıfırla
        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;

        // Sprint
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            currentSpeed *= sprintMultiplier;

        cc.Move(move * currentSpeed * Time.deltaTime);

        // Zıplama
        if (Input.GetButtonDown("Jump") && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        // Yerçekimi uygula
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
