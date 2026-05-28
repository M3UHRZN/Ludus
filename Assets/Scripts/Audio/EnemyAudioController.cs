using UnityEngine;

public class EnemyAudioController : MonoBehaviour
{
    [Header("Hoparlor ve Kaset")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip footstepClip;

    // Animasyon Event'i bu fonksiyonu cagiracak
    public void PlayFootstep()
    {
        if (footstepSource != null && footstepClip != null && !footstepSource.isPlaying)
        {
            // Canavarin adimlari daha organik ve korkutucu duyulsun diye pitch ile oynuyoruz
            footstepSource.pitch = Random.Range(0.7f, 1.1f);
            footstepSource.PlayOneShot(footstepClip);
        }
    }
}
