using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Bu sýnýf, oyun içi HUD (Heads-Up Display) elemanlarýný yönetir. Süre göstergesi ve takým arkadaþý durum ikonlarýný içerir.
public class HUDController : MonoBehaviour
{
    [Header("Süre Elementleri")]
    public TextMeshProUGUI timerText;
    public Image timerFillImage;
    public float maxTime = 60f;

    [Header("Takým Arkadaþý Ýkonlarý (Sol Menü)")]
    public Image[] teammateIcons;
    public Sprite aliveIcon;
    public Sprite deadIcon;

    private void OnEnable()
    {
        // Sadece Süre ve Ölüm/Canlanma eventlerini dinliyoruz
        GameEventBus.Subscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Subscribe<PlayerDiedEvent>(OnTeammateDied);
        GameEventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnTeammateDied);
        GameEventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);
    }

    private void OnTimerUpdated(TimerEventTriggered evt)
    {
        timerText.text = evt.RemainingSeconds.ToString("F1");
        if (timerFillImage != null) timerFillImage.fillAmount = evt.RemainingSeconds / maxTime;

        if (evt.IsUrgent)
        {
            timerText.color = Color.red;
            if (timerFillImage != null) timerFillImage.color = Color.red;
        }
        else
        {
            timerText.color = Color.white;
            if (timerFillImage != null) timerFillImage.color = new Color(0f, 0.8f, 1f); // Mavi
        }
    }

    private void OnTeammateDied(PlayerDiedEvent evt)
    {
        if (evt.PlayerId >= 0 && evt.PlayerId < teammateIcons.Length)
        {
            teammateIcons[evt.PlayerId].sprite = deadIcon;
            teammateIcons[evt.PlayerId].color = Color.red;
        }
    }

    private void OnPlayerRevived(PlayerRevivedEvent evt)
    {
        if (evt.PlayerId >= 0 && evt.PlayerId < teammateIcons.Length)
        {
            teammateIcons[evt.PlayerId].sprite = aliveIcon;
            teammateIcons[evt.PlayerId].color = Color.white;
        }
    }

    public void SetPlayerCount(int playerCount)
    {
        for (int i = 0; i < teammateIcons.Length; i++)
        {
            teammateIcons[i].gameObject.SetActive(i < playerCount);
            teammateIcons[i].sprite = aliveIcon;
            teammateIcons[i].color = Color.white;
        }
    }
}