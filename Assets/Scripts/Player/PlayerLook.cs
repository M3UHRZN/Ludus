using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [Header("Hassasiyet")]
    [SerializeField] private float mouseSensitivity = 200f;

    [Header("Referanslar")]
    [SerializeField] private Transform playerBody;     // yaw
    [SerializeField] private Transform cameraTarget;   // pitch (CinemachineCamera bunu takip eder)

    [Header("Pitch Sınırları")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    private float xRotation = 0f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

        cameraTarget.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}