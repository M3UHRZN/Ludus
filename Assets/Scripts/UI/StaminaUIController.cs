using UnityEngine;
using UnityEngine.UI;

public class StaminaUIController : MonoBehaviour
{
    [Header("Stamina Bar Ayarlarý")]
    public Image staminaFillImage; // Dolan/Boþalan sarý bar

    [Header("Renkler")]
    public Color normalColor = new Color(1f, 0.7f, 0f); // Turuncu/Sarýmsý
    public Color exhaustedColor = Color.red; // Stamina bitip cana vurduðunda kýrmýzý olacak

    private void OnEnable()
    {
        // EventBus'a abone oluyoruz
        GameEventBus.Subscribe<StaminaUpdatedEvent>(OnStaminaUpdated);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<StaminaUpdatedEvent>(OnStaminaUpdated);
    }

    private void OnStaminaUpdated(StaminaUpdatedEvent evt)
    {
        if (staminaFillImage == null) return;

        // 1. Barýn doluluðunu ayarla (0 ile 1 arasý bir deðer olmalý)
        // Matematik: Mevcut Stamina / Maksimum Stamina (Örn: 50/100 = 0.5f)
        float fillRatio = evt.CurrentStamina / evt.MaxStamina;
        staminaFillImage.fillAmount = fillRatio;

        // 2. Renk Deðiþimi (Stamina bittiyse ve cana vuruyorsa oyuncuyu kýrmýzýyla paniklet!)
        if (evt.IsExhausted || fillRatio <= 0.05f)
        {
            staminaFillImage.color = exhaustedColor;
        }
        else
        {
            staminaFillImage.color = normalColor;
        }
    }
}