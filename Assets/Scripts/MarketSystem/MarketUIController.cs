using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class MarketUIController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Text")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text selectedItemText;
    [SerializeField] private TMP_Text statusText;

    [Header("Dynamic Lists")]
    [SerializeField] private Transform buyListContent;    // ScrollView Content (VerticalLayoutGroup)
    [SerializeField] private Transform sellListContent;   // ScrollView Content (VerticalLayoutGroup)

    [Header("Bulk Controls")]
    [SerializeField] private Button sellAllButton;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    private MarketTransactionService _service;
    private PlayerInventory _inventory;
    private TestPlayer _testPlayer;

    private void Awake()
    {
        if (sellAllButton != null)
            sellAllButton.onClick.AddListener(SellAll);

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        Close();
    }

    private void Update()
    {
        if (panelRoot != null && panelRoot.activeSelf)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
                Close();
        }
    }

    private void OnDestroy()
    {
        if (_service != null && _service.Wallet != null)
            _service.Wallet.CreditsChanged -= OnCreditsChanged;

        if (_inventory != null && _inventory.Slots != null)
            _inventory.Slots.OnListChanged -= OnInventoryChanged;
    }

    public void Open(MarketTransactionService service, PlayerInventory inventory, TestPlayer testPlayer = null)
    {
        if (_service != null && _service.Wallet != null)
            _service.Wallet.CreditsChanged -= OnCreditsChanged;

        if (_inventory != null && _inventory.Slots != null)
            _inventory.Slots.OnListChanged -= OnInventoryChanged;

        _service   = service;
        _inventory = inventory;
        _testPlayer = testPlayer;

        if (_service != null && _service.Wallet != null)
            _service.Wallet.CreditsChanged += OnCreditsChanged;

        if (_inventory != null && _inventory.Slots != null)
            _inventory.Slots.OnListChanged += OnInventoryChanged;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        // Unlock cursor, freeze player
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        if (_testPlayer != null)
            _testPlayer.SetInputEnabled(false);

        SetStatus("Market opened.");
        BuildBuyList();
        BuildSellList();
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (_inventory != null && _inventory.Slots != null)
            _inventory.Slots.OnListChanged -= OnInventoryChanged;

        // Lock cursor back, unfreeze player
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        if (_testPlayer != null)
            _testPlayer.SetInputEnabled(true);
    }

    public void SellAll()
    {
        if (_service == null)
        {
            SetStatus("Market service is missing.");
            return;
        }

        if (_inventory == null)
        {
            SetStatus("Inventory is missing.");
            return;
        }

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && _service.IsSpawned && !_service.IsServer)
        {
            _service.RequestSellAll(_inventory);
            SetStatus("Sell all requested.");
        }
        else
        {
            int soldCount = _service.SellAll(_inventory, out int totalCredits);
            SetStatus(soldCount > 0
                ? $"Sold {soldCount} item(s) for {totalCredits} credits."
                : "No sellable items found.");
        }

        Refresh();
    }

    private void OnInventoryChanged(NetworkListEvent<ushort> _)
    {
        BuildSellList();
        Refresh();
    }

    private void BuildBuyList()
    {
        if (buyListContent == null || _service == null) return;
        ClearChildren(buyListContent);
        IReadOnlyList<ItemDefinition> items = _service.GetBuyableItems();
        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition def = items[i];
            if (def == null) continue;
            CreateBuyRow(def);
        }
    }

    private void BuildSellList()
    {
        if (sellListContent == null || _service == null || _inventory == null) return;
        ClearChildren(sellListContent);
        for (int slot = 0; slot < _inventory.Slots.Count; slot++)
        {
            ushort itemId = _inventory.Slots[slot];
            if (itemId == 0) continue;
            ItemDefinition def = ItemCatalog.Instance?.GetById(itemId);
            if (def == null) continue;
            CreateSellRow(def, slot);
        }
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void CreateBuyRow(ItemDefinition def)
    {
        GameObject row = CreateRow(buyListContent, $"BuyRow_{def.Id}");
        CreateRowText(row.transform, "Name", def.DisplayName, 16, TextAlignmentOptions.MidlineLeft);
        int price = _service.GetBuyPriceFor(def.Id);
        CreateRowText(row.transform, "Price", $"{price} cr", 16, TextAlignmentOptions.Center);
        Button button = CreateRowButton(row.transform, "BuyButton", "Buy");

        ushort capturedId = def.Id;
        button.onClick.AddListener(() =>
        {
            if (_service == null || _inventory == null)
                return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && _service.IsSpawned && !_service.IsServer)
            {
                _service.RequestBuy(capturedId, _inventory);
                SetStatus("Purchase requested.");
            }
            else
            {
                _service.TryBuy(capturedId, out string msg);
                SetStatus(!string.IsNullOrEmpty(msg) ? msg : "Purchase requested.");
            }

            Refresh();
        });
    }

    private void CreateSellRow(ItemDefinition def, int slotIndex)
    {
        GameObject row = CreateRow(sellListContent, $"SellRow_{slotIndex}_{def.Id}");
        CreateRowText(row.transform, "Name", $"[{slotIndex}] {def.DisplayName}", 16, TextAlignmentOptions.MidlineLeft);
        int price = _service.GetSellPriceFor(def.Id);
        CreateRowText(row.transform, "Price", $"{price} cr", 16, TextAlignmentOptions.Center);
        Button button = CreateRowButton(row.transform, "SellButton", "Sell");

        int capturedSlot = slotIndex;
        button.onClick.AddListener(() =>
        {
            if (_service == null || _inventory == null)
                return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && _service.IsSpawned && !_service.IsServer)
            {
                _service.RequestSellOne(_inventory, capturedSlot);
                SetStatus("Sell requested.");
            }
            else
            {
                _service.TrySellOne(_inventory, capturedSlot, out string msg);
                SetStatus(msg);
            }
            // Refresh inventory.Slots.OnListChanged uzerinden gelecek
        });
    }

    private GameObject CreateRow(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.16f, 0.22f, 0.28f, 1f);

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.minHeight = 44f;
        layout.preferredHeight = 44f;

        HorizontalLayoutGroup hlg = obj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.padding = new RectOffset(8, 8, 4, 4);
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleCenter;

        return obj;
    }

    private TMP_Text CreateRowText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions align)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = align;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private Button CreateRowButton(Transform parent, string name, string label)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.22f, 0.45f, 0.55f, 1f);
        Button button = obj.AddComponent<Button>();

        LayoutElement layout = obj.AddComponent<LayoutElement>();
        layout.minWidth = 70f;
        layout.preferredWidth = 80f;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(obj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = labelObj.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 16;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void Refresh()
    {
        if (_service != null && _service.Wallet != null && creditsText != null)
            creditsText.text = $"Credits: {_service.Wallet.CurrentCredits}";

        if (selectedItemText != null)
            selectedItemText.text = _inventory != null
                ? $"Inventory Slots: {_inventory.Slots.Count}/{PlayerInventory.MaxSlots}"
                : "Inventory Slots: -";
    }

    private void OnCreditsChanged(int credits)
    {
        Refresh();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
        else if (!string.IsNullOrEmpty(message))
            Debug.Log($"[Market] {message}");
    }

    public void SetExternalStatus(string message)
    {
        SetStatus(message);
        Refresh();
    }

    public void Configure(
        GameObject root,
        TMP_Text credits,
        TMP_Text selected,
        TMP_Text status,
        Transform buyListContent,
        Transform sellListContent,
        Button sellAll,
        Button close = null)
    {
        panelRoot            = root;
        creditsText          = credits;
        selectedItemText     = selected;
        statusText           = status;
        this.buyListContent  = buyListContent;
        this.sellListContent = sellListContent;
        sellAllButton        = sellAll;
        closeButton          = close;
    }
}
