using TMPro;
using Unity.Netcode;
using UnityEngine;

public class PlayerWorldNameplate : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerDisplayName displayNameSource;
    [SerializeField] private PlayerCameraModeController cameraMode;
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Transform followTarget;

    [Header("Presentation")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.15f, 0f);
    [SerializeField] private float fontSize = 1.25f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private float outlineWidth = 0.22f;
    [SerializeField] private bool hideOwnerInFirstPerson = true;

    private Camera _camera;

    private void Awake()
    {
        if (displayNameSource == null)
            displayNameSource = GetComponent<PlayerDisplayName>();

        if (cameraMode == null)
            cameraMode = GetComponent<PlayerCameraModeController>();

        if (followTarget == null)
            followTarget = transform;

        EnsureLabel();
    }

    public override void OnNetworkSpawn()
    {
        if (displayNameSource != null)
        {
            displayNameSource.DisplayNameChanged += SetName;
            SetName(displayNameSource.DisplayName);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (displayNameSource != null)
            displayNameSource.DisplayNameChanged -= SetName;
    }

    private void LateUpdate()
    {
        if (label == null)
            return;

        bool visible = ShouldShowNameplate();
        if (label.gameObject.activeSelf != visible)
            label.gameObject.SetActive(visible);

        if (!visible)
            return;

        label.transform.position = followTarget.position + worldOffset;

        if (_camera == null)
            _camera = Camera.main;

        if (_camera != null)
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - _camera.transform.position);
    }

    private bool ShouldShowNameplate()
    {
        if (!hideOwnerInFirstPerson || !IsOwner)
            return true;

        return cameraMode != null && cameraMode.IsEffectivelyThirdPerson;
    }

    private void EnsureLabel()
    {
        if (label == null)
            label = GetComponentInChildren<TextMeshPro>();

        if (label == null)
        {
            GameObject labelObject = new GameObject("NameplateLabel");
            labelObject.transform.SetParent(transform, false);
            label = labelObject.AddComponent<TextMeshPro>();
        }

        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.fontSize = fontSize;
        label.color = textColor;
        label.outlineColor = outlineColor;
        label.outlineWidth = outlineWidth;

        if (label.font == null && TMP_Settings.defaultFontAsset != null)
            label.font = TMP_Settings.defaultFontAsset;

        RectTransform rectTransform = label.rectTransform;
        rectTransform.sizeDelta = new Vector2(4f, 0.6f);
    }

    private void SetName(string displayName)
    {
        EnsureLabel();
        label.text = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
    }
}
