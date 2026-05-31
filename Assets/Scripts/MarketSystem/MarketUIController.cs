using TMPro;
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

    [Header("Buy Controls")]
    [SerializeField] private Button buyFlashbangButton;
    [SerializeField] private Button buyTorchButton;
    [SerializeField] private ushort flashbangItemId = 1;
    [SerializeField] private ushort torchItemId = 3;

    [Header("Sell Controls")]
    [SerializeField] private Button sellSelectedButton;
    [SerializeField] private Button sellAllButton;
    [SerializeField] private TMP_InputField sellSlotInput;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    private MarketTransactionService _service;
    private PlayerInventory _inventory;
    private TestPlayer _testPlayer;

    private void Awake()
    {
        NormalizeLegacyIds();
        EnsureTorchButton();
        BindButtons();
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
    }

    public void Open(MarketTransactionService service, PlayerInventory inventory, TestPlayer testPlayer = null)
    {
        if (_service != null && _service.Wallet != null)
            _service.Wallet.CreditsChanged -= OnCreditsChanged;

        _service   = service;
        _inventory = inventory;
        _testPlayer = testPlayer;

        if (_service != null && _service.Wallet != null)
            _service.Wallet.CreditsChanged += OnCreditsChanged;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        // Unlock cursor, freeze player
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        if (_testPlayer != null)
            _testPlayer.SetInputEnabled(false);

        SetStatus("Market opened.");
        Refresh();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        // Lock cursor back, unfreeze player
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        if (_testPlayer != null)
            _testPlayer.SetInputEnabled(true);
    }

    public void BuyFlashbang()
    {
        BuyItem(flashbangItemId);
    }

    public void BuyTorch()
    {
        BuyItem(torchItemId);
    }

    private void BuyItem(ushort itemId)
    {
        if (_inventory == null)
        {
            SetStatus("Inventory is missing.");
            return;
        }

        if (_service == null)
        {
            SetStatus("Market service is missing.");
            return;
        }

        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            _inventory.RequestMarketItemPurchase(itemId, _service.DeliveryPosition, _service.DeliveryForward);
            SetStatus("Purchase requested.");
        }
        else if (_service.IsSpawned && !_service.IsServer)
        {
            _service.RequestBuy(itemId, _inventory);
            SetStatus("Purchase requested.");
        }
        else
        {
            _service.TryBuy(itemId, out string message);
            SetStatus(message);
        }

        Refresh();
    }

    public void SellSelectedSlot()
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

        int slotIndex = ReadSlotIndex();
        if (_service.IsSpawned && !_service.IsServer)
        {
            _service.RequestSellOne(_inventory, slotIndex);
            SetStatus("Sell requested.");
        }
        else
        {
            _service.TrySellOne(_inventory, slotIndex, out string message);
            SetStatus(message);
        }

        Refresh();
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

        if (_service.IsSpawned && !_service.IsServer)
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

    private int ReadSlotIndex()
    {
        if (sellSlotInput == null)
            return 0;

        return int.TryParse(sellSlotInput.text, out int value) ? Mathf.Max(0, value) : 0;
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
        Button buyFlashbang,
        Button buyTorch,
        Button sellSelected,
        Button sellAll,
        TMP_InputField slotInput,
        Button close = null)
    {
        panelRoot        = root;
        creditsText      = credits;
        selectedItemText = selected;
        statusText       = status;
        buyFlashbangButton = buyFlashbang;
        buyTorchButton   = buyTorch;
        sellSelectedButton = sellSelected;
        sellAllButton    = sellAll;
        sellSlotInput    = slotInput;
        closeButton      = close;

        NormalizeLegacyIds();
        EnsureTorchButton();
        BindButtons();
    }

    private void NormalizeLegacyIds()
    {
        if (flashbangItemId == 100)
            flashbangItemId = 1;
    }

    private void BindButtons()
    {
        if (buyFlashbangButton != null)
        {
            buyFlashbangButton.onClick.RemoveListener(BuyFlashbang);
            buyFlashbangButton.onClick.AddListener(BuyFlashbang);
        }

        if (buyTorchButton != null)
        {
            buyTorchButton.onClick.RemoveListener(BuyTorch);
            buyTorchButton.onClick.AddListener(BuyTorch);
        }

        if (sellSelectedButton != null)
        {
            sellSelectedButton.onClick.RemoveListener(SellSelectedSlot);
            sellSelectedButton.onClick.AddListener(SellSelectedSlot);
        }

        if (sellAllButton != null)
        {
            sellAllButton.onClick.RemoveListener(SellAll);
            sellAllButton.onClick.AddListener(SellAll);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }
    }

    private void EnsureTorchButton()
    {
        if (buyTorchButton != null || panelRoot == null)
            return;

        Transform existing = panelRoot.transform.Find("BuyTorchButton");
        if (existing != null && existing.TryGetComponent(out Button existingButton))
        {
            buyTorchButton = existingButton;
            return;
        }

        GameObject obj = new GameObject("BuyTorchButton", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(panelRoot.transform, false);

        Image image = obj.GetComponent<Image>();
        image.color = new Color(0.16f, 0.22f, 0.28f, 1f);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 0.30f);
        rect.anchorMax = new Vector2(0.45f, 0.42f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.transform.SetParent(obj.transform, false);

        RectTransform labelRect = labelObj.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TMP_Text label = labelObj.GetComponent<TMP_Text>();
        label.text = "Buy Torch";
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;

        buyTorchButton = obj.GetComponent<Button>();
    }
}
