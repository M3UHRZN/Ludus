using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Bu sýnýf, Extraction (Görev Tamamlama) ekranýný yönetir. Görev tamamlandýðýnda veya kota baþarýsýz olduðunda bu ekran açýlýr ve ilgili bilgileri gösterir.
public class ExtractionUIController : MonoBehaviour
{
    [Header("Ana Panel")]
    public GameObject extractionPanel;

    [Header("Metinler")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI creditsText;
    public TextMeshProUGUI penaltyText;

    [Header("Kota Barý")]
    public Image quotaFillImage;

    private void Start()
    {
        extractionPanel.SetActive(false); // Oyun baþlarken gizli
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
        // Ekraný aç
        extractionPanel.SetActive(true);

        // Baþlýk
        if (evt.IsSuccess)
        {
            titleText.text = "GÖREV TAMAMLANDI";
            titleText.color = Color.white;
        }
        else
        {
            titleText.text = "KOTA BAÞARISIZ";
            titleText.color = Color.red;
        }

        // Krediler
        creditsText.text = "Toplanan Kredi: " + evt.CollectedCredits;

        // Ceza Yazýsý
        if (evt.PenaltyAmount > 0)
        {
            penaltyText.text = "Terk Cezasý: -" + evt.PenaltyAmount + " Kredi";
            penaltyText.gameObject.SetActive(true);
        }
        else
        {
            penaltyText.gameObject.SetActive(false);
        }

        // Kota Barý
        if (quotaFillImage != null)
            quotaFillImage.fillAmount = evt.QuotaFillAmount;
    }

    public void ReturnToShip()
    {
        Debug.Log("Sistem: Gemiye Dönülüyor... Lobi sahnesi yüklenecek.");
        // UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene"); 
    }
}