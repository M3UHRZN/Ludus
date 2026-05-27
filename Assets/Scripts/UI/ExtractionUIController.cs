using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Sahneler aras� ge�i� i�in �art!

// Bu s�n�f, Extraction (G�rev Tamamlama) ekran�n� y�netir. G�rev tamamland���nda veya kota ba�ar�s�z oldu�unda bu ekran a��l�r ve ilgili bilgileri g�sterir.
public class ExtractionUIController : MonoBehaviour
{
    [Header("Ana Panel")]
    public GameObject extractionPanel;

    [Header("Metinler")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI creditsText;
    public TextMeshProUGUI penaltyText;

    [Header("Kota Bar�")]
    public Image quotaFillImage;

    private void Start()
    {
        extractionPanel.SetActive(false); // Oyun ba�larken gizli
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<LevelEndedEvent>(OnLevelEnded);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<LevelEndedEvent>(OnLevelEnded);
    }

    private void OnLevelEnded(LevelEndedEvent evt)
    {
        // Ekran� a�
        extractionPanel.SetActive(true);

        // Ba�l�k
        if (evt.IsSuccess)
        {
            titleText.text = "MISSION CLEAR";
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
            titleText.text = "MISSION FAILED";
            titleText.color = Color.red;
        }

        // Krediler
        creditsText.text = "COLLECTED CREDITS: " + evt.CollectedCredits;

        // Ceza Yaz�s�
        if (evt.PenaltyAmount > 0)
        {
            penaltyText.text = "PENALTY: -" + evt.PenaltyAmount + " CREDITS";
            penaltyText.gameObject.SetActive(true);
        }
        else
        {
            penaltyText.gameObject.SetActive(false);
        }

        // Kota Bar�
        if (quotaFillImage != null)
            quotaFillImage.fillAmount = evt.QuotaFillAmount;
    }

    public void ReturnToShip()
    {
        Debug.Log("Sistem: Gemiye D�n�l�yor... Lobi sahnesi y�klenecek.");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene"); 
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