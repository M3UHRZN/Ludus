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
        // Null-safe: prefab tam wire edilmeden de bu script crash etmesin.
        // Designer slot referanslarini henuz atamamis olabilir — sessizce skip.
        if (slots == null) return;

        ushort[] ids = evt.ItemIds ?? System.Array.Empty<ushort>();

        // Gecici agirlik hesabi (her esya 2 KG; ileride ItemDatabase'den okunur)
        if (weightText != null)
        {
            float totalWeight = ids.Length * 2f;
            weightText.text = $"{totalWeight:F1} KG";
        }

        for (int i = 0; i < slots.Length; i++)
        {
            var slotUI = slots[i];
            if (slotUI == null) continue;

            if (i < ids.Length)
            {
                ushort itemId = ids[i];

                // ItemDatabase.Instance henuz yoksa veya icon kayit yoksa gizle.
                Sprite itemIcon = (ItemDatabase.Instance != null)
                    ? ItemDatabase.Instance.GetIcon(itemId)
                    : null;

                if (slotUI.icon != null)
                {
                    if (itemIcon != null)
                    {
                        slotUI.icon.sprite = itemIcon;
                        slotUI.icon.color = Color.white;
                    }
                    else
                    {
                        slotUI.icon.color = new Color(1, 1, 1, 0); // Resim yoksa gizle
                    }
                }
            }
            else if (slotUI.icon != null)
            {
                slotUI.icon.sprite = null;
                slotUI.icon.color = new Color(1, 1, 1, 0); // Bos slotu gizle
            }

            // Aktif slotu vurgula (Scroll yapildikca sari cerceve kayar)
            if (slotUI.background != null)
            {
                if (i == evt.ActiveSlotIndex)
                {
                    slotUI.background.color = activeColor;
                    slotUI.background.rectTransform.localScale = Vector3.one * 1.1f;
                }
                else
                {
                    slotUI.background.color = inactiveColor;
                    slotUI.background.rectTransform.localScale = Vector3.one;
                }
            }
        }
    }
}
