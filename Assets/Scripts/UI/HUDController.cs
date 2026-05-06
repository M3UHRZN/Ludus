using UnityEngine;
using UnityEngine.UI;   // Image bile�eni i�in gerekli
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("UI Elementleri")]
    public TextMeshProUGUI timerText;
    public Image timerFillImage; // Radial Bar

    [Header("S�re Ayarlar�")]
    public float maxTime = 60f; // Oyunun ba�lang�� s�resi

    [Header("A��rl�k Bar� Ayarlar�")]
    public Image weightBarImage;
    public float fillSpeed = 5f; // Bar�n ne kadar h�zl� akaca��n� belirler

    private int currentTotalWeight = 0; // Oyuncunun o anki toplam a��rl���
    private int maxWeight = 10; // Bar 10 kutu oldu�u i�in s�n�r 10
    private float targetWeightFill = 0f; // Bar�n ula�mak istedi�i hedef nokta

    [Header("Teammate �konlar�")]
    public Image[] teammateIcons;
    public Sprite aliveIcon; 
    public Sprite deadIcon;  

    private void OnEnable()
    {
        // Script aktif oldu�unda EventBus'a abone oluyoruz
        GameEventBus.Subscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);      // Yeni e�ya al�nd���nda a��rl�k bar�n� g�ncellemek i�in
        GameEventBus.Subscribe<PlayerDiedEvent>(OnTeammateDied);
        GameEventBus.Subscribe<SessionStartedEvent>(OnSessionStarted);
    }

    private void OnDisable()
    {
        // Script kapand���nda haf�za s�z�nt�s� olmamas� i�in abonelikten ��k�yoruz
        GameEventBus.Unsubscribe<TimerEventTriggered>(OnTimerUpdated);
        GameEventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnTeammateDied);
        GameEventBus.Unsubscribe<SessionStartedEvent>(OnSessionStarted);
    }

    // --- EVENT D�NLEY�C� FONKS�YONLAR ---

    private void OnTimerUpdated(TimerEventTriggered evt)
    {
        // 1. Say�y� ekrana yazd�r
        timerText.text = evt.RemainingSeconds.ToString("F1");

        // 2. Bar�n doluluk oran�n� ayarla (0 ile 1 aras�nda bir de�er olmal�)
        timerFillImage.fillAmount = evt.RemainingSeconds / maxTime;

        // 3. Son 10 saniye kontrol� 
        if (evt.IsUrgent)
        {
            // Acil durum: Yaz� ve bar k�rm�z� olsun!
            timerText.color = Color.red;
            timerFillImage.color = Color.red;
        }
        else
        {
            // Normal durum: Yaz� beyaz, bar ise haval� bir bilim-kurgu mavisi
            timerText.color = Color.white;
            timerFillImage.color = new Color(0f, 0.8f, 1f); // Rengi istedi�in gibi de�i�tirebilirsin
        }
    }

    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        // 1. Yeni e�yan�n a��rl���n� toplam a��rl��a ekle
        currentTotalWeight += evt.Weight;

        // 2. A��rl�k s�n�r� a�mas�n diye kontrol et
        if (currentTotalWeight > maxWeight)
        {
            currentTotalWeight = maxWeight;
        }

        // Bar� an�nda doldurmak YER�NE, hedefimizi belirliyoruz
        targetWeightFill = (float)currentTotalWeight / maxWeight;

        // A��rl�k 0'dan b�y�kse bar� g�r�n�r yap
        if (currentTotalWeight > 0)
        {
            weightBarImage.enabled = true;
        }
    }

    private void OnSessionStarted(SessionStartedEvent evt)
    {
        maxTime = evt.SessionDuration;
        SetPlayerCount(evt.PlayerCount);
    }

    private void OnTeammateDied(PlayerDiedEvent evt)
    {
        // �len oyuncunun ikonunu kuru kafa yap ve rengini k�rm�z�ya �evir
        if (evt.PlayerId >= 0 && evt.PlayerId < teammateIcons.Length)
        {
            teammateIcons[evt.PlayerId].sprite = deadIcon;
            teammateIcons[evt.PlayerId].color = Color.red;
        }
    }

    // Bu fonksiyonu GameSessionManager oyun ba�larken �a��racak
    public void SetPlayerCount(int playerCount)
    {
        for (int i = 0; i < teammateIcons.Length; i++)
        {
            // E�er i de�eri oyuncu say�s�ndan k���kse o ikonu a� (true), de�ilse gizle (false)
            teammateIcons[i].gameObject.SetActive(i < playerCount);

            // Yeni el ba�lad���nda herkesi hayata d�nd�r (�konlar� resetle)
            teammateIcons[i].sprite = aliveIcon;
            teammateIcons[i].color = Color.white;
        }
    }

    private void Update()
    {
        // E�er a��rl�k bar�m�z aktifse, mevcut dolulu�u hedefe do�ru yumu�ak�a kayd�r (Lerp)
        if (weightBarImage.enabled)
        {
            weightBarImage.fillAmount = Mathf.Lerp(weightBarImage.fillAmount, targetWeightFill, Time.deltaTime * fillSpeed);

            // E�er e�ya b�rak�l�rsa ve a��rl�k 0'a d�nerse, bar tamamen bo�ald���nda onu gizle
            if (currentTotalWeight == 0 && weightBarImage.fillAmount < 0.01f)
            {
                weightBarImage.fillAmount = 0f;
                weightBarImage.enabled = false;
            }
        }
    }
}

