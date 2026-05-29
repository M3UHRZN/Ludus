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
        // Geçici Aðýrlýk Hesabý (Þimdilik her eþya 2 KG olsun, gerçek DB gelince düzelir)
        float totalWeight = evt.ItemIds.Length * 2f;
        if (weightText != null) weightText.text = $"{totalWeight:F1} KG";

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < evt.ItemIds.Length)
            {
                ushort itemId = evt.ItemIds[i]; // Çantadaki eþyanýn numarasý

                // Resmi Merkez Veritabanýndan istiyoruz!
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
                slots[i].icon.color = new Color(1, 1, 1, 0); // Boþ slotu gizle
            }

            // Aktif slotu vurgula (Scroll yaptýkça sarý çerçeve kayacak)
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