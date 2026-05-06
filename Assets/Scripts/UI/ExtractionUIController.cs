using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement; // Sahneler arasý geçiþ için þart!

public class ExtractionUIController : MonoBehaviour
{
    [Header("Ana Panel")]
    public GameObject extractionPanel; // Tüm ekraný açýp kapatmak için

    [Header("Metinler")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI creditsText;
    public TextMeshProUGUI penaltyText;

    [Header("Kota Barý")]
    public Image quotaFillImage;

    private void Start()
    {
        // Oyun baþladýðýnda bu ekran kesinlikle gizli olmalý
        extractionPanel.SetActive(false);
    }

    // Tur bittiðinde Metin'in (veya þimdilik bizim test kodumuzun) çaðýracaðý fonksiyon
    public void ShowExtractionScreen(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
    {

        // 1. Ekraný görünür yap
        extractionPanel.SetActive(true);

        // 2. OYUNU DURDUR!
        Time.timeScale = 0f;

        // 3. Baþlýðý baþarý durumuna göre ayarla
        if (isSuccess)
        {
            titleText.text = "GÖREV TAMAMLANDI";
            titleText.color = Color.white;
        }
        else
        {
            titleText.text = "KOTA BAÞARISIZ";
            titleText.color = Color.red;
        }

        // 4. Kredileri yazdýr
        creditsText.text = "Toplanan Kredi: " + collectedCredits;

        // 5. Ceza kontrolü (Ceza yoksa yazýyý tamamen gizle)
        if (penaltyAmount > 0)
        {
            penaltyText.text = "Terk Cezasý: -" + penaltyAmount + " Kredi";
            penaltyText.gameObject.SetActive(true);
        }
        else
        {
            penaltyText.gameObject.SetActive(false);
        }

        // 6. Kota barýný doldur (0 ile 1 arasýnda bir deðer, örn: %80 için 0.8f)
        quotaFillImage.fillAmount = quotaFillAmount;
    }

    // Bu fonksiyonu "Gemiye Dön" butonunun OnClick() kýsmýna baðlanacak!!!!!!!!
    public void ReturnToShip()
    {
        // 1. Zamaný mutlaka geri baþlat! (Yoksa lobi sahnesi de donuk kalýr, týklayamazsýn bile)
        Time.timeScale = 1f;

        // 2. Lobi sahnesini yükle!
        // Not: Metin lobi sahnesinin adýný (örneðin "LobbyMenu") kesinleþtirdiðinde 
        // aþaðýdaki yorum satýrýný kaldýrýp o ismi yazarsýn. Þimdilik test için Debug atýyoruz.

        Debug.Log("Sistem: Gemiye Dönülüyor... Lobi sahnesi yüklenecek.");
        // SceneManager.LoadScene("LobbyScene"); 
    }
}