using UnityEngine;

/// <summary>
/// R.E.P.O. tarzı eşya tutma sistemi.
/// Player kamerasına (veya Player objesine) ekle.
/// 
/// KONTROLLER
/// ───────────────────────────────
/// E               → Eşyayı tut / bırak
/// Sol Tık (LMB)   → Fırlatmaya başla (basılı tut = şarj et)
/// LMB bırak       → Fırlat
/// Sağ Tık (RMB)   → Anında bırak / itme
/// Scroll Wheel    → Tutma mesafesini ayarla
/// </summary>
public class GrabSystem : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Oyuncu kamerası. Boş bırakılırsa Camera.main kullanılır.")]
    public Camera playerCamera;

    [Header("Tutma Ayarları")]
    [Tooltip("Eşyanın tutulduğunda durduğu mesafe (kameradan)")]
    [Range(1f, 6f)] public float holdDistance = 2.5f;

    [Tooltip("Scroll ile en az / en fazla tutma mesafesi")]
    public float minHoldDistance = 1f;
    public float maxHoldDistance = 5f;

    [Tooltip("Eşya tutulurken ne kadar hızlı döner (opsiyonel rotasyon)")]
    public float rotationSpeed = 100f;

    [Header("Fırlatma Ayarları")]
    [Tooltip("Tam şarj için gereken süre (saniye)")]
    public float maxChargeTime = 1.5f;

    [Header("Raycast")]
    [Tooltip("Hangi katmanlar tutulabilir")]
    public LayerMask grabbableLayers = ~0; // Varsayılan: her şey

    [Tooltip("Eşya arama menzili")]
    public float grabRange = 4f;

    [Header("UI Geri Bildirimi")]
    [Tooltip("Eşyaya bakarken gösterilecek crosshair / UI (opsiyonel)")]
    public GameObject interactPromptUI;

    // ── İç Durum ──────────────────────────────────────────────────────────────
    private PhysicsObject heldObject = null;
    private PhysicsObject lookedObject = null;

    private bool isChargingThrow = false;
    private float chargeStartTime = 0f;

    private Unity.Netcode.NetworkObject _netObj;
    private PlayerStateMachine _playerStateMachine;

    void Awake()
    {
        if (playerCamera == null)
            playerCamera = Camera.main;

        _netObj = GetComponentInParent<Unity.Netcode.NetworkObject>();
        _playerStateMachine = GetComponentInParent<PlayerStateMachine>();
    }

    void Update()
    {
        if (_netObj != null && !_netObj.IsOwner) return;

        HandleLook();
        HandleInput();
        UpdateHoldDistance();
    }

    void FixedUpdate()
    {
        if (heldObject != null)
            UpdateHeldObject();
    }

    // ── Raycast ile Bakılan Eşyayı Tespit Et ─────────────────────────────────
    void HandleLook()
    {
        // Önceki highlight'ı temizle (tutulmuş değilse)
        if (lookedObject != null && lookedObject != heldObject)
        {
            lookedObject.SetHighlight(false);
            lookedObject = null;
        }

        if (heldObject != null)
        {
            // Zaten bir eşya tutuyorsak bakış kontrolü gerekmez
            ShowPrompt(false);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out RaycastHit hit, grabRange, grabbableLayers))
        {
            PhysicsObject po = hit.collider.GetComponent<PhysicsObject>();
            if (po != null && !po.IsHeld)
            {
                lookedObject = po;
                po.SetHighlight(true);
                ShowPrompt(true);
                return;
            }
        }

        ShowPrompt(false);
    }

    // ── Input İşleme ──────────────────────────────────────────────────────────
    void HandleInput()
    {
        // E tuşu → Tut veya Bırak
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (heldObject == null)
                TryGrab();
            else
                DropObject();
        }

        // Sol tık basılı → Fırlatma şarjı başlat
        if (Input.GetMouseButtonDown(0) && heldObject != null)
        {
            isChargingThrow = true;
            chargeStartTime = Time.time;
        }

        // Sol tık bırakıldı → Fırlat
        if (Input.GetMouseButtonUp(0) && isChargingThrow && heldObject != null)
        {
            ThrowObject();
        }

        // Sağ tık → Anında bırak + hafif itme (R.E.P.O.'daki "push")
        if (Input.GetMouseButtonDown(1) && heldObject != null)
        {
            PushAndDrop();
        }
    }

    // ── Scroll ile Tutma Mesafesini Ayarla ────────────────────────────────────
    void UpdateHoldDistance()
    {
        if (heldObject == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        holdDistance = Mathf.Clamp(holdDistance + scroll * 0.5f, minHoldDistance, maxHoldDistance);
    }

    // ── Eşyayı Tut ────────────────────────────────────────────────────────────
    void TryGrab()
    {
        if (lookedObject == null) return;

        heldObject = lookedObject;
        lookedObject = null;

        heldObject.OnGrab(_playerStateMachine);
        Debug.Log($"[GrabSystem] Tutuldu: {heldObject.gameObject.name}");
    }

    // ── Eşyayı Bırak ──────────────────────────────────────────────────────────
    public void DropObject()
    {
        if (heldObject == null) return;

        Debug.Log($"[GrabSystem] Bırakıldı: {heldObject.gameObject.name}");

        heldObject.OnRelease();
        heldObject = null;
        isChargingThrow = false;
    }

    // ── Eşyayı Fırlat ─────────────────────────────────────────────────────────
    void ThrowObject()
    {
        if (heldObject == null) return;

        // Şarj oranı: 0 = anında bırak, 1 = tam güç
        float chargeTime = Time.time - chargeStartTime;
        float chargeRatio = Mathf.Clamp01(chargeTime / maxChargeTime);

        Vector3 throwDir = playerCamera.transform.forward;

        Debug.Log($"[GrabSystem] Fırlatıldı: {heldObject.gameObject.name} | Şarj: {chargeRatio:P0}");

        heldObject.Throw(throwDir, chargeRatio);
        heldObject = null;
        isChargingThrow = false;
    }

    // ── Eşyayı İterek Bırak ───────────────────────────────────────────────────
    void PushAndDrop()
    {
        if (heldObject == null) return;

        // Küçük bir itme kuvveti uygula, sonra bırak
        Vector3 pushDir = playerCamera.transform.forward;
        heldObject.Throw(pushDir, 0.1f);   // Düşük şarjla hafif itme
        heldObject = null;
        isChargingThrow = false;

        Debug.Log("[GrabSystem] İtildi ve bırakıldı.");
    }

    // ── Tutulan Eşyayı Hedefe Doğru Hareket Ettir ────────────────────────────
    void UpdateHeldObject()
    {
        // Hedef nokta: kameranın önünde holdDistance kadar
        Vector3 targetPos = playerCamera.transform.position
                          + playerCamera.transform.forward * holdDistance;

        heldObject.MoveTowards(targetPos);
    }

    // ── Prompt UI ─────────────────────────────────────────────────────────────
    void ShowPrompt(bool active)
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(active);
    }

    // ── Gizmos (Editor görselleştirme) ────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;

        // Raycast menzilini göster
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * grabRange);

        // Tutma noktasını göster
        if (Application.isPlaying && heldObject != null)
        {
            Vector3 holdPos = playerCamera.transform.position
                            + playerCamera.transform.forward * holdDistance;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(holdPos, 0.1f);
        }
    }
}
