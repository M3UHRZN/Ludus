using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode; // AÐ KÜTÜPHANESÝ EKLENDÝ

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

    private void Start()
    {
        // Að (Network) yöneticisi varsa, mevcut oyuncu sayýsýný çek
        if (NetworkManager.Singleton != null)
        {
            UpdatePlayerCount();

            // Sunucuya yeni biri baðlanýrsa veya biri düþerse listeyi otomatik güncelle
            NetworkManager.Singleton.OnClientConnectedCallback += (id) => UpdatePlayerCount();
            NetworkManager.Singleton.OnClientDisconnectCallback += (id) => UpdatePlayerCount();
        }
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Subscribe<PlayerDiedEvent>(OnTeammateDied);
        GameEventBus.Subscribe<PlayerRevivedEvent>(OnPlayerRevived);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnTeammateDied);
        GameEventBus.Unsubscribe<PlayerRevivedEvent>(OnPlayerRevived);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= (id) => UpdatePlayerCount();
            NetworkManager.Singleton.OnClientDisconnectCallback -= (id) => UpdatePlayerCount();
        }
    }

    // AÐDAN OYUNCU SAYISINI ÇEKEN FONKSÝYON
    private void UpdatePlayerCount()
    {
        if (NetworkManager.Singleton != null)
        {
            // Sunucuya baðlý olan (sen dahil) toplam oyuncu sayýsý
            int count = NetworkManager.Singleton.ConnectedClientsIds.Count;
            SetPlayerCount(count);
        }
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
            if (timerFillImage != null) timerFillImage.color = new Color(0f, 0.8f, 1f);
        }
    }

    private void OnTeammateDied(PlayerDiedEvent evt)
    {
        // Event'ten gelen PlayerId'yi ikon sýrasý olarak kullanýyoruz
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