using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

public class PlayerLook : NetworkBehaviour
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

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var cm = FindAnyObjectByType<CinemachineCamera>();
        if (cm != null && cameraTarget != null)
        {
            cm.Follow = cameraTarget;
            cm.LookAt = cameraTarget;
        }
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