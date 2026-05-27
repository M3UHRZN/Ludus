using UnityEngine;

public class EnemyAudioController : MonoBehaviour
{
    [Header("Hoparlör ve Kaset")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip footstepClip;

    // Animasyon Event'i bu fonksiyonu çaðýracak
    public void PlayFootstep()
    {
        if (footstepSource != null && footstepClip != null && !footstepSource.isPlaying)
        {
            // Canavarýn adýmlarý daha organik ve korkutucu duyulsun diye pitch ile oynuyoruz
            footstepSource.pitch = Random.Range(0.7f, 1.1f);
            footstepSource.PlayOneShot(footstepClip);
        }
    }
}