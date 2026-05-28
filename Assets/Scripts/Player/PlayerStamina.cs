using Unity.Netcode;
using UnityEngine;

// Bu kodun çalýþmasý için karakterde kesinlikle PlayerMovement olmasý lazým 
[RequireComponent(typeof(PlayerMovement))]
public class PlayerStamina : NetworkBehaviour
{
    [Header("Stamina Ayarlarý")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float drainRate = 20f; // Koþarken saniyede ne kadar azalacak
    public float regenRate = 15f; // Dinlenirken saniyede ne kadar dolacak

    [Header("Nefessizlik (Exhaustion) Ayarlarý")]
    public float exhaustionDamage = 5f; // Saniyede kaç can gidecek?
    private float _damageTimer = 0f; // 1 saniyeyi sayacak kronometre

    private PlayerMovement _movement;
    private bool _isExhausted = false; // Karakter nefes nefese kaldý mý?

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        currentStamina = maxStamina;
    }

    private void Update()
    {
        // Sadece kendi karakterimizin staminasýný hesaplýyoruz!
        if (!IsOwner) return;

        CalculateStamina();
        UpdateUI();
    }

    private void CalculateStamina()
    {
        Debug.Log($"[Stamina Test] Karakter Hýzý: {_movement.CurrentSpeed} | Stamina: {currentStamina}");

        // 1. FLOAT HASSASÝYETÝ ÇÖZÜMÜ: Hýzýn %80'ine bile ulaþtýysa "Koþuyor" kabul ediyoruz.
        bool isSprinting = _movement.CurrentSpeed >= (_movement.RunSpeed * 0.8f) && _movement.CurrentSpeed > 0.1f;

        if (isSprinting)
        {
            if (currentStamina > 0f)
            {
                // Staminasý var, normal koþuyor (Barý azalt)
                currentStamina -= drainRate * Time.deltaTime;
                _isExhausted = false;
                _damageTimer = 0f; // Dinç olduðu için hasar sayacýný sýfýrla
            }
            else
            {
                // EYVAH! Stamina bitti ama oyuncu hala Shift'e basýp koþmaya zorluyor!
                currentStamina = 0f;
                _isExhausted = true;

                // Zorladýðý için saniye saniye canýndan düþmeye baþla
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
            // Oyuncu Shift'i býraktý, dinleniyor veya yürüyor
            _isExhausted = false;
            _damageTimer = 0f;

            if (currentStamina < maxStamina)
            {
                // Dinlenirken barý doldur
                currentStamina += regenRate * Time.deltaTime;
                if (currentStamina > maxStamina) currentStamina = maxStamina;
            }
        }
    }

    private void ApplyExhaustionDamage()
    {
        // Karakterin ana beynine (PlayerStateMachine) ulaþýyoruz
        if (TryGetComponent(out PlayerStateMachine stateMachine))
        {
            // TakeDamage fonksiyonu 3 veri istiyor: (Hasar Miktarý, Vuruþ Noktasý, Saldýranýn ID'si)
            // Kendi kendimize hasar verdiðimiz için kendi pozisyonumuzu ve kendi ID'mizi gönderiyoruz
            stateMachine.TakeDamage(exhaustionDamage, transform.position, OwnerClientId);

            Debug.Log("Karakter nefessizlikten " + exhaustionDamage + " can kaybetti! Kalan Can: " + stateMachine.CurrentHealth);
        }
    }

    private void UpdateUI()
    {
        // Her karede yeni durumu fýrlatýyoruz, StaminaUIController bunu yakalayýp barý hareket ettirecek.
        GameEventBus.Publish(new StaminaUpdatedEvent
        {
            CurrentStamina = currentStamina,
            MaxStamina = maxStamina,
            IsExhausted = _isExhausted
        });
    }
}