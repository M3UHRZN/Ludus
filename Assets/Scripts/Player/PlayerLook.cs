using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerLook : NetworkBehaviour
{
    [Header("Hassasiyet")]
    [SerializeField] private float mouseSensitivity = 0.15f;

    [Header("Referanslar")]
    [SerializeField] private Transform playerBody;
    [SerializeField] private Transform cameraTarget;

    [Header("Pitch Sinirlari")]
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    public Transform CameraTarget => cameraTarget;

    private float _xRotation;
    private InputAction _lookAction;
    private CinemachineCamera _vcam;

    public override void OnNetworkSpawn()
    {
        if (cameraTarget != null)
            _vcam = cameraTarget.GetComponent<CinemachineCamera>();

        if (!IsOwner)
        {
            if (_vcam != null) _vcam.enabled = false;
            enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        var input = GetComponent<PlayerInput>();
        _lookAction = input.actions["Gameplay/Look"];

        // Bu oyuncunun vcam'ini aktif et — CinemachineBrain otomatik devralir
        if (_vcam != null) _vcam.enabled = true;
    }

    private void Update()
    {
        if (_lookAction == null) return;
        Vector2 delta = _lookAction.ReadValue<Vector2>();

        _xRotation -= delta.y * mouseSensitivity;
        _xRotation = Mathf.Clamp(_xRotation, minPitch, maxPitch);

        cameraTarget.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * (delta.x * mouseSensitivity));
    }
}
