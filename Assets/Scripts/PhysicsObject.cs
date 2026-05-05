using UnityEngine;

/// <summary>
/// Tutulabilir her eşyaya ekle.
/// Rigidbody ile çalışır; GrabSystem tarafından yönetilir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PhysicsObject : MonoBehaviour
{
    [Header("Grab Ayarları")]
    [Tooltip("Eşyanın tutulabileceği maksimum mesafe (metre)")]
    public float grabDistance = 4f;

    [Tooltip("Eşya tutulurken ne kadar hızlı hedefe yaklaşır (yay katsayısı)")]
    public float holdSpringStrength = 150f;

    [Tooltip("Yay titreşimini önlemek için sönümleme")]
    public float holdDamping = 12f;

    [Tooltip("Fırlatma kuvveti çarpanı")]
    public float throwForceMultiplier = 12f;

    [Header("Görsel Geribildirim")]
    [Tooltip("Highlight materyali (opsiyonel)")]
    public Material highlightMaterial;

    // ── İç Durum ──────────────────────────────────────────────────────────────
    [HideInInspector] public bool isHeld = false;
    [HideInInspector] public bool isHighlighted = false;

    private Rigidbody rb;
    private Material originalMaterial;
    private Renderer rend;

    // Eşyanın tutulduğunda uçmasını önlemek için orijinal drag değerleri
    private float originalDrag;
    private float originalAngularDrag;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rend = GetComponent<Renderer>();

        originalDrag = rb.linearDamping;
        originalAngularDrag = rb.angularDamping;

        if (rend != null)
            originalMaterial = rend.material;
    }

    // ── Highlight (Vurgulama) ──────────────────────────────────────────────────
    public void SetHighlight(bool active)
    {
        if (rend == null || highlightMaterial == null) return;
        if (isHighlighted == active) return;

        isHighlighted = active;
        rend.material = active ? highlightMaterial : originalMaterial;
    }

    // ── Tutma / Bırakma ───────────────────────────────────────────────────────
    public void OnGrab()
    {
        isHeld = true;

        // Tutulurken daha fazla hava direnci → daha kontrollü hareket
        rb.linearDamping = 8f;
        rb.angularDamping = 8f;

        // Gravity kapatmak isteğe bağlı; yorum satırını kaldırarak aktif edebilirsin
        // rb.useGravity = false;
    }

    public void OnRelease()
    {
        isHeld = false;
        rb.linearDamping = originalDrag;
        rb.angularDamping = originalAngularDrag;
        // rb.useGravity = true;

        SetHighlight(false);
    }

    // ── Fizik Güncellemesi (GrabSystem çağırır) ───────────────────────────────
    /// <summary>
    /// Eşyayı hedef pozisyona Spring (yay) kuvvetiyle çeker.
    /// FixedUpdate'ten çağrılmalıdır.
    /// </summary>
    public void MoveTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - rb.position;
        float distance = direction.magnitude;

        // Yay kuvveti: F = k * x   (k = holdSpringStrength, x = mesafe)
        Vector3 springForce = direction * holdSpringStrength;

        // Sönümleme: mevcut hızın tersine orantılı kuvvet
        Vector3 dampingForce = -rb.linearVelocity * holdDamping;

        rb.AddForce(springForce + dampingForce, ForceMode.Force);

        // Eşya çok uzaklaşırsa kendiliğinden bırak (güvenlik kontrolü)
        if (distance > grabDistance * 2.5f)
        {
            GrabSystem grabSystem = FindObjectOfType<GrabSystem>();
            if (grabSystem != null)
                grabSystem.DropObject();
        }
    }

    // ── Fırlatma ──────────────────────────────────────────────────────────────
    public void Throw(Vector3 throwDirection, float chargeRatio)
    {
        OnRelease();

        // Charge oranı 0-1 arası; tam şarj daha güçlü fırlatır
        float force = throwForceMultiplier * (1f + chargeRatio * 2f);
        rb.AddForce(throwDirection.normalized * force, ForceMode.Impulse);

        // Hafif rastgele rotasyon → daha gerçekçi fırlatma
        Vector3 randomTorque = Random.insideUnitSphere * force * 0.3f;
        rb.AddTorque(randomTorque, ForceMode.Impulse);
    }
}
