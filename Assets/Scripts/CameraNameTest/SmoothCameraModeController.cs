using UnityEngine;
using UnityEngine.InputSystem;

public class SmoothCameraModeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CameraNameTestPlayer player;
    [SerializeField] private Camera targetCamera;

    [Header("Camera Modes")]
    [SerializeField] private Vector3 firstPersonOffset = new Vector3(0f, 0f, 0.05f);
    [SerializeField] private Vector3 thirdPersonOffset = new Vector3(0f, 0.35f, -4f);
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private float rotationSmoothSpeed = 18f;

    [Header("Collision")]
    [SerializeField] private LayerMask obstructionMask = ~0;
    [SerializeField] private float cameraRadius = 0.28f;
    [SerializeField] private float minThirdPersonDistance = 0.75f;
    [SerializeField] private float wallPadding = 0.08f;

    private Vector3 _positionVelocity;
    private bool _thirdPersonRequested;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (player == null || player.LookPivot == null || targetCamera == null)
            return;

        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            _thirdPersonRequested = !_thirdPersonRequested;

        Transform pivot = player.LookPivot;
        Vector3 firstPersonPosition = pivot.TransformPoint(firstPersonOffset);
        Vector3 desiredPosition = _thirdPersonRequested
            ? pivot.TransformPoint(thirdPersonOffset)
            : firstPersonPosition;

        bool forcedFirstPerson = false;
        if (_thirdPersonRequested)
            desiredPosition = ResolveThirdPersonCollision(firstPersonPosition, desiredPosition, out forcedFirstPerson);

        targetCamera.transform.position = Vector3.SmoothDamp(
            targetCamera.transform.position,
            desiredPosition,
            ref _positionVelocity,
            positionSmoothTime);

        Quaternion targetRotation = pivot.rotation;
        targetCamera.transform.rotation = Quaternion.Slerp(
            targetCamera.transform.rotation,
            targetRotation,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));

        player.SetThirdPersonVisualVisible(_thirdPersonRequested && !forcedFirstPerson);
    }

    public void SetPlayer(CameraNameTestPlayer newPlayer)
    {
        player = newPlayer;
    }

    public void SetCamera(Camera newCamera)
    {
        targetCamera = newCamera;
    }

    private Vector3 ResolveThirdPersonCollision(Vector3 firstPersonPosition, Vector3 desiredPosition, out bool forcedFirstPerson)
    {
        forcedFirstPerson = false;

        Vector3 direction = desiredPosition - firstPersonPosition;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            forcedFirstPerson = true;
            return firstPersonPosition;
        }

        direction /= distance;
        if (!Physics.SphereCast(firstPersonPosition, cameraRadius, direction, out RaycastHit hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
            return desiredPosition;

        float safeDistance = Mathf.Max(0f, hit.distance - cameraRadius - wallPadding);
        if (safeDistance < minThirdPersonDistance)
        {
            forcedFirstPerson = true;
            return firstPersonPosition;
        }

        return firstPersonPosition + direction * safeDistance;
    }
}
