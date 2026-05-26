using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fear System — pure MonoBehaviour, no Netcode required.
/// When integrated into the real player prefab, only the base class changes (MonoBehaviour → NetworkBehaviour).
///
/// Triggers:
///   - Enemy nearby  (Physics.OverlapSphere, Enemy layer)
///   - PlayerDiedEvent   (GameEventBus)
///   - PlayerDamagedEvent (GameEventBus)
///   - Dark environment  (RenderSettings.ambientIntensity)
///
/// Effects (0-100):
///   0-40  → vignette
///   40-70 → chromatic aberration
///   70-90 → lens distortion + speed penalty
///   90+   → panic: movement stops, screen flashes
///
/// Inspector connections:
///   - postProcessVolume : Global Volume in scene
///   - fearOverlay       : fullscreen red Image under Canvas (alpha 0)
///   - playerMovement    : TestPlayer script reference
/// </summary>
public class FearSystem : MonoBehaviour
{
    [Header("Fear Decay")]
    [Range(0f, 100f)] public float fearDecayRate   = 15f;
    [Range(0f, 100f)] public float darknessRate    = 3f;
    [Tooltip("How quickly the desired fear target follows distance changes.")]
    public float fearTargetFollowSpeed = 5f;

    [Header("Detection")]
    public float      nearDeathRadius   = 15f;
    public LayerMask  enemyLayer;
    public float      darkThreshold     = 0.3f;
    public float      enemyDetectRadius = 8f;
    [Tooltip("Extra radius used to fade fear out smoothly when entering/leaving detection range.")]
    public float      enemyFalloffBuffer = 2f;
    [Tooltip("How quickly enemy distance updates while moving closer to the enemy.")]
    public float      distanceApproachFollowSpeed = 2.5f;
    [Tooltip("How quickly enemy distance updates while moving away from the enemy.")]
    public float      distanceRetreatFollowSpeed = 8f;

    [Header("Distance → Fear Table")]
    [Tooltip("Fear target by enemy distance. Lower distance = higher fear. Must be ordered large to small.")]
    public DistanceFearEntry[] distanceFearTable = new DistanceFearEntry[]
    {
        new DistanceFearEntry { distance = 8f, fear = 10f },
        new DistanceFearEntry { distance = 5f, fear = 40f },
        new DistanceFearEntry { distance = 3f, fear = 70f },
        new DistanceFearEntry { distance = 1f, fear = 95f },
    };

    [Header("Event Triggers")]
    [Range(0f, 100f)] public float deathWitnessAdd = 30f;
    [Range(0f, 100f)] public float damageAdd       = 15f;

    [Header("Panic")]
    public float panicThreshold    = 90f;
    [Tooltip("Panic can trigger again only after fear falls to this level or lower.")]
    [Range(0f, 100f)] public float panicRearmFear = 75f;
    [Tooltip("Fear must stay above the panic threshold for this long before panic starts.")]
    public float panicTriggerHoldTime = 1.25f;
    public float panicStunDuration = 2f;
    public float panicCooldown     = 8f;

    [Header("Camera Shake")]
    [Tooltip("Camera transform to shake (usually Main Camera)")]
    public Transform shakeCamera;
    [Tooltip("Shake intensity")]
    public float shakeAmount    = 0.05f;
    [Tooltip("Shake speed")]
    public float shakeFrequency = 20f;
    [Tooltip("Blur amount applied before panic at very high fear.")]
    [Range(0f, 1.5f)] public float highFearBlurRadius = 0.45f;

    [Header("Movement Penalty")]
    [Tooltip("Speed multiplier at max fear (0.6 = 60%)")]
    [Range(0f, 1f)]
    public float maxFearSpeedMultiplier = 0.6f;
    public TestPlayer playerMovement;

    [Header("Heartbeat Audio")]
    [Tooltip("Optional AudioSource for heartbeat playback. If left empty, one is created automatically on this object.")]
    public AudioSource heartbeatSource;
    [Tooltip("Loopable heartbeat clip.")]
    public AudioClip heartbeatClip;
    [Range(0f, 100f)] public float heartbeatStartFear = 35f;
    [Range(0f, 1f)] public float heartbeatMaxVolume = 0.8f;
    public float heartbeatMinPitch = 0.9f;
    public float heartbeatMaxPitch = 1.35f;
    public float heartbeatFollowSpeed = 2.5f;

    [Header("Post Processing")]
    [Tooltip("Global Volume — must contain Vignette, ChromaticAberration, LensDistortion overrides")]
    public Volume postProcessVolume;

    [Header("UI Overlay")]
    [Tooltip("Fullscreen red Image under Canvas — start alpha at 0")]
    public UnityEngine.UI.Image fearOverlay;

    public float FearLevel => _fearLevel;
    public bool  IsInPanic => _panicLock;

    private Vignette            _vignette;
    private ChromaticAberration _chromatic;
    private LensDistortion      _lensDistortion;
    private DepthOfField        _dof;

    private float _fearLevel;
    private float _smoothedClosestDistance = float.MaxValue;
    private float _smoothedFearTarget;
    private float _panicThresholdTimer;
    private bool  _panicLock;
    private bool  _panicArmed = true;
    private bool  _stunned;
    private float _smoothedHeartbeatLevel;
    private Vector3 _cameraRestLocalPosition;
    private bool _hasCameraRestPosition;
    private ISpeedModifiable _speed;   // gercek PlayerMovement ya da test TestPlayer
    private Volume _runtimeVolume;     // sahnede Volume yoksa kod ile olusturulan
    private GameObject _runtimeOverlayGo;  // sahnede kirmizi overlay yoksa kod ile olusturulan

    // ---------------------------------------------------------------

    private void Start()
    {
        // Hiz cezasi icin ISpeedModifiable'i coz: elle atanan playerMovement (test sahnesi)
        // ya da bu oyuncudaki gercek PlayerMovement. Boylece hem standalone test sahnesinde
        // hem de networked gercek oyuncuda calisir.
        _speed = playerMovement as ISpeedModifiable;
        if (_speed == null) _speed = GetComponentInParent<ISpeedModifiable>();

        // Sahne Global Volume'u prefab uzerinde elle atanamaz; runtime'da otomatik bul.
        if (postProcessVolume == null) postProcessVolume = FindFirstObjectByType<Volume>();
        // Sahnede hic Volume yoksa kendi post-process Volume'umuzu kod ile olustur
        // (Anil'in efektleri icin elle Volume kurulumu gerekmesin).
        if (postProcessVolume == null) postProcessVolume = CreateRuntimeVolume();
        // URP'de kameranin post-processing'i kapali ise efektler gorunmez — ac.
        EnableCameraPostProcessing();
        // Sahnede tam-ekran kirmizi korku overlay'i yoksa kod ile olustur (Anil'in kirmizi efekti).
        if (fearOverlay == null) fearOverlay = CreateRuntimeOverlay();

        if (postProcessVolume != null)
        {
            postProcessVolume.profile.TryGet(out _vignette);
            postProcessVolume.profile.TryGet(out _chromatic);
            postProcessVolume.profile.TryGet(out _lensDistortion);
            postProcessVolume.profile.TryGet(out _dof);
        }

        CacheCameraRestPosition();
        ConfigureHeartbeatSource();

        GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
        GameEventBus.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
    }

    private void OnDestroy()
    {
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
        GameEventBus.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
        ResetCameraPosition();

        if (_runtimeVolume != null)
        {
            if (_runtimeVolume.profile != null) Destroy(_runtimeVolume.profile);
            Destroy(_runtimeVolume.gameObject);
        }
        if (_runtimeOverlayGo != null) Destroy(_runtimeOverlayGo);
    }

    /// <summary>
    /// Sahnede Volume yoksa post-process efektleri (vignette, kromatik, lens, blur) icin
    /// kod ile global bir Volume + profil olusturur. Boylece elle Volume kurulumu gerekmez.
    /// </summary>
    private Volume CreateRuntimeVolume()
    {
        var go = new GameObject("FearSystem_RuntimeVolume");
        go.transform.SetParent(transform, false);

        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 100f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vol.profile = profile;

        var vig = profile.Add<Vignette>(true);
        vig.intensity.value = 0.2f;
        vig.color.value = new Color(0.5f, 0f, 0f);   // kirmizi vignette (Anil'in sahnesindeki gibi)

        var ca = profile.Add<ChromaticAberration>(true);
        ca.intensity.value = 0f;

        var ld = profile.Add<LensDistortion>(true);
        ld.intensity.value = 0f;

        var dof = profile.Add<DepthOfField>(true);
        dof.mode.value = DepthOfFieldMode.Gaussian;
        dof.gaussianMaxRadius.value = 0f;
        dof.active = true;

        _runtimeVolume = vol;
        Debug.Log("[FearSystem] Runtime post-process Volume olusturuldu.");
        return vol;
    }

    /// <summary>
    /// Sahnede tam-ekran kirmizi korku overlay'i yoksa kod ile olusturur (Anil'in
    /// FearSceneTest'teki kirmizi efektin ayni si). Alpha'si korkuyla ApplyVisualEffects'te artar.
    /// </summary>
    private UnityEngine.UI.Image CreateRuntimeOverlay()
    {
        var canvasGo = new GameObject("FearSystem_RuntimeOverlay");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;   // her seyin ustunde
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

        var imgGo = new GameObject("Overlay");
        imgGo.transform.SetParent(canvasGo.transform, false);
        var img = imgGo.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.55f, 0f, 0f, 0f);   // kirmizi, baslangic alpha 0
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _runtimeOverlayGo = canvasGo;
        return img;
    }

    /// <summary>URP'de aktif kameranin post-processing'ini acar (kapaliysa efekt gorunmez).</summary>
    private void EnableCameraPostProcessing()
    {
        var cam = Camera.main;
        if (cam == null) return;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null) data.renderPostProcessing = true;
    }

    // ---------------------------------------------------------------

    private void Update()
    {
        if (_stunned) return;

        float dt          = Time.deltaTime;
        float closestDist = GetClosestEnemyDistance();
        closestDist = GetSmoothedEnemyDistance(closestDist, dt);
        bool  dark        = RenderSettings.ambientIntensity < darkThreshold;

        // Calculate target fear based on distance
        float fearTarget = 0f;
        if (closestDist < GetEnemySearchRadius())
            fearTarget = GetFearForDistance(closestDist);

        if (dark) fearTarget = Mathf.Max(fearTarget, 30f);

        // Tehdit yaklasinca korku ANINDA hedefe ziplar (mesafeye gore direkt tepki);
        // tehdit uzaklasinca yavasca iner (gerilim kademeli dagilir).
        _smoothedFearTarget = fearTarget;
        if (fearTarget > _fearLevel)
            _fearLevel = fearTarget;
        else
            _fearLevel = Mathf.MoveTowards(_fearLevel, fearTarget, fearDecayRate * dt);

        ApplyVisualEffects(_fearLevel);
        ApplySpeedPenalty(_fearLevel);
        UpdateHeartbeat(_fearLevel, dt);

        if (!_panicArmed && _fearLevel <= panicRearmFear)
            _panicArmed = true;

        if (_fearLevel >= panicThreshold && !_panicLock && _panicArmed)
            _panicThresholdTimer += dt;
        else
            _panicThresholdTimer = 0f;

        if (_panicThresholdTimer >= panicTriggerHoldTime)
            StartCoroutine(TriggerPanic());
    }

    // ---------------------------------------------------------------
    // Event callbacks

    private void OnPlayerDied(PlayerDiedEvent e)
    {
        if (Vector3.Distance(transform.position, e.DeathPosition) <= nearDeathRadius)
            AddFear(deathWitnessAdd);
    }

    private void OnPlayerDamaged(PlayerDamagedEvent e) => AddFear(damageAdd);

    /// <summary>Add fear from external source (other systems, test buttons, etc.).</summary>
    public void AddFear(float amount)
    {
        _fearLevel = Mathf.Clamp(_fearLevel + amount, 0f, 100f);
    }

    /// <summary>Reset fear to zero immediately.</summary>
    public void ResetFear()
    {
        _fearLevel = 0f;
        _smoothedClosestDistance = float.MaxValue;
        _smoothedFearTarget = 0f;
        _panicThresholdTimer = 0f;
        _panicArmed = true;
        _smoothedHeartbeatLevel = 0f;
        ApplyVisualEffects(0f);
        ResetCameraPosition();
        UpdateHeartbeat(0f, 0f);
        _speed?.SetSpeedMultiplier(1f);
    }

    // ---------------------------------------------------------------
    // Distance helpers

    private float GetFearForDistance(float dist)
    {
        if (distanceFearTable == null || distanceFearTable.Length == 0) return 0f;

        float searchRadius = GetEnemySearchRadius();
        if (dist >= searchRadius) return 0f;

        float farthestDistance = distanceFearTable[0].distance;
        if (dist > farthestDistance)
        {
            float edgeT = Mathf.InverseLerp(searchRadius, farthestDistance, dist);
            return Mathf.Lerp(0f, distanceFearTable[0].fear, edgeT);
        }

        // Table is ordered large→small distance (e.g. 8,5,3,1)
        // Find two entries that bracket the current distance
        for (int i = 0; i < distanceFearTable.Length - 1; i++)
        {
            float dFar  = distanceFearTable[i].distance;
            float dNear = distanceFearTable[i + 1].distance;

            if (dist <= dFar && dist >= dNear)
            {
                float t = Mathf.InverseLerp(dFar, dNear, dist);
                return Mathf.Lerp(distanceFearTable[i].fear, distanceFearTable[i + 1].fear, t);
            }
        }

        // Closer than the smallest entry → return max fear
        if (dist < distanceFearTable[distanceFearTable.Length - 1].distance)
            return distanceFearTable[distanceFearTable.Length - 1].fear;

        // Further than the largest entry → return min fear
        return distanceFearTable[0].fear;
    }

    private float GetClosestEnemyDistance()
    {
        Collider[] hits    = Physics.OverlapSphere(transform.position, GetEnemySearchRadius(), enemyLayer);
        float      closest = float.MaxValue;
        foreach (var h in hits)
        {
            Vector3 closestPoint = h.ClosestPoint(transform.position);
            float d = Vector3.Distance(transform.position, closestPoint);
            if (d < closest) closest = d;
        }
        return closest;
    }

    private float GetSmoothedEnemyDistance(float rawDistance, float dt)
    {
        if (rawDistance == float.MaxValue)
            rawDistance = GetEnemySearchRadius();

        if (_smoothedClosestDistance == float.MaxValue)
        {
            _smoothedClosestDistance = rawDistance;
            return _smoothedClosestDistance;
        }

        bool approaching = rawDistance < _smoothedClosestDistance;
        // Yaklasirken hizli guncelle (tehdit aninda hissedilsin), uzaklasirken yumusat.
        float followSpeed = approaching
            ? Mathf.Max(distanceApproachFollowSpeed, 25f)
            : distanceRetreatFollowSpeed;
        _smoothedClosestDistance = Mathf.MoveTowards(_smoothedClosestDistance, rawDistance, followSpeed * dt);
        return _smoothedClosestDistance;
    }

    private float GetEnemySearchRadius()
    {
        return enemyDetectRadius + Mathf.Max(0f, enemyFalloffBuffer);
    }

    // ---------------------------------------------------------------
    // Visual effects

    private void ApplyVisualEffects(float fear)
    {
        float t = fear / 100f;

        if (_vignette != null)
            _vignette.intensity.Override(Mathf.Lerp(0.2f, 0.65f, t));

        if (_chromatic != null)
            _chromatic.intensity.Override(
                Mathf.Lerp(0f, 1f, Mathf.InverseLerp(0.1f, 0.8f, t)));

        if (_lensDistortion != null)
            _lensDistortion.intensity.Override(
                Mathf.Lerp(0f, -0.35f, Mathf.InverseLerp(0.7f, 1f, t)));

        if (_dof != null && !_panicLock)
        {
            float blur = Mathf.Lerp(0f, highFearBlurRadius, Mathf.InverseLerp(0.75f, 1f, t));
            _dof.gaussianMaxRadius.Override(blur);
        }

        if (fearOverlay != null)
        {
            Color c = fearOverlay.color;
            c.a = Mathf.Lerp(0f, 0.42f, t);
            fearOverlay.color = c;
        }
    }

    private void CacheCameraRestPosition()
    {
        if (shakeCamera == null) return;
        _cameraRestLocalPosition = shakeCamera.localPosition;
        _hasCameraRestPosition = true;
    }

    private void ResetCameraPosition()
    {
        if (shakeCamera == null || !_hasCameraRestPosition) return;
        shakeCamera.localPosition = _cameraRestLocalPosition;
    }

    private void ApplySpeedPenalty(float fear)
    {
        if (_speed == null) return;
        float penalty = Mathf.InverseLerp(70f, 100f, fear);
        _speed.SetSpeedMultiplier(Mathf.Lerp(1f, maxFearSpeedMultiplier, penalty));
    }

    private void ConfigureHeartbeatSource()
    {
        if (heartbeatSource == null)
            heartbeatSource = GetComponent<AudioSource>();

        if (heartbeatSource == null)
            heartbeatSource = gameObject.AddComponent<AudioSource>();

        heartbeatSource.playOnAwake = false;
        heartbeatSource.loop = true;
        heartbeatSource.spatialBlend = 0f;
        heartbeatSource.volume = 0f;
        heartbeatSource.pitch = heartbeatMinPitch;

        if (heartbeatClip != null)
            heartbeatSource.clip = heartbeatClip;
    }

    private void UpdateHeartbeat(float fear, float dt)
    {
        if (heartbeatSource == null) return;

        if (heartbeatClip != null && heartbeatSource.clip != heartbeatClip)
            heartbeatSource.clip = heartbeatClip;

        if (heartbeatSource.clip == null)
        {
            if (heartbeatSource.isPlaying)
                heartbeatSource.Stop();
            return;
        }

        float targetLevel = Mathf.InverseLerp(heartbeatStartFear, 100f, fear);
        if (dt > 0f)
        {
            _smoothedHeartbeatLevel = Mathf.MoveTowards(
                _smoothedHeartbeatLevel,
                targetLevel,
                heartbeatFollowSpeed * dt);
        }
        else
        {
            _smoothedHeartbeatLevel = targetLevel;
        }

        heartbeatSource.volume = Mathf.Lerp(0f, heartbeatMaxVolume, _smoothedHeartbeatLevel);
        heartbeatSource.pitch = Mathf.Lerp(heartbeatMinPitch, heartbeatMaxPitch, _smoothedHeartbeatLevel);

        if (_smoothedHeartbeatLevel > 0.001f)
        {
            if (!heartbeatSource.isPlaying)
                heartbeatSource.Play();
        }
        else if (heartbeatSource.isPlaying)
        {
            heartbeatSource.Stop();
        }
    }

    // ---------------------------------------------------------------
    // Panic

    private IEnumerator TriggerPanic()
    {
        _panicLock = true;
        _panicArmed = false;
        _panicThresholdTimer = 0f;
        _stunned   = true;

        _speed?.SetSpeedMultiplier(0f);
        ResetCameraPosition();

        // Camera shake + blur during stun
        if (shakeCamera != null) StartCoroutine(ShakeCamera(panicStunDuration));
        StartCoroutine(PanicBlur(panicStunDuration));

        yield return new WaitForSeconds(panicStunDuration);

        _stunned = false;
        _speed?.SetSpeedMultiplier(1f);

        yield return new WaitForSeconds(panicCooldown);
        _panicLock = false;
    }

    private IEnumerator PanicBlur(float duration)
    {
        if (_dof == null) yield break;

        float elapsed = 0f;
        float halfDur = duration * 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Fade in first half, fade out second half
            float t = elapsed < halfDur
                ? elapsed / halfDur
                : 1f - ((elapsed - halfDur) / halfDur);

            _dof.gaussianMaxRadius.Override(Mathf.Lerp(0f, 1.5f, t));
            yield return null;
        }

        _dof.gaussianMaxRadius.Override(0f);
    }

    private IEnumerator ShakeCamera(float duration)
    {
        Vector3 origin  = _hasCameraRestPosition ? _cameraRestLocalPosition : shakeCamera.localPosition;
        float   elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Intensity fades out toward the end
            float fade   = 1f - (elapsed / duration);
            float offsetX = Mathf.Sin(elapsed * shakeFrequency)              * shakeAmount * fade;
            float offsetY = Mathf.Sin(elapsed * shakeFrequency * 1.3f + 1f) * shakeAmount * fade;

            shakeCamera.localPosition = origin + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        shakeCamera.localPosition = origin;
    }

    // ---------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, enemyDetectRadius);
        Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, GetEnemySearchRadius());
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, nearDeathRadius);

        // Show distance threshold rings
        if (distanceFearTable != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            foreach (var entry in distanceFearTable)
                Gizmos.DrawWireSphere(transform.position, entry.distance);
        }
    }
}

[System.Serializable]
public struct DistanceFearEntry
{
    [Tooltip("At this distance (metres)")]
    public float distance;
    [Tooltip("Target fear value (0-100)")]
    [Range(0f, 100f)]
    public float fear;
}
