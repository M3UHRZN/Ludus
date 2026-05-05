using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI Elementleri")]
    public TextMeshProUGUI timerText;
    public Slider weightBar;
    public Image[] teammateIcons; // Takým arkadaþý ikonlarý

    private void OnEnable()
    {
        // Script aktif olduðunda EventBus'a abone oluyoruz
        GameEventBus.Subscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
        GameEventBus.Subscribe<PlayerDiedEvent>(OnTeammateDied);
    }

    private void OnDisable()
    {
        // Script kapandýðýnda hafýza sýzýntýsý olmamasý için abonelikten çýkýyoruz
        GameEventBus.Unsubscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnTeammateDied);
    }

    // --- EVENT DÝNLEYÝCÝ FONKSÝYONLAR ---

    private void OnTimerUpdated(TimerEventTriggered evt)
    {
        // Süreyi ekrana yazdýr (Örn: 45.2 saniye)
        timerText.text = evt.RemainingSeconds.ToString("F1");

        // GDD'ye göre 10 saniyenin altýndaysa acil durum (rengi kýrmýzý yap)
        if (evt.IsUrgent)
        {
            timerText.color = Color.red;
        }
        else
        {
            timerText.color = Color.white;
        }
    }

    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        // Aðýrlýk barýný güncelle
        // Not: Maksimum aðýrlýðý sistemin nasýl tasarlandýðýna göre oranlaman gerekecek
        weightBar.value = evt.Weight;
    }

    private void OnTeammateDied(PlayerDiedEvent evt)
    {
        // Ölen oyuncunun ikonunu kýrmýzý veya gri yap
        if (evt.PlayerId >= 0 && evt.PlayerId < teammateIcons.Length)
        {
            teammateIcons[evt.PlayerId].color = Color.red;
        }
    }
}