using TMPro;
using UnityEngine;

public class PlayerNameplate : MonoBehaviour
{
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.25f, 0f);
    [SerializeField] private string fallbackName = "Player";

    private Camera _camera;

    private void Awake()
    {
        _camera = Camera.main;

        if (label == null)
            label = GetComponentInChildren<TextMeshPro>();
    }

    private void Start()
    {
        string savedName = PlayerPrefs.GetString("DisplayName", string.Empty).Trim();
        SetName(string.IsNullOrWhiteSpace(savedName) ? fallbackName : savedName);
    }

    private void LateUpdate()
    {
        if (followTarget != null)
            transform.position = followTarget.position + worldOffset;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera != null)
            transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position);
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    public void SetName(string displayName)
    {
        if (label != null)
            label.text = string.IsNullOrWhiteSpace(displayName) ? fallbackName : displayName.Trim();
    }
}
