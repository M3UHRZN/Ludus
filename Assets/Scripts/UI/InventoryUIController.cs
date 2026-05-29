using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUIController : MonoBehaviour
{
    [System.Serializable]
    public class InventorySlotUI
    {
        public Image background; // Slotun kendi cercevesi
        public Image icon;       // Icindeki esyanin resmi
    }

    [Header("UI Referanslari")]
    public InventorySlotUI[] slots;
    public TextMeshProUGUI weightText;

    [Header("Vurgu Renkleri")]
    public Color activeColor = Color.yellow;
    public Color inactiveColor = Color.white;

    private void Awake()
    {
        GameEventBus.Subscribe<LocalInventoryUpdatedEvent>(OnInventoryUpdated);
    }

    private void OnDestroy()
    {
        GameEventBus.Unsubscribe<LocalInventoryUpdatedEvent>(OnInventoryUpdated);
    }

    private void OnInventoryUpdated(LocalInventoryUpdatedEvent evt)
    {
        // Gecici agirlik hesabi (her esya 2 KG; ileride ItemDatabase'den okunur)
        float totalWeight = evt.ItemIds.Length * 2f;
        if (weightText != null) weightText.text = $"{totalWeight:F1} KG";

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < evt.ItemIds.Length)
            {
                ushort itemId = evt.ItemIds[i]; // Cantadaki esyanin numarasi

                // Resmi merkez veritabanindan istiyoruz
                Sprite itemIcon = ItemDatabase.Instance.GetIcon(itemId);

                if (itemIcon != null)
                {
                    slots[i].icon.sprite = itemIcon;
                    slots[i].icon.color = Color.white;
                }
                else
                {
                    slots[i].icon.color = new Color(1, 1, 1, 0); // Resim yoksa gizle
                }
            }
            else
            {
                slots[i].icon.sprite = null;
                slots[i].icon.color = new Color(1, 1, 1, 0); // Bos slotu gizle
            }

            // Aktif slotu vurgula (Scroll yapildikca sari cerceve kayar)
            if (i == evt.ActiveSlotIndex)
            {
                slots[i].background.color = activeColor;
                slots[i].background.rectTransform.localScale = Vector3.one * 1.1f;
            }
            else
            {
                slots[i].background.color = inactiveColor;
                slots[i].background.rectTransform.localScale = Vector3.one;
            }
        }
    }
}
