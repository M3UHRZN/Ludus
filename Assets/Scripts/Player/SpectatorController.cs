using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class SpectatorController : NetworkBehaviour
{
    [SerializeField] private float freeCamSpeed = 5f;
    [SerializeField] private float freeCamSensitivity = 0.15f;
    [SerializeField] private CinemachineCamera spectatorCamPrefab;

    private readonly List<ISpectatable> _targets = new();
    private int _currentIndex;
    private bool _isFreeCam;

    private CinemachineCamera _spectateCamera;
    private InputAction _lookAction;
    private InputAction _nextAction;
    private InputAction _prevAction;
    private InputAction _freeCamAction;

    private float _freeCamYaw;
    private float _freeCamPitch;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (!IsOwner) return;

        var input = GetComponent<PlayerInput>();
        _lookAction = input.actions["Spectator/Look"];
        _nextAction = input.actions["Spectator/SwitchNext"];
        _prevAction = input.actions["Spectator/SwitchPrev"];
        _freeCamAction = input.actions["Spectator/FreeCam"];

        RefreshTargets();
        SetupSpectateCamera();

        if (_targets.Count > 0)
        FocusTarget(0);
    }

    private void OnDisable()
    {
        if (_spectateCamera != null)
            _spectateCamera.Priority = 0;
    }

    private void Update()
    {
        if (_nextAction.WasPressedThisFrame())
            CycleTarget(1);
        if (_prevAction.WasPressedThisFrame())
            CycleTarget(-1);
        if (_freeCamAction.WasPressedThisFrame())
            ToggleFreeCam();

        if (_isFreeCam)
            UpdateFreeCam();
    }

    private void RefreshTargets()
    {
        _targets.Clear();
        foreach (var obj in FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None))
        {
            if (obj is ISpectatable s && s.CanBeSpectated && obj != GetComponent<PlayerStateMachine>())
                _targets.Add(s);
        }
    }

    private void SetupSpectateCamera()
    {
        var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in allCams)
        {
            if (cam.gameObject.name.Contains("Spectator") || cam.gameObject.name.Contains("spectator"))
            {
                _spectateCamera = cam;
                break;
            }
        }

        if (_spectateCamera == null)
        {
            if (spectatorCamPrefab != null)
                _spectateCamera = Instantiate(spectatorCamPrefab);
            else
            {
                var go = new GameObject("SpectatorCam");
                _spectateCamera = go.AddComponent<CinemachineCamera>();
            }
        }

        _spectateCamera.Priority.Value = 100;
    }
    private void FocusTarget(int index)
    {
        if (_targets.Count == 0) return;
        _currentIndex = index;
        _isFreeCam = false;

        var target = _targets[_currentIndex];
        _spectateCamera.Follow = target.SpectateTarget;
        _spectateCamera.LookAt = target.SpectateTarget;
        _spectateCamera.transform.rotation = Quaternion.identity;
    }

    private void CycleTarget(int direction)
    {
        if (_targets.Count == 0) return;
        RefreshTargets();
        if (_targets.Count == 0) return;

        _currentIndex = (_currentIndex + direction + _targets.Count) % _targets.Count;
        FocusTarget(_currentIndex);
    }

    private void ToggleFreeCam()
    {
        _isFreeCam = !_isFreeCam;
        if (_isFreeCam)
        {
            _spectateCamera.Follow = null;
            _spectateCamera.LookAt = null;
            _spectateCamera.transform.position = transform.position + Vector3.up * 2f;
            _freeCamYaw = _spectateCamera.transform.eulerAngles.y;
            _freeCamPitch = _spectateCamera.transform.eulerAngles.x;
        }
        else if (_targets.Count > 0)
        {
            FocusTarget(_currentIndex);
        }
    }

    private void UpdateFreeCam()
    {
        Vector2 look = _lookAction.ReadValue<Vector2>();
        _freeCamYaw += look.x * freeCamSensitivity;
        _freeCamPitch -= look.y * freeCamSensitivity;
        _freeCamPitch = Mathf.Clamp(_freeCamPitch, -85f, 85f);

        _spectateCamera.transform.rotation = Quaternion.Euler(_freeCamPitch, _freeCamYaw, 0f);

        Vector3 move = Vector3.zero;
        if (Keyboard.current.wKey.isPressed) move += _spectateCamera.transform.forward;
        if (Keyboard.current.sKey.isPressed) move -= _spectateCamera.transform.forward;
        if (Keyboard.current.dKey.isPressed) move += _spectateCamera.transform.right;
        if (Keyboard.current.aKey.isPressed) move -= _spectateCamera.transform.right;

        _spectateCamera.transform.position += move.normalized * (freeCamSpeed * Time.deltaTime);
    }
}
