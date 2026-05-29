using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Per-player "blinded" feedback: fullscreen white flash, ringing audio, and
/// temporary move/look penalty. Server calls ServerBlind on a victim's own
/// component; the effect renders only on that victim's owning client.
/// </summary>
public class PlayerFlashEffect : NetworkBehaviour
{
    [SerializeField] [Range(0f, 1f)] private float blindedMoveMultiplier = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float blindedLookMultiplier = 0.25f;
    [SerializeField] private AudioClip ringingClip;
    [SerializeField] [Range(0f, 1f)] private float ringingVolume = 0.7f;
    [SerializeField] private float audioExtraFadeTime = 1f;

    private Image _flashOverlay;
    private Coroutine _flashRoutine;
    private PlayerMovement _movement;
    private PlayerLook _look;
    private AudioSource _audioSource;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _look = GetComponent<PlayerLook>();
    }

    /// <summary>Server-only: blind THIS player. Routes to the owning client via RPC.</summary>
    public void ServerBlind(float duration, float alpha)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;
        ApplyBlindRpc(duration, Mathf.Clamp01(alpha));
    }

    [Rpc(SendTo.Owner)]
    private void ApplyBlindRpc(float duration, float alpha)
    {
        EnsureFlashOverlay();
        EnsureAudio();

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(BlindRoutine(duration, alpha));
    }

    private IEnumerator BlindRoutine(float duration, float alpha)
    {
        float safeDuration = Mathf.Max(0.05f, duration);
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (_movement != null) _movement.SetSpeedMultiplier(blindedMoveMultiplier);
        if (_look != null) _look.SetLookMultiplier(blindedLookMultiplier);

        if (_flashOverlay != null)
        {
            _flashOverlay.enabled = true;
            _flashOverlay.color = new Color(1f, 1f, 1f, clampedAlpha);
        }

        if (_audioSource != null && ringingClip != null)
        {
            if (ringingClip.loadState == AudioDataLoadState.Unloaded)
                ringingClip.LoadAudioData();
            _audioSource.Stop();
            _audioSource.clip = ringingClip;
            _audioSource.time = 0f;
            _audioSource.volume = ringingVolume * clampedAlpha;
            _audioSource.Play();
        }

        float audioDuration = safeDuration + Mathf.Max(0f, audioExtraFadeTime);
        float timer = 0f;
        while (timer < audioDuration)
        {
            timer += Time.deltaTime;
            float visualT = Mathf.Clamp01(timer / safeDuration);
            float visualFade = 1f - Mathf.SmoothStep(0f, 1f, visualT);

            if (_flashOverlay != null)
                _flashOverlay.color = new Color(1f, 1f, 1f, clampedAlpha * visualFade);

            if (_audioSource != null && _audioSource.isPlaying)
            {
                float audioT = Mathf.Clamp01(timer / audioDuration);
                float audioFade = 1f - Mathf.SmoothStep(0f, 1f, audioT);
                _audioSource.volume = ringingVolume * clampedAlpha * audioFade;
            }

            if (_movement != null)
                _movement.SetSpeedMultiplier(Mathf.Lerp(blindedMoveMultiplier, 1f, visualT));
            if (_look != null)
                _look.SetLookMultiplier(Mathf.Lerp(blindedLookMultiplier, 1f, visualT));

            yield return null;
        }

        if (_flashOverlay != null)
        {
            _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            _flashOverlay.enabled = false;
        }
        if (_movement != null) _movement.SetSpeedMultiplier(1f);
        if (_look != null) _look.SetLookMultiplier(1f);
        if (_audioSource != null) { _audioSource.Stop(); _audioSource.volume = 0f; }
        _flashRoutine = null;
    }

    private void EnsureFlashOverlay()
    {
        if (_flashOverlay != null) return;

        GameObject canvasObject = new GameObject("FlashbangRuntimeCanvas");
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObject = new GameObject("FlashOverlay");
        overlayObject.transform.SetParent(canvasObject.transform, false);
        _flashOverlay = overlayObject.AddComponent<Image>();
        _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        _flashOverlay.raycastTarget = false;
        _flashOverlay.enabled = false;

        RectTransform rect = _flashOverlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureAudio()
    {
        if (_audioSource != null) return;
        Transform existing = transform.Find("FlashbangAudioSource");
        GameObject audioObject = existing != null ? existing.gameObject : new GameObject("FlashbangAudioSource");
        audioObject.transform.SetParent(transform, false);
        _audioSource = audioObject.GetComponent<AudioSource>() ?? audioObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 0f;
        if (ringingClip != null && ringingClip.loadState == AudioDataLoadState.Unloaded)
            ringingClip.LoadAudioData();
    }
}
