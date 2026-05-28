using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Ses Oynaticilar (Audio Sources)")]
    [SerializeField] private AudioSource musicSource; // Arka plan muzigi ve ambiyans icin
    [SerializeField] private AudioSource sfxSource;   // Tiklama, para, hata gibi UI sesleri icin

    [Header("UI ve Sistem Sesleri (2D)")]
    public AudioClip coinSound;
    public AudioClip buySound;
    public AudioClip wrongSound;
    public AudioClip last10SecSound;
    public AudioClip shipTakeoffSound;

    [Header("Muzik ve Ambiyans")]
    public AudioClip menuMusic;
    public AudioClip lobbyMusic;
    public AudioClip factoryAmbient;

    private void Awake()
    {
        // Singleton Kurulumu ve Sahneler Arasi Yok Olmama (DontDestroyOnLoad)
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
    /// UI ve kisa ses efektlerini calar (ust uste calabilir)
    /// </summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// Arka plan muzigini veya ambiyansi kesintisiz dongoyle calar
    /// </summary>
    public void PlayMusic(AudioClip musicClip)
    {
        if (musicSource.clip == musicClip) return; // Zaten bu caliyorsa bastan baslatma

        musicSource.clip = musicClip;
        musicSource.loop = true;
        musicSource.Play();
    }
}
