using UnityEngine;

public class PlayerAudioController : MonoBehaviour
{
    [Header("SES KASETLERÝ")]
    public AudioClip footstepClip;
    public AudioClip jumpLandClip; // YENÝ: Yere inme (Düþme) sesi!

    [Header("Ayarlar")]
    public float stepDistance = 1.5f;

    private AudioSource zrhliHoparlor;
    private Vector3 lastStepPosition;

    // Zýplama tespiti için karakterin kendi motoru
    private CharacterController characterController;
    private bool wasGrounded;

    private void Start()
    {
        lastStepPosition = transform.position;

        // Karakterin fizik motorunu (PlayerV0.5'in üstündeki) buluyoruz
        characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            wasGrounded = characterController.isGrounded;
        }

        // --- ZIRHLI HOPARLÖR ---
        zrhliHoparlor = gameObject.AddComponent<AudioSource>();
        zrhliHoparlor.spatialBlend = 0f; // %100 2D
        zrhliHoparlor.volume = 1f;       // Full Ses
        zrhliHoparlor.mute = false;
        zrhliHoparlor.playOnAwake = false;
    }

    private void Update()
    {
        // --- 1. ZIPLAMA VE YERE ÝNME KONTROLÜ ---
        if (characterController != null)
        {
            bool isGroundedNow = characterController.isGrounded;

            // Eðer geçen frame havadaysa (wasGrounded == false) ve þu an yerdeyse -> YERE ÝNDÝ!
            if (!wasGrounded && isGroundedNow)
            {
                if (jumpLandClip != null)
                {
                    zrhliHoparlor.pitch = Random.Range(0.9f, 1.1f); // Her zýplayýþta ses azýcýk deðiþsin
                    zrhliHoparlor.PlayOneShot(jumpLandClip);
                }

                // Yere iner inmez ekstra bir ayak sesi çalmasýn diye sayacý sýfýrlýyoruz
                lastStepPosition = transform.position;
            }

            wasGrounded = isGroundedNow; // Durumu sonraki frame için kaydet
        }

        // --- 2. YÜRÜYÜÞ KONTROLÜ ---
        if (footstepClip == null) return;

        // Karakter yerdeyse adým saysýn (Havadayken adým sesi çýkmasýn)
        if (characterController == null || characterController.isGrounded)
        {
            // Sadece X ve Z eksenindeki hareketi ölç (Zýplamayý adým saymasýn)
            Vector3 currentPos = new Vector3(transform.position.x, 0, transform.position.z);
            Vector3 lastPos = new Vector3(lastStepPosition.x, 0, lastStepPosition.z);
            float distanceWalked = Vector3.Distance(currentPos, lastPos);

            if (distanceWalked >= stepDistance)
            {
                zrhliHoparlor.pitch = Random.Range(0.85f, 1.15f);
                zrhliHoparlor.PlayOneShot(footstepClip);

                lastStepPosition = transform.position;
            }
        }
    }
}