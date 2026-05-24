using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUIController : MonoBehaviour
{
    [System.Serializable]
    public class InventorySlotUI
    {
        public Image background; // Slotun kendi çerçevesi
        public Image icon;       // Ýçindeki eþyanýn resmi
    }

    [Header("UI Referanslarý")]
    public InventorySlotUI[] slots; // 3 slotumuzu buraya atacaðýz
    public TextMeshProUGUI weightText;

    [Header("Vurgu Renkleri")]
    public Color activeColor = Color.yellow; // Seçili slotun rengi
    public Color inactiveColor = Color.white; // Seçili olmayan slotun rengi

    // Her eþya alýndýðýnda veya býrakýldýðýnda bu fonksiyon calisacak  
    public void UpdateInventory(Sprite[] currentItems, int activeSlotIndex, float totalWeight)
    {
        // 1. Aðýrlýðý Güncelle
        weightText.text = $"{totalWeight:F1} KG";

        // 2. Slotlarý Güncelle
        for (int i = 0; i < slots.Length; i++)
        {
            // Ýkonu ayarla
            if (i < currentItems.Length && currentItems[i] != null)
            {
                slots[i].icon.sprite = currentItems[i];
                slots[i].icon.color = Color.white; // Ýkonu görünür yap
            }
            else
            {
                slots[i].icon.sprite = null;
                slots[i].icon.color = new Color(1, 1, 1, 0); // Ýkonu gizle (saydam)
            }

            // Aktif slotu vurgula
            if (i == activeSlotIndex)
            {
                slots[i].background.color = activeColor;
                slots[i].background.rectTransform.localScale = Vector3.one * 1.1f; // Biraz büyüt
            }
            else
            {
                slots[i].background.color = inactiveColor;
                slots[i].background.rectTransform.localScale = Vector3.one; // Normal boyut
            }
        }
    }
}