using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraModeController : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform modelVisualRoot;

    [Header("Camera Modes")]
    [SerializeField] private Key toggleKey = Key.C;
    [SerializeField] private Vector3 thirdPersonLocalOffset = new Vector3(0f, 1.75f, -2.25f);
    [SerializeField] private float positionSmoothTime = 0.08f;

    [Header("Collision")]
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private float cameraRadius = 0.22f;
    [SerializeField] private float minThirdPersonDistance = 0.45f;
    [SerializeField] private float wallPadding = 0.08f;

    public bool IsThirdPersonRequested => _thirdPersonRequested;
    public bool IsEffectivelyThirdPerson => _thirdPersonRequested && !_forcedFirstPerson;

    private Vector3 _firstPersonLocalPosition;
    private Vector3 _positionVelocity;
    private bool _thirdPersonRequested;
    private bool _forcedFirstPerson;
    private int _runtimeObstructionMask;

    public override void OnNetworkSpawn()
    {
        if (playerLook == null)
            playerLook = GetComponent<PlayerLook>();

        if (cameraTarget == null && playerLook != null)
            cameraTarget = playerLook.CameraTarget;

        if (modelVisualRoot == null)
            modelVisualRoot = transform.Find("ModelSlot");

        if (cameraTarget != null)
            _firstPersonLocalPosition = cameraTarget.localPosition;

        _runtimeObstructionMask = obstructionMask.value & ~(1 << gameObject.layer);

        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        SetOwnerModelVisible(false);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            SetOwnerModelVisible(true);
    }

    private void LateUpdate()
    {
        if (!IsOwner || cameraTarget == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
            _thirdPersonRequested = !_thirdPersonRequested;

        Vector3 targetLocalPosition = _firstPersonLocalPosition;
        _forcedFirstPerson = false;

        if (_thirdPersonRequested)
        {
            Vector3 firstWorldPosition = transform.TransformPoint(_firstPersonLocalPosition);
            Vector3 desiredWorldPosition = transform.TransformPoint(thirdPersonLocalOffset);
            Vector3 resolvedWorldPosition = ResolveThirdPersonCollision(
                firstWorldPosition,
                desiredWorldPosition,
                out _forcedFirstPerson);

            targetLocalPosition = transform.InverseTransformPoint(resolvedWorldPosition);
        }

        cameraTarget.localPosition = Vector3.SmoothDamp(
            cameraTarget.localPosition,
            targetLocalPosition,
            ref _positionVelocity,
            positionSmoothTime);

        SetOwnerModelVisible(IsEffectivelyThirdPerson);
    }

    private Vector3 ResolveThirdPersonCollision(Vector3 firstWorldPosition, Vector3 desiredWorldPosition, out bool forcedFirstPerson)
    {
        forcedFirstPerson = false;

        Vector3 direction = desiredWorldPosition - firstWorldPosition;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            forcedFirstPerson = true;
            return firstWorldPosition;
        }

        direction /= distance;

        if (!Physics.SphereCast(
                firstWorldPosition,
                cameraRadius,
                direction,
                out RaycastHit hit,
                distance,
                _runtimeObstructionMask,
                QueryTriggerInteraction.Ignore))
        {
            return desiredWorldPosition;
        }

        float safeDistance = Mathf.Max(0f, hit.distance - cameraRadius - wallPadding);
        if (safeDistance < minThirdPersonDistance)
        {
            forcedFirstPerson = true;
            return firstWorldPosition;
        }

        return firstWorldPosition + direction * safeDistance;
    }

    private void SetOwnerModelVisible(bool visible)
    {
        if (modelVisualRoot != null && modelVisualRoot.gameObject.activeSelf != visible)
            modelVisualRoot.gameObject.SetActive(visible);
    }
}
