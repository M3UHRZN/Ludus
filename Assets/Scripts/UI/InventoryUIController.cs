using Unity.Netcode;
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

    private void OnEnable()
    {
        GameEventBus.Subscribe<LocalInventoryUpdatedEvent>(OnInventoryUpdated);

        // Sahne degisiminde / late activation'da PlayerInventory zaten spawn
        // olmus olabilir; subscribe sirasinda kacirmadigimizdan emin olmak icin
        // local player inventory'i bul ve mevcut durumu yeniden ciz.
        RefreshFromLocalInventory();
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<LocalInventoryUpdatedEvent>(OnInventoryUpdated);
    }

    /// <summary>
    /// Local oyuncunun PlayerInventory'sini bulup TriggerUIUpdate cagirir.
    /// UI ile inventory subscribe sirasi yanlistan yana olsa bile bu sayede
    /// HUD her zaman dogru state'i yansitir (lobby -> map gecisinde bos slot
    /// problemini cozer).
    /// </summary>
    private void RefreshFromLocalInventory()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;

        var inv = nm.LocalClient.PlayerObject.GetComponent<PlayerInventory>();
        if (inv != null) inv.TriggerUIUpdate();
    }

    private void OnInventoryUpdated(LocalInventoryUpdatedEvent evt)
    {
        // Agirlik artik tek ItemCatalog'dan.
        float totalWeight = 0f;
        if (ItemCatalog.Instance != null)
            foreach (ushort id in evt.ItemIds) totalWeight += ItemCatalog.Instance.GetWeight(id);
        if (weightText != null) weightText.text = $"{totalWeight:F1} KG";

        for (int i = 0; i < slots.Length; i++)
        {
            if (i < evt.ItemIds.Length)
            {
                ushort itemId = evt.ItemIds[i];

                // Resmi merkez veritabanindan al
                Sprite itemIcon = ItemCatalog.Instance != null
                    ? ItemCatalog.Instance.GetIcon(itemId)
                    : null;

                if (itemIcon != null)
                {
                    slots[i].icon.sprite = itemIcon;
                    slots[i].icon.color = Color.white;
                }
                else
                {
                    slots[i].icon.color = new Color(1, 1, 1, 0);
                }
            }
            else
            {
                slots[i].icon.sprite = null;
                slots[i].icon.color = new Color(1, 1, 1, 0);
            }

            // Aktif slotu vurgula (scroll yapildikca sari cerceve kayar)
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
