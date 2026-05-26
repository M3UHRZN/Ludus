using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Ses Oynatýcýlar (Audio Sources)")]
    [SerializeField] private AudioSource musicSource; // Arka plan müziði ve ambiyans için
    [SerializeField] private AudioSource sfxSource;   // Týklama, para, hata gibi UI sesleri için

    [Header("UI ve Sistem Sesleri (2D)")]
    public AudioClip coinSound;
    public AudioClip buySound;
    public AudioClip wrongSound;
    public AudioClip last10SecSound;
    public AudioClip shipTakeoffSound;

    [Header("Müzik ve Ambiyans")]
    public AudioClip menuMusic;
    public AudioClip lobbyMusic;
    public AudioClip factoryAmbient;

    private void Awake()
    {
        // Singleton Kurulumu ve Sahneler Arasý Yok Olmama (DontDestroyOnLoad)
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// UI ve kýsa ses efektlerini çalar (Üst üste çalabilir)
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Arka plan müziðini veya ambiyansý kesintisiz döngüyle çalar
    /// </summary>
    public void PlayMusic(AudioClip musicClip)
    {
        if (musicSource.clip == musicClip) return; // Zaten bu çalýyorsa baþtan baþlatma

        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.Play();
    }
}