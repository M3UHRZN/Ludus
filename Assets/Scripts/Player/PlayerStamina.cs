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
        // Oyuncunun þu an koþup koþmadýðýný "CurrentSpeed" üzerinden anlýyoruz. Hýz 0.1'den büyükse (durmuyorsa) ve RunSpeed'e eþitse koþuyordur.
        bool isSprinting = _movement.CurrentSpeed >= _movement.RunSpeed && _movement.CurrentSpeed > 0.1f;

        if (isSprinting && !_isExhausted)
        {
            // Koþuyorsa staminayý azalt
            currentStamina -= drainRate * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                _isExhausted = true;
            }
        }
        else
        {
            // Koþmuyorsa staminayý doldur
            currentStamina += regenRate * Time.deltaTime;

            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                _isExhausted = false; // Nefesi yerine geldi
            }
        }

        // --- NEFESSÝZLÝK HASARI KONTROLÜ ---
        if (_isExhausted)
        {
            // Kronometreyi çalýþtýr
            _damageTimer += Time.deltaTime;

            // Eðer 1 saniye dolduysa hasar ver ve kronometreyi sýfýrla
            if (_damageTimer >= 1f)
            {
                ApplyExhaustionDamage();
                _damageTimer = 0f;
            }
        }
        else
        {
            // Karakter dinleniyorsa kronometreyi sýfýrla
            _damageTimer = 0f;
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