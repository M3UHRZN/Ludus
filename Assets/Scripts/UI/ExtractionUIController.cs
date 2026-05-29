using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode; // AĞ KÜTÜPHANESİ EKLENDİ

public class ExtractionUIController : MonoBehaviour
{
    [Header("Ana Panel")]
    public GameObject extractionPanel;

    [Header("Metinler")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI creditsText;
    public TextMeshProUGUI penaltyText;

    [Header("Kota Barı")]
    public Image quotaFillImage;

    // Öldüğümüzde ekrandan silinmesini istediğimiz şeyler (Timer, Stamina vb.)
    [Header("Gizlenecek HUD Parçaları")]
    public GameObject[] hudElementsToHide;

    private void Awake()
    {
        // GameSessionManager'ın fırlattığı sinyali dinliyoruz!
        GameEventBus.Subscribe<SessionEndedEvent>(OnSessionEnded);
        GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnDestroy()
    {
        GameEventBus.Unsubscribe<SessionEndedEvent>(OnSessionEnded);
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void Start()
    {
        extractionPanel.SetActive(false);
    }

    // SÜRE/OYUN BİTTİĞİNDE ÇALIŞIR
    private void OnSessionEnded(SessionEndedEvent evt)
    {
        bool isSuccess = (evt.Reason == SessionEndReason.Escaped);

        // === Parayı ExtractionManager dan çekiyoruz! ===
        int finalCredits = 0;
        if (ExtractionManager.Instance != null)
        {
            finalCredits = ExtractionManager.Instance.TotalCredits.Value;
        }

        ShowExtractionScreen(isSuccess, finalCredits, 0, 0f);
    }

    // BİZ ÖLDÜĞÜMÜZDE ÇALIŞIR
    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        if (NetworkManager.Singleton != null && evt.PlayerId == (int)NetworkManager.Singleton.LocalClientId)
        {
            // === Parayı ExtractionManager dan çekiyoruz! ===
            int currentCredits = 0;
            if (ExtractionManager.Instance != null)
            {
                currentCredits = ExtractionManager.Instance.TotalCredits.Value;
            }

            ShowExtractionScreen(false, currentCredits, 50, 0f);
        }
    }

    public void ShowExtractionScreen(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
    {
        extractionPanel.SetActive(true);
        // Oyuncu butona tıklayabilsin diye farenin kilidini aç ve görünür yap!
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Arkadaki Timer, Stamina, Envanter gibi şeyleri gizliyoruz
        if (hudElementsToHide != null)
        {
            foreach (var element in hudElementsToHide)
            {
                if (element != null) element.SetActive(false);
            }
        }

        if (isSuccess)
        {
            titleText.text = "MISSION SUCCESS";
            titleText.color = Color.white;
        }
        else
        {
            titleText.text = "MISSION FAILED";
            titleText.color = Color.red;
        }

        creditsText.text = "COLLECTED CREDITS: " + collectedCredits;

        if (penaltyAmount > 0)
        {
            penaltyText.text = "PENALTY: -" + penaltyAmount + " CREDITS";
            penaltyText.gameObject.SetActive(true);
        }
        else
        {
            penaltyText.gameObject.SetActive(false);
        }

        if (quotaFillImage != null)
            quotaFillImage.fillAmount = quotaFillAmount;
    }

    public void ReturnToShip()
    {
        extractionPanel.SetActive(false);
        // SceneManager.LoadScene("LobbyScene"); 
    }
}