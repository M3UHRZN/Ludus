using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    private AudioSource footstepSource;
    private AudioSource combatSource;

    [Header("Ses Kasetleri (Clips)")]
    public AudioClip footstepClip;
    public AudioClip fallClip;
    public AudioClip deathClip;
    public AudioClip bloodyClip; // Stamina bitip can gidince calacak
    public AudioClip punchClip;  // Dusman 1 (Yakin dovus) vurdugunda
    public AudioClip shotClip;   // Dusman 2 (Menzilli) vurdugunda

    private void Start()
    {
        // 1. Karakterin Root objesine cik (PlayerV0.3)
        Transform rootObj = transform.root;

        // 2. O objenin altindaki "AudioHolder" isimli klasoru bul
        Transform audioHolder = rootObj.Find("AudioHolder");

        if (audioHolder != null)
        {
            // 3. AudioHolder'in icindeki hoparlorleri otomatik al ve yerlestir
            AudioSource[] sources = audioHolder.GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                footstepSource = sources[0]; // Ilk hoparlor ayak sesi
                combatSource = sources[1];   // Ikinci hoparlor combat/dusme
            }
        }
        else
        {
            Debug.LogWarning("AudioHolder objesini bulamadim!");
        }
    }

    public void PlayFootstep()
    {
        // Sesi hafif kalinlastirip incelterek (pitch) robotikligini aliyoruz
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
