using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local flashbang feedback prototype.
/// Handles a white screen overlay fade and optional ringing audio.
/// Kept standalone on purpose so it can be tested before network/gameplay integration.
/// </summary>
public class FlashbangEffect : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Image flashOverlay;
    [SerializeField] [Range(0f, 1f)] private float peakAlpha = 1f;
    [SerializeField] private float blindDuration = 2.5f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Audio")]
    [SerializeField] private AudioSource ringingSource;
    [SerializeField] private AudioClip ringingClip;
    [SerializeField] [Range(0f, 1f)] private float ringingVolume = 0.7f;

    [Header("Player Penalty")]
    [SerializeField] private FlashbangTestPlayer targetPlayer;
    [SerializeField] [Range(0f, 1f)] private float blindedMoveMultiplier = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float blindedLookMultiplier = 0.2f;
    [SerializeField] private AnimationCurve recoveryCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Coroutine _activeRoutine;

    private void Awake()
    {
        EnsureOverlayHidden();
        ConfigureRingingSource();
    }

    public void TriggerFlashbang()
    {
        TriggerFlashbang(blindDuration, peakAlpha);
    }

    public void TriggerFlashbang(float duration, float alpha)
    {
        if (_activeRoutine != null)
            StopCoroutine(_activeRoutine);

        StopRinging();

        _activeRoutine = StartCoroutine(FlashbangRoutine(duration, alpha));
    }

    private IEnumerator FlashbangRoutine(float duration, float alpha)
    {
        ApplyPlayerPenalty(0f);

        if (flashOverlay != null)
        {
            Color color = flashOverlay.color;
            color.a = Mathf.Clamp01(alpha);
            flashOverlay.color = color;
            flashOverlay.enabled = true;
        }

        if (ringingSource != null && ringingClip != null)
        {
            ringingSource.Stop();
            ringingSource.clip = ringingClip;
            ringingSource.time = 0f;
            ringingSource.volume = ringingVolume * Mathf.Clamp01(alpha);
            ringingSource.Play();
        }

        float timer = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (timer < safeDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            float currentAlpha = fadeCurve.Evaluate(t) * Mathf.Clamp01(alpha);
            float recovery = recoveryCurve.Evaluate(t);
            float audioFade = Mathf.Clamp01(alpha) <= 0.0001f ? 0f : currentAlpha / Mathf.Clamp01(alpha);

            ApplyPlayerPenalty(recovery);

            if (flashOverlay != null)
            {
                Color color = flashOverlay.color;
                color.a = currentAlpha;
                flashOverlay.color = color;
            }

            if (ringingSource != null && ringingSource.isPlaying)
                ringingSource.volume = ringingVolume * Mathf.Clamp01(alpha) * audioFade;

            yield return null;
        }

        EnsureOverlayHidden();
        StopRinging();
        ApplyPlayerPenalty(1f);
        _activeRoutine = null;
    }

    private void EnsureOverlayHidden()
    {
        if (flashOverlay == null) return;

        Color color = flashOverlay.color;
        color.a = 0f;
        flashOverlay.color = color;
        flashOverlay.enabled = false;
    }

    private void ConfigureRingingSource()
    {
        if (ringingSource == null) return;

        ringingSource.playOnAwake = false;
        ringingSource.loop = false;
        ringingSource.spatialBlend = 0f;
    }

    private void StopRinging()
    {
        if (ringingSource == null)
            return;

        ringingSource.Stop();
        ringingSource.volume = 0f;
    }

    private void ApplyPlayerPenalty(float recovery)
    {
        if (targetPlayer == null) return;

        float moveMultiplier = Mathf.Lerp(blindedMoveMultiplier, 1f, recovery);
        float lookMultiplier = Mathf.Lerp(blindedLookMultiplier, 1f, recovery);

        targetPlayer.SetMoveMultiplier(moveMultiplier);
        targetPlayer.SetLookMultiplier(lookMultiplier);
    }

    public void SetOverlay(Image overlay)
    {
        flashOverlay = overlay;
        EnsureOverlayHidden();
    }

    public void SetTargetPlayer(FlashbangTestPlayer player)
    {
        targetPlayer = player;
    }

    public void SetRingingSource(AudioSource source)
    {
        ringingSource = source;
        ConfigureRingingSource();
    }
}
