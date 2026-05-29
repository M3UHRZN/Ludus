using Unity.Netcode;
using UnityEngine;

// Bu kodun calismasi icin karakterde kesinlikle PlayerMovement olmasi lazim
[RequireComponent(typeof(PlayerMovement))]
public class PlayerStamina : NetworkBehaviour
{
    [Header("Stamina Ayarlari")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float drainRate = 20f;  // Kosarken saniyede ne kadar azalacak
    public float regenRate = 15f;  // Dinlenirken saniyede ne kadar dolacak

    [Header("Nefessizlik (Exhaustion) Ayarlari")]
    public float exhaustionDamage = 5f; // Saniyede kac can gidecek?
    private float _damageTimer = 0f;     // 1 saniyeyi sayacak kronometre

    [Header("Debug")]
    [Tooltip("True iken her frame stamina log'u Console'a yazilir. Production'da kapali tutun, " +
             "yoksa Console'u 999+ log ile bogup oyunu yavaslatir.")]
    [SerializeField] private bool _verbose = false;

    private PlayerMovement _movement;
    private bool _isExhausted = false; // Karakter nefes nefese mi kaldi?

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        currentStamina = maxStamina;
    }

    private void Update()
    {
        // Sadece kendi karakterimizin staminasini hesapliyoruz
        if (!IsOwner) return;

        CalculateStamina();
        UpdateUI();
    }

    private void CalculateStamina()
    {
        if (_verbose)
            Debug.Log($"[Stamina] Karakter Hizi: {_movement.CurrentSpeed} | Stamina: {currentStamina}");

        // FLOAT HASSASIYET COZUMU: Hizin %80'ine bile ulastiysa "Kosuyor" kabul ediyoruz.
        bool isSprinting = _movement.CurrentSpeed >= (_movement.RunSpeed * 0.8f) && _movement.CurrentSpeed > 0.1f;

        if (isSprinting)
        {
            if (currentStamina > 0f)
            {
                // Staminasi var, normal kosuyor (Bari azalt)
                currentStamina -= drainRate * Time.deltaTime;
                _isExhausted = false;
                _damageTimer = 0f;
            }
            else
            {
                // EYVAH! Stamina bitti ama oyuncu hala Shift'e basip kosmaya zorluyor!
                currentStamina = 0f;
                _isExhausted = true;

                // Zorladigi icin saniye saniye canindan dusmeye basla
                _damageTimer += Time.deltaTime;
                if (_damageTimer >= 1f)
                {
                    ApplyExhaustionDamage();
                    _damageTimer = 0f;
                }
            }
        }
        else
        {
            // Oyuncu Shift'i birakti, dinleniyor veya yuruyor
            _isExhausted = false;
            _damageTimer = 0f;

            if (currentStamina < maxStamina)
            {
                // Dinlenirken bari doldur
                currentStamina += regenRate * Time.deltaTime;
                if (currentStamina > maxStamina) currentStamina = maxStamina;
            }
        }
    }

    private void ApplyExhaustionDamage()
    {
        // Karakterin ana beynine (PlayerStateMachine) ulasiyoruz
        if (TryGetComponent(out PlayerStateMachine stateMachine))
        {
            // TakeDamage 3 veri istiyor: (Hasar Miktari, Vurus Noktasi, Saldiranin ID'si)
            // Kendi kendimize hasar verdigimiz icin kendi pozisyonumuzu ve kendi ID'mizi gonderiyoruz
            stateMachine.TakeDamage(exhaustionDamage, transform.position, OwnerClientId);

            if (_verbose)
                Debug.Log($"[Stamina] Karakter nefessizlikten {exhaustionDamage} can kaybetti. Kalan: {stateMachine.CurrentHealth}");
        }
    }

    private void UpdateUI()
    {
        // Her karede yeni durumu firlatiyoruz, StaminaUIController bunu yakalayip bari hareket ettirecek.
        GameEventBus.Publish(new StaminaUpdatedEvent
        {
            CurrentStamina = currentStamina,
            MaxStamina = maxStamina,
            IsExhausted = _isExhausted
        });
    }
}
