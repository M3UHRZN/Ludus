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
        // Event'ten gelen verileri alp, ekran gsterme fonksiyonuna gonderiyoruz
        ShowExtractionScreen(evt.IsSuccess, evt.CollectedCredits, evt.PenaltyAmount, evt.QuotaFillAmount);
    }
    // --- EVENTBUS ABONELKLER BT ---

    public void ShowExtractionScreen(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
    {
        // 1. Ekran grnr yap
        extractionPanel.SetActive(true);

        // 2. OYUNU DURDUR!
        Time.timeScale = 0f;

        // 3. Bal baar durumuna gre ayarla
        if (isSuccess)
        {
            titleText.text = "GREV TAMAMLANDI";
            titleText.color = Color.white;
        }
        else
        {
            titleText.text = "MISSION FAILED";
            titleText.color = Color.red;
        }

        // Krediler
        creditsText.text = "COLLECTED CREDITS: " + collectedCredits;

        // Ceza Yazs
        if (penaltyAmount > 0)
        {
            penaltyText.text = "PENALTY: -" + penaltyAmount + " CREDITS";
            penaltyText.gameObject.SetActive(true);
        }
        else
        {
            penaltyText.gameObject.SetActive(false);
        }

        // Kota Bar
        if (quotaFillImage != null)
            quotaFillImage.fillAmount = quotaFillAmount;
    }

    // Bu fonksiyon "Gemiye Dn" butonunun OnClick() ksmna balanacak!!!!!!!!
    public void ReturnToShip()
    {
        Time.timeScale = 1f;
        extractionPanel.SetActive(false);
        Debug.Log("[ExtractionUIController] UI closed, scene transition handled by NetworkManager.");
    }
}