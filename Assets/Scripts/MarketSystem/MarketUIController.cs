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
    [SerializeField] private ushort flashbangItemId = 100;

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
        if (buyFlashbangButton != null)
            buyFlashbangButton.onClick.AddListener(BuyFlashbang);

        if (sellSelectedButton != null)
            sellSelectedButton.onClick.AddListener(SellSelectedSlot);

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
        if (_service == null)
        {
            SetStatus("Market service is missing.");
            return;
        }

        bool success = _service.TryBuy(flashbangItemId, out string message);
        SetStatus(message);
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
        bool success = _service.TrySellOne(_inventory, slotIndex, out string message);
        SetStatus(message);
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

        int soldCount = _service.SellAll(_inventory, out int totalCredits);
        SetStatus(soldCount > 0
            ? $"Sold {soldCount} item(s) for {totalCredits} credits."
            : "No sellable items found.");
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

    public void Configure(
        GameObject root,
        TMP_Text credits,
        TMP_Text selected,
        TMP_Text status,
        Button buyFlashbang,
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
        sellSelectedButton = sellSelected;
        sellAllButton    = sellAll;
        sellSlotInput    = slotInput;
        closeButton      = close;
    }
}
