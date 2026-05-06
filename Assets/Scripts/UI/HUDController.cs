using UnityEngine;
using UnityEngine.UI;   // Image bileþeni için gerekli
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI Elementleri")]
    public TextMeshProUGUI timerText;
    public Image timerFillImage; // Radial Bar

    [Header("Süre Ayarlarý")]
    public float maxTime = 60f; // Oyunun baþlangýç süresi

    [Header("Aðýrlýk Barý Ayarlarý")]
    public Image weightBarImage;
    public float fillSpeed = 5f; // Barýn ne kadar hýzlý akacaðýný belirler

    private int currentTotalWeight = 0; // Oyuncunun o anki toplam aðýrlýðý
    private int maxWeight = 10; // Bar 10 kutu olduðu için sýnýr 10
    private float targetWeightFill = 0f; // Barýn ulaþmak istediði hedef nokta

    [Header("Teammate Ýkonlarý")]
    public Image[] teammateIcons;
    public Sprite aliveIcon; 
    public Sprite deadIcon;  

    private void OnEnable()
    {
        // Script aktif olduðunda EventBus'a abone oluyoruz
        GameEventBus.Subscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);      // Yeni eþya alýndýðýnda aðýrlýk barýný güncellemek için
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
        // 1. Sayýyý ekrana yazdýr
        timerText.text = evt.RemainingSeconds.ToString("F1");

        // 2. Barýn doluluk oranýný ayarla (0 ile 1 arasýnda bir deðer olmalý)
        timerFillImage.fillAmount = evt.RemainingSeconds / maxTime;

        // 3. Son 10 saniye kontrolü 
        if (evt.IsUrgent)
        {
            // Acil durum: Yazý ve bar kýrmýzý olsun!
            timerText.color = Color.red;
            timerFillImage.color = Color.red;
        }
        else
        {
            // Normal durum: Yazý beyaz, bar ise havalý bir bilim-kurgu mavisi
            timerText.color = Color.white;
            timerFillImage.color = new Color(0f, 0.8f, 1f); // Rengi istediðin gibi deðiþtirebilirsin
        }
    }

    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        // 1. Yeni eþyanýn aðýrlýðýný toplam aðýrlýða ekle
        currentTotalWeight += evt.Weight;

        // 2. Aðýrlýk sýnýrý aþmasýn diye kontrol et
        if (currentTotalWeight > maxWeight)
        {
            currentTotalWeight = maxWeight;
        }

        // Barý anýnda doldurmak YERÝNE, hedefimizi belirliyoruz
        targetWeightFill = (float)currentTotalWeight / maxWeight;

        // Aðýrlýk 0'dan büyükse barý görünür yap
        if (currentTotalWeight > 0)
        {
            weightBarImage.enabled = true;
        }
    }

    private void OnTeammateDied(PlayerDiedEvent evt)
    {
        // Ölen oyuncunun ikonunu kuru kafa yap ve rengini kýrmýzýya çevir
        if (evt.PlayerId >= 0 && evt.PlayerId < teammateIcons.Length)
        {
            teammateIcons[evt.PlayerId].sprite = deadIcon;
            teammateIcons[evt.PlayerId].color = Color.red;
        }
    }

    // Bu fonksiyonu GameSessionManager oyun baþlarken çaðýracak
    public void SetPlayerCount(int playerCount)
    {
        for (int i = 0; i < teammateIcons.Length; i++)
        {
            // Eðer i deðeri oyuncu sayýsýndan küçükse o ikonu aç (true), deðilse gizle (false)
            teammateIcons[i].gameObject.SetActive(i < playerCount);

            // Yeni el baþladýðýnda herkesi hayata döndür (Ýkonlarý resetle)
            teammateIcons[i].sprite = aliveIcon;
            teammateIcons[i].color = Color.white;
        }
    }

    private void Update()
    {
        // Eðer aðýrlýk barýmýz aktifse, mevcut doluluðu hedefe doðru yumuþakça kaydýr (Lerp)
        if (weightBarImage.enabled)
        {
            weightBarImage.fillAmount = Mathf.Lerp(weightBarImage.fillAmount, targetWeightFill, Time.deltaTime * fillSpeed);

            // Eðer eþya býrakýlýrsa ve aðýrlýk 0'a dönerse, bar tamamen boþaldýðýnda onu gizle
            if (currentTotalWeight == 0 && weightBarImage.fillAmount < 0.01f)
            {
                weightBarImage.fillAmount = 0f;
                weightBarImage.enabled = false;
            }
        }
    }
}

