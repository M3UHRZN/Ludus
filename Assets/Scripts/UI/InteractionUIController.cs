using UnityEngine;
using TMPro;

public class InteractionUIController : MonoBehaviour
{
    [Header("Ana Kapsayýcý")]
    public GameObject interactionContainer; // Tüm sistemi tek tuþla gizlemek için

    [Header("Metinler")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI itemValueText;

    // FBXController buraya eþyanýn adýný ve deðerini yollayacak
    public void ShowInteraction(string itemName, float value)
    {
        interactionContainer.SetActive(true);

        // Gelen verileri ilgili metinlere yazdýrýyoruz
        itemNameText.text = itemName;
        itemValueText.text = $"$ {value}";
    }

    public void HideInteraction()
    {
        interactionContainer.SetActive(false);
    }
}