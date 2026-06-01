using UnityEngine;
using UnityEngine.SceneManagement;

// Map sahnesinde fabrika ambient loop'unu yonetir.
// Sahneye eklenecek: bir GameObject'e attach et, clip serialize field'a ata.
// Lobi veya MainMenu sahnesine gecince otomatik durur.
[RequireComponent(typeof(AudioSource))]
public class FactoryAmbientLoop : MonoBehaviour
{
    [Header("Clip")]
    [SerializeField] private AudioClip _ambientClip;

    [Header("Ayarlar")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 0.45f;
    [SerializeField] private bool _playOnStart = true;
    [SerializeField] private float _fadeInSeconds = 1.5f;
    [SerializeField] private float _fadeOutSeconds = 1.5f;

    private AudioSource _src;
    private float _fadeTarget;
    private bool _fading;
    private float _fadeRate;

    private void Awake()
    {
        _src = GetComponent<AudioSource>();
        _src.loop = true;
        _src.spatialBlend = 0f; // 2D, her yerde ayni ses
        _src.playOnAwake = false;
        _src.priority = 64;
        if (_ambientClip != null) _src.clip = _ambientClip;
        _src.volume = 0f;
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnSceneChanged;
    }

    private void Start()
    {
        if (_playOnStart && _ambientClip != null && ShouldPlayInCurrentScene())
            StartFadeIn();
    }

    private void Update()
    {
        if (!_fading) return;

        _src.volume = Mathf.MoveTowards(_src.volume, _fadeTarget, _fadeRate * Time.deltaTime);
        if (Mathf.Approximately(_src.volume, _fadeTarget))
        {
            _fading = false;
            if (_fadeTarget <= 0f && _src.isPlaying)
                _src.Stop();
        }
    }

    private void OnSceneChanged(Scene oldScene, Scene newScene)
    {
        if (ShouldPlayInCurrentScene())
        {
            if (_ambientClip != null) StartFadeIn();
        }
        else
        {
            StartFadeOut();
        }
    }

    private bool ShouldPlayInCurrentScene()
    {
        string s = SceneManager.GetActiveScene().name;
        if (s == SceneNames.Lobby) return false;
        if (s == SceneNames.MainMenu) return false;
        return true;
    }

    private void StartFadeIn()
    {
        if (_ambientClip == null) return;
        if (_src.clip != _ambientClip) _src.clip = _ambientClip;
        if (!_src.isPlaying) _src.Play();
        _fadeTarget = _volume;
        _fadeRate = _fadeInSeconds > 0f ? _volume / _fadeInSeconds : 999f;
        _fading = true;
    }

    private void StartFadeOut()
    {
        _fadeTarget = 0f;
        _fadeRate = _fadeOutSeconds > 0f ? _volume / _fadeOutSeconds : 999f;
        _fading = true;
    }
}
