using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Sahneler aras� ge�i� i�in �art!

public class ExtractionUIController : MonoBehaviour
{
    [Header("Ana Panel")]
    public GameObject extractionPanel; // T�m ekran� a��p kapatmak i�in

    [Header("Metinler")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI creditsText;
    public TextMeshProUGUI penaltyText;

    [Header("Kota Bar�")]
    public Image quotaFillImage;

    private void Start()
    {
        // Oyun ba�lad���nda bu ekrani gizli yapar�z, sadece g�rev bitti�inde g�sterilecek
        extractionPanel.SetActive(false);
    }

    // --- EVENTBUS ABONEL�KLER� BA�LANGICI ---
    private void OnEnable()
    {
        GameEventBus.Subscribe<LevelEndedEvent>(OnLevelEnded);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<LevelEndedEvent>(OnLevelEnded);
    }

    // Olay tetiklendi�inde �al��acak fonksiyon
    private void OnLevelEnded(LevelEndedEvent evt)
    {
        // Event'ten gelen verileri al�p, ekran g�sterme fonksiyonuna gonderiyoruz
        // !!!Degisken isimlerini ben belirledim, sen event struct'�nda ne isim verdin ise onu yazars�n. Benim verdi�im isimler sadece �rnek!!!
        ShowExtractionScreen(evt.IsSuccess, evt.CollectedCredits, evt.PenaltyAmount, evt.QuotaFillAmount);
    }
    // --- EVENTBUS ABONEL�KLER� B�T��� ---

    public void ShowExtractionScreen(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
    {

        // 1. Ekran� g�r�n�r yap
        extractionPanel.SetActive(true);

        // 2. OYUNU DURDUR!
        Time.timeScale = 0f;

        // 3. Ba�l��� ba�ar� durumuna g�re ayarla
        if (isSuccess)
        {
            titleText.text = "G�REV TAMAMLANDI";
            titleText.color = Color.white;
        }
        else
        {
            titleText.text = "KOTA BA�ARISIZ";
            titleText.color = Color.red;
        }

        // 4. Kredileri yazd�r
        creditsText.text = "Toplanan Kredi: " + collectedCredits;

        // 5. Ceza kontrol� (Ceza yoksa yaz�y� tamamen gizle)
        if (penaltyAmount > 0)
        {
            penaltyText.text = "Terk Cezas�: -" + penaltyAmount + " Kredi";
            penaltyText.gameObject.SetActive(true);
        }
        else
        {
            penaltyText.gameObject.SetActive(false);
        }

        // 6. Kota bar�n� doldur (0 ile 1 aras�nda bir de�er, �rn: %80 i�in 0.8f)
        quotaFillImage.fillAmount = quotaFillAmount;
    }

    // Bu fonksiyonu "Gemiye D�n" butonunun OnClick() k�sm�na ba�lanacak!!!!!!!!
    public void ReturnToShip()
    {
        Time.timeScale = 1f;
        extractionPanel.SetActive(false);
        Debug.Log("[ExtractionUIController] UI closed, scene transition handled by NetworkManager.");
    }
}