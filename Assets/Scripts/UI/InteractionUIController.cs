using UnityEngine;
using TMPro;

public class InteractionUIController : MonoBehaviour
{
    // --- SÝNGLETON EKLENTÝSÝ ---
    public static InteractionUIController Instance { get; private set; }

    private void Awake()
    {
        // Eðer sahnede benden baþka InteractionUIController yoksa, beni 'Instance' yap
        if (Instance == null) Instance = this;
        else Destroy(gameObject); // Yanlýþlýkla 2 tane koyulursa diðerini sil
    }
    // ----------------------------------------------------------------

    [Header("Ana Kapsayýcý")]
    public GameObject interactionContainer;

    [Header("Metinler")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemValueText;

    public void ShowInteraction(string itemName, float value)
    {
        interactionContainer.SetActive(true);
        itemNameText.text = itemName;
        itemValueText.text = $"$ {value}";
    }

    public void HideInteraction()
    {
        interactionContainer.SetActive(false);
    }
}