using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    private AudioSource footstepSource;
    private AudioSource combatSource;

    [Header("Ses Kasetleri (Clips)")]
    public AudioClip footstepClip;
    public AudioClip fallClip;
    public AudioClip deathClip;
    public AudioClip bloodyClip; // Stamina bitip can gidince çalacak
    public AudioClip punchClip;  // Düþman 1 (Yakýn dövüþ) vurduðunda
    public AudioClip shotClip;   // Düþman 2 (Menzilli) vurduðunda

    private void Start()
    {
        // 1. Karakterin Root objesine çýk (PlayerV0.3)
        Transform rootObj = transform.root;

        // 2. O objenin altýndaki "AudioHolder" isimli klasörü bul
        Transform audioHolder = rootObj.Find("AudioHolder");

        if (audioHolder != null)
        {
            // 3. AudioHolder'ýn içindeki hoparlörleri otomatik al ve yerleþtir
            AudioSource[] sources = audioHolder.GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                footstepSource = sources[0]; // Ýlk hoparlör ayak sesi
                combatSource = sources[1];   // Ýkinci hoparlör combat/düþme
            }
        }
        else
        {
            Debug.LogWarning("AudioHolder objesini bulamadým!");
        }
    }

    public void PlayFootstep()
    {
        // Sesi hafif kalýnlaþtýrýp incelterek (pitch) robotikliðini alýyoruz
        if (footstepSource != null && footstepClip != null && !footstepSource.isPlaying)
        {
            footstepSource.pitch = Random.Range(0.85f, 1.15f);
            footstepSource.PlayOneShot(footstepClip);
        }
    }

    public void PlayFallSound()
    {
        if (combatSource != null && fallClip != null)
            combatSource.PlayOneShot(fallClip);
    }

    public void PlayDeathSound()
    {
        if (combatSource != null && deathClip != null)
            combatSource.PlayOneShot(deathClip);
    }

    public void PlayBloodySound()
    {
        if (combatSource != null && bloodyClip != null)
            combatSource.PlayOneShot(bloodyClip);
    }

    public void PlayPunchSound()
    {
        if (combatSource != null && punchClip != null)
            combatSource.PlayOneShot(punchClip);
    }

    public void PlayShotSound()
    {
        if (combatSource != null && shotClip != null)
            combatSource.PlayOneShot(shotClip);
    }
}
