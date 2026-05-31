using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("Gizlenecek HUD Parçaları")]
    public GameObject[] hudElementsToHide;

    private void Awake()
    {
        GameEventBus.Subscribe<RunResultEvent>(OnRunResult);
    }

    private void OnDestroy()
    {
        GameEventBus.Unsubscribe<RunResultEvent>(OnRunResult);
    }

    private void Start()
    {
        if (extractionPanel != null) extractionPanel.SetActive(false);
    }

    private void OnRunResult(RunResultEvent evt)
    {
        bool isSuccess = evt.Reason == SessionEndReason.Escaped || evt.Reason == SessionEndReason.TimeUp;
        bool isWipe    = evt.Reason == SessionEndReason.AllDead;

        ShowExtractionScreen(isSuccess && !isWipe, evt.Net, evt.Penalty, isWipe);
    }

    public void ShowExtractionScreen(bool isSuccess, int netCredits, int penaltyAmount, bool isWipe)
    {
        if (extractionPanel != null) extractionPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hudElementsToHide != null)
            foreach (var element in hudElementsToHide)
                if (element != null) element.SetActive(false);

        if (titleText != null)
        {
            if (isWipe)        { titleText.text = "TEAM WIPED";      titleText.color = Color.red; }
            else if (isSuccess){ titleText.text = "MISSION COMPLETE"; titleText.color = Color.white; }
            else               { titleText.text = "MISSION FAILED";   titleText.color = Color.red; }
        }

        if (creditsText != null)
            creditsText.text = "CREDITS EARNED: " + netCredits;

        if (penaltyText != null)
        {
            if (penaltyAmount > 0)
            {
                penaltyText.text = "PENALTY: -" + penaltyAmount + " CREDITS";
                penaltyText.gameObject.SetActive(true);
            }
            else penaltyText.gameObject.SetActive(false);
        }
    }
}
