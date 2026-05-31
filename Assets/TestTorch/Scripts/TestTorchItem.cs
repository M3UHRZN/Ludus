using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Networked flashlight item for VoidHaul testing.
/// Only the player holding this item can request a toggle; the server owns the
/// replicated light state so every client sees the same flashlight beam.
/// </summary>
public class TestTorchItem : EquippableItem
{
    [Header("Light")]
    [SerializeField] private Light torchLight;
    [SerializeField] private float enabledIntensity = 7f;
    [SerializeField] private float enabledRange = 22f;
    [SerializeField] private float enabledSpotAngle = 42f;

    [Header("Visual")]
    [SerializeField] private Renderer emissionRenderer;
    [SerializeField] private string emissionProperty = "_EmissionColor";
    [SerializeField] private Color emissionColor = new(1f, 0.92f, 0.72f, 1f);
    [SerializeField] private float emissionMultiplier = 2.2f;

    [Header("Local Held View Pose")]
    [SerializeField] private bool useLocalHeldViewPose = true;
    [SerializeField] private Vector3 heldVisualCameraOffset = new(-0.34f, -0.28f, 0.56f);
    [SerializeField] private Vector3 heldVisualEulerOffset = new(-18f, -12f, 8f);
    [Tooltip("Light pozisyonu — visual'a göre local. (0,0,Z) = barrel tip yönünde Z metre.")]
    [SerializeField] private Vector3 lightLocalTipOffset = new(0f, 0f, 0.5f);
    [Tooltip("Light rotasyon offset — model'in barrel ekseni Z+ değilse buradan düzelt.")]
    [SerializeField] private Vector3 lightLocalEulerOffset = new(0f, 0f, 0f);
    [SerializeField] private Vector3 heldLightCameraOffset = new(-0.12f, -0.10f, 0.18f);

    [Header("Cookie")]
    [SerializeField] private Texture[] lightCookies;
    [SerializeField] private byte defaultCookieIndex;

    [Header("Audio")]
    [SerializeField] private AudioSource toggleAudioSource;
    [Tooltip("The same click clip is played when the torch turns on and off.")]
    [SerializeField] private AudioClip toggleClip;
    [SerializeField] [Range(0f, 1f)] private float toggleVolume = 0.7f;

    [Header("Battery")]
    [Tooltip("Maksimum pil suresi (saniye). Isik acikken eksilir, 0'da otomatik kapanir.")]
    [SerializeField] private float batteryDuration = 60f;

    [Header("Enemy Stun")]
    [Tooltip("Isigi enemy'ye tutunca uygulanan stun suresi (saniye).")]
    [SerializeField] private float enemyStunDuration = 1f;
    [Tooltip("Ayni enemy'nin tekrar stunlanabilmesi icin gecmesi gereken sure (saniye).")]
    [SerializeField] private float enemyStunCooldown = 5f;
    [Tooltip("Isigin enemy'yi etkileyebilecegi maksimum mesafe (metre).")]
    [SerializeField] private float enemyMaxRange = 12f;
    [Tooltip("Enemy stun konisinin yari aci (derece). Spot acisinin yarisina yakin olmali.")]
    [SerializeField] private float enemyConeHalfAngle = 22f;
    [Tooltip("Enemy ve LOS (line of sight) check'i icin layer maskesi. Default: Everything.")]
    [SerializeField] private LayerMask enemyDetectionMask = ~0;

    public readonly NetworkVariable<bool> NetLightOn = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<byte> NetCookieIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>Kalan pil yuzdesi (0..1). UI bagi icin de okunabilir.</summary>
    public readonly NetworkVariable<float> NetBatteryRemaining = new(
        -1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PhysicsObject _physicsObject;
    private Transform _visualTransform;
    private Collider _rootCollider;
    private bool _heldColliderDisabled;
    private Vector3 _defaultVisualLocalPosition;
    private Quaternion _defaultVisualLocalRotation;
    private Vector3 _defaultVisualLocalScale;
    private Vector3 _defaultLightLocalPosition;
    private Quaternion _defaultLightLocalRotation;
    private bool _viewPoseActive;

    // Server-only: per-enemy stun cooldown (enemy instance id -> next allowed time).
    private readonly Dictionary<int, float> _enemyStunCooldownByInstance = new();
    // Server-only: overlap buffer for stun scan (alloc once, reuse).
    private static readonly Collider[] s_overlapBuffer = new Collider[32];

    private void Awake()
    {
        _physicsObject = GetComponent<PhysicsObject>();
        _rootCollider = GetComponent<Collider>();
        _visualTransform = emissionRenderer != null ? emissionRenderer.transform : null;

        if (_visualTransform != null)
        {
            _defaultVisualLocalPosition = _visualTransform.localPosition;
            _defaultVisualLocalRotation = _visualTransform.localRotation;
            _defaultVisualLocalScale = _visualTransform.localScale;
        }

        if (torchLight != null)
        {
            _defaultLightLocalPosition = torchLight.transform.localPosition;
            _defaultLightLocalRotation = torchLight.transform.localRotation;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            NetCookieIndex.Value = ClampCookieIndex(defaultCookieIndex);
            // Yeni spawn olmus torch -> dolu pil. (Inventory'den her equip = yeni spawn.)
            NetBatteryRemaining.Value = Mathf.Max(0f, batteryDuration);
        }

        NetLightOn.OnValueChanged += OnLightStateChanged;
        NetCookieIndex.OnValueChanged += OnCookieChanged;
        if (_physicsObject != null)
            _physicsObject.NetIsHeld.OnValueChanged += OnHeldChanged;
        ApplyLightState(NetLightOn.Value);
        ApplyCookie(NetCookieIndex.Value);
        ApplyHeldCollider(_physicsObject != null && _physicsObject.NetIsHeld.Value);
    }

    public override void OnNetworkDespawn()
    {
        NetLightOn.OnValueChanged -= OnLightStateChanged;
        NetCookieIndex.OnValueChanged -= OnCookieChanged;
        if (_physicsObject != null)
            _physicsObject.NetIsHeld.OnValueChanged -= OnHeldChanged;
        ApplyHeldCollider(false);
        base.OnNetworkDespawn();
    }

    private void OnHeldChanged(bool previous, bool current) => ApplyHeldCollider(current);

    private void ApplyHeldCollider(bool isHeld)
    {
        if (_rootCollider == null) return;
        // Torch elde tutulurken collider'ı kapat: spring sistemi sürekli ileri
        // çekerken cube gibi objelere çarpıp onları zıplatmasını engeller.
        // Bırakıldığında collider geri açılır (pickup raycast'i ve fizik için).
        if (isHeld && !_heldColliderDisabled)
        {
            _rootCollider.enabled = false;
            _heldColliderDisabled = true;
        }
        else if (!isHeld && _heldColliderDisabled)
        {
            _rootCollider.enabled = true;
            _heldColliderDisabled = false;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        // Local input (sadece eldeki torch icin)
        if (IsHeldByLocalClient() && Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
            RequestToggleLightRpc();

        // Server-side: pil tuketim + enemy stun (sadece isik acikken)
        if (IsServer && NetLightOn.Value)
            ServerTickWhileLightOn(Time.deltaTime);
    }

    private void ServerTickWhileLightOn(float deltaTime)
    {
        // 1) Pil tuket
        if (batteryDuration > 0f)
        {
            float newValue = NetBatteryRemaining.Value - deltaTime;
            if (newValue <= 0f)
            {
                NetBatteryRemaining.Value = 0f;
                NetLightOn.Value = false;
                return;
            }
            NetBatteryRemaining.Value = newValue;
        }

        // 2) Enemy stun: torch onunde, koni icinde, LOS varsa stun uygula
        if (torchLight == null) return;
        TryStunEnemiesInBeam();
    }

    private void TryStunEnemiesInBeam()
    {
        Transform lightTransform = torchLight.transform;
        Vector3 origin = lightTransform.position;
        Vector3 forward = lightTransform.forward;
        float now = Time.time;
        float cosHalfAngle = Mathf.Cos(enemyConeHalfAngle * Mathf.Deg2Rad);

        int hitCount = Physics.OverlapSphereNonAlloc(origin, enemyMaxRange, s_overlapBuffer, enemyDetectionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = s_overlapBuffer[i];
            if (col == null) continue;

            EnemyController enemy = col.GetComponent<EnemyController>();
            if (enemy == null) enemy = col.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.IsAlive) continue;

            // Koni icinde mi?
            Vector3 toEnemy = enemy.transform.position - origin;
            float distance = toEnemy.magnitude;
            if (distance < 0.001f) continue;
            Vector3 toEnemyDir = toEnemy / distance;
            if (Vector3.Dot(forward, toEnemyDir) < cosHalfAngle) continue;

            // LOS: arada duvar var mi?
            if (Physics.Raycast(origin, toEnemyDir, out RaycastHit losHit, distance + 0.1f,
                    enemyDetectionMask, QueryTriggerInteraction.Ignore))
            {
                if (losHit.collider != col &&
                    !losHit.collider.transform.IsChildOf(enemy.transform) &&
                    losHit.collider.transform != enemy.transform)
                {
                    continue; // duvar arkasinda
                }
            }

            // Per-enemy cooldown
            int id = enemy.GetInstanceID();
            if (_enemyStunCooldownByInstance.TryGetValue(id, out float nextAllowed) && now < nextAllowed)
                continue;

            enemy.SetStunned(true, enemyStunDuration);
            _enemyStunCooldownByInstance[id] = now + enemyStunCooldown;
        }
    }

    private void LateUpdate()
    {
        if (!useLocalHeldViewPose || !IsSpawned)
            return;

        if (IsHeldByLocalClient())
        {
            ApplyLocalHeldViewPose();
            return;
        }

        RestoreDefaultLocalPose();
    }

    private bool IsHeldByLocalClient()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || _physicsObject == null)
            return false;

        return _physicsObject.NetIsHeld.Value &&
               _physicsObject.NetGrabberClientId.Value == networkManager.LocalClientId;
    }

    /// <summary>
    /// Local oyuncunun kamera referansini bul. Once Camera.main, sonra local
    /// player'in PlayerLook.CameraTarget'ini fallback olarak kullanir.
    /// Lobby gibi MainCamera tag'i olmayan sahnelerde calismaya devam eder.
    /// </summary>
    private Transform ResolveLocalCameraTransform()
    {
        Camera main = Camera.main;
        if (main != null) return main.transform;

        // Fallback: local player'in CameraTarget'i (cinemachine vcam'a feed eden transform)
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null) return null;

        if (nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
        {
            PlayerLook look = nm.LocalClient.PlayerObject.GetComponent<PlayerLook>();
            if (look != null && look.CameraTarget != null)
                return look.CameraTarget;
        }

        // Son care: sahnedeki tum PlayerLook'lar arasinda owner olanin CameraTarget'i.
        PlayerLook[] looks = FindObjectsByType<PlayerLook>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (PlayerLook look in looks)
        {
            if (look != null && look.IsOwner && look.CameraTarget != null)
                return look.CameraTarget;
        }

        return null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestToggleLightRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || _physicsObject == null)
            return;

        if (!_physicsObject.NetIsHeld.Value)
            return;

        if (_physicsObject.NetGrabberClientId.Value != rpcParams.Receive.SenderClientId)
            return;

        // Pil bittiyse acmaya calismayi engelle (kapamayi engelleme).
        bool turningOn = !NetLightOn.Value;
        if (turningOn && batteryDuration > 0f && NetBatteryRemaining.Value <= 0f)
            return;

        NetLightOn.Value = !NetLightOn.Value;
        PlayToggleAudioRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void PlayToggleAudioRpc()
    {
        if (toggleAudioSource == null || toggleClip == null)
            return;

        toggleAudioSource.PlayOneShot(toggleClip, toggleVolume);
    }

    private void OnLightStateChanged(bool previous, bool current)
    {
        ApplyLightState(current);
    }

    private void OnCookieChanged(byte previous, byte current)
    {
        ApplyCookie(current);
    }

    private void ApplyLightState(bool isOn)
    {
        if (torchLight != null)
        {
            torchLight.enabled = isOn;
            torchLight.intensity = isOn ? enabledIntensity : 0f;
            torchLight.range = enabledRange;
            torchLight.spotAngle = enabledSpotAngle;
        }

        if (emissionRenderer != null && emissionRenderer.material != null)
        {
            Color color = isOn ? emissionColor * emissionMultiplier : Color.black;
            emissionRenderer.material.SetColor(emissionProperty, color);
        }
    }

    private void ApplyCookie(byte cookieIndex)
    {
        if (torchLight == null || lightCookies == null || lightCookies.Length == 0)
            return;

        int index = Mathf.Clamp(cookieIndex, 0, lightCookies.Length - 1);
        torchLight.cookie = lightCookies[index];
    }

    private byte ClampCookieIndex(byte index)
    {
        if (lightCookies == null || lightCookies.Length == 0)
            return 0;

        return (byte)Mathf.Clamp(index, 0, lightCookies.Length - 1);
    }

    private void ApplyLocalHeldViewPose()
    {
        Transform cameraTransform = ResolveLocalCameraTransform();
        if (cameraTransform == null)
            return;

        if (_visualTransform != null)
        {
            _visualTransform.position = cameraTransform.TransformPoint(heldVisualCameraOffset);
            _visualTransform.rotation = cameraTransform.rotation * Quaternion.Euler(heldVisualEulerOffset);
            _visualTransform.localScale = _defaultVisualLocalScale;
        }

        if (torchLight != null)
        {
            Transform lightTransform = torchLight.transform;
            if (_visualTransform != null)
            {
                // Light visual ile birlikte: torch tip'inden çıkar, torch'un baktığı yöne gider.
                lightTransform.position = _visualTransform.TransformPoint(lightLocalTipOffset);
                lightTransform.rotation = _visualTransform.rotation * Quaternion.Euler(lightLocalEulerOffset);
            }
            else
            {
                lightTransform.position = cameraTransform.TransformPoint(heldLightCameraOffset);
                lightTransform.rotation = cameraTransform.rotation;
            }
        }

        _viewPoseActive = true;
    }

    private void RestoreDefaultLocalPose()
    {
        if (!_viewPoseActive)
            return;

        if (_visualTransform != null)
        {
            _visualTransform.localPosition = _defaultVisualLocalPosition;
            _visualTransform.localRotation = _defaultVisualLocalRotation;
            _visualTransform.localScale = _defaultVisualLocalScale;
        }

        if (torchLight != null)
        {
            torchLight.transform.localPosition = _defaultLightLocalPosition;
            torchLight.transform.localRotation = _defaultLightLocalRotation;
        }

        _viewPoseActive = false;
    }
}
