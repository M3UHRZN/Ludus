using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Voidhaul Quartermaster terminal UI controller.
/// Scene-baked panel iskeleti + runtime row builder (item icon + name + price + action).
/// Open/close = backdrop fade + panel scale fade. Credits = tick lerp.
/// </summary>
public class MarketUIController : MonoBehaviour
{
    public enum StatusKind { Info, Success, Warning, Error }

    [Header("Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Animation")]
    [SerializeField] private CanvasGroup backdropGroup;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private RectTransform panelRect;

    [Header("Text")]
    [SerializeField] private TMP_Text creditsText;
    [SerializeField] private TMP_Text selectedItemText;
    [SerializeField] private TMP_Text statusText;

    [Header("Dynamic Lists")]
    [SerializeField] private Transform buyListContent;
    [SerializeField] private Transform sellListContent;

    [Header("Bulk Controls")]
    [SerializeField] private Button sellAllButton;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Animation Tuning")]
    [SerializeField] private float openDuration  = 0.22f;
    [SerializeField] private float closeDuration = 0.14f;
    [SerializeField] private float creditTickDuration = 0.45f;
    [SerializeField] private Vector2 panelStartScale = new(0.96f, 0.96f);

    private MarketTransactionService _service;
    private PlayerInventory _inventory;
    private TestPlayer _testPlayer;
    private PlayerStateMachine _player;
    private string _previousActionMap;

    private Coroutine _openCo;
    private Coroutine _creditTickCo;
    private int _displayedCredits;

    private void Awake()
    {
        if (sellAllButton != null)
        {
            sellAllButton.onClick.AddListener(SellAll);
            sellAllButton.colors = MarketTheme.DangerButton();
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
            closeButton.colors = MarketTheme.IconButton();
        }

        ForceClosedState();
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

        _service    = service;
        _inventory  = inventory;
        _testPlayer = testPlayer;

        if (_service != null && _service.Wallet != null)
            _service.Wallet.CreditsChanged += OnCreditsChanged;
        if (_inventory != null && _inventory.Slots != null)
            _inventory.Slots.OnListChanged += OnInventoryChanged;

        if (panelRoot != null) panelRoot.SetActive(true);

        // Player input'unu InteractState ile ayni pattern'de UI map'ine cevir.
        // Movement/Look/Interaction/Inventory disable — karakter durur, kamera donmez.
        _player = inventory != null ? inventory.GetComponentInParent<PlayerStateMachine>() : null;
        if (_player != null)
        {
            _previousActionMap = _player.PlayerInput != null && _player.PlayerInput.currentActionMap != null
                ? _player.PlayerInput.currentActionMap.name
                : "Gameplay";
            _player.SwitchActionMap("UI");
            _player.SetComponentsEnabled(movement: false, look: false, interaction: false, inventory: false, spectator: false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        if (_testPlayer != null) _testPlayer.SetInputEnabled(false);

        // Krediler hemen senkron olsun (animasyon olmadan baslangic)
        _displayedCredits = _service != null && _service.Wallet != null ? _service.Wallet.CurrentCredits : 0;
        RenderCreditsText(_displayedCredits);

        BuildBuyList();
        BuildSellList();
        UpdateInventoryHeader();
        SetStatus("Terminal online.", StatusKind.Info);

        if (_openCo != null) StopCoroutine(_openCo);
        _openCo = StartCoroutine(AnimateOpen());
    }

    public void Close()
    {
        if (_openCo != null) StopCoroutine(_openCo);
        _openCo = StartCoroutine(AnimateClose());

        if (_inventory != null && _inventory.Slots != null)
            _inventory.Slots.OnListChanged -= OnInventoryChanged;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
        if (_testPlayer != null) _testPlayer.SetInputEnabled(true);

        if (_player != null)
        {
            _player.SwitchActionMap(string.IsNullOrEmpty(_previousActionMap) ? "Gameplay" : _previousActionMap);
            _player.SetComponentsEnabled(movement: true, look: true, interaction: true, inventory: true, spectator: false);
            _player = null;
            _previousActionMap = null;
        }
    }

    private void ForceClosedState()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (backdropGroup != null) { backdropGroup.alpha = 0f; backdropGroup.blocksRaycasts = false; }
        if (panelGroup != null)    { panelGroup.alpha    = 0f; panelGroup.blocksRaycasts    = false; }
        if (panelRect != null)     panelRect.localScale  = panelStartScale;
    }

    private IEnumerator AnimateOpen()
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        float t = 0f;
        if (backdropGroup != null) backdropGroup.blocksRaycasts = true;
        if (panelGroup != null)    panelGroup.blocksRaycasts    = true;
        Vector3 endScale = Vector3.one;
        Vector3 startScale = new(panelStartScale.x, panelStartScale.y, 1f);
        while (t < openDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / openDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f); // easeOutCubic
            if (backdropGroup != null) backdropGroup.alpha = eased;
            if (panelGroup != null)    panelGroup.alpha    = eased;
            if (panelRect != null)     panelRect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }
        if (backdropGroup != null) backdropGroup.alpha = 1f;
        if (panelGroup != null)    panelGroup.alpha    = 1f;
        if (panelRect != null)     panelRect.localScale = endScale;
    }

    private IEnumerator AnimateClose()
    {
        float t = 0f;
        Vector3 endScale = new(panelStartScale.x, panelStartScale.y, 1f);
        Vector3 startScale = panelRect != null ? panelRect.localScale : Vector3.one;
        float aStartBack = backdropGroup != null ? backdropGroup.alpha : 1f;
        float aStartPanel = panelGroup != null ? panelGroup.alpha : 1f;
        while (t < closeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / closeDuration);
            float eased = k * k; // easeInQuad
            if (backdropGroup != null) backdropGroup.alpha = Mathf.Lerp(aStartBack, 0f, eased);
            if (panelGroup != null)    panelGroup.alpha    = Mathf.Lerp(aStartPanel, 0f, eased);
            if (panelRect != null)     panelRect.localScale = Vector3.LerpUnclamped(startScale, endScale, eased);
            yield return null;
        }
        if (backdropGroup != null) { backdropGroup.alpha = 0f; backdropGroup.blocksRaycasts = false; }
        if (panelGroup != null)    { panelGroup.alpha    = 0f; panelGroup.blocksRaycasts    = false; }
        if (panelRect != null)     panelRect.localScale = endScale;
        if (panelRoot != null)     panelRoot.SetActive(false);
    }

    public void SellAll()
    {
        if (_service == null)   { SetStatus("Market service is missing.", StatusKind.Error); return; }
        if (_inventory == null) { SetStatus("Inventory is missing.", StatusKind.Error); return; }

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && _service.IsSpawned && !_service.IsServer)
        {
            _service.RequestSellAll(_inventory);
            SetStatus("Liquidation requested...", StatusKind.Info);
        }
        else
        {
            int soldCount = _service.SellAll(_inventory, out int totalCredits);
            SetStatus(soldCount > 0
                ? $"Liquidated {soldCount} item(s) for {totalCredits} CR."
                : "No sellable items found.",
                soldCount > 0 ? StatusKind.Success : StatusKind.Warning);
        }
    }

    private void OnInventoryChanged(NetworkListEvent<ushort> _)
    {
        BuildSellList();
        UpdateInventoryHeader();
    }

    private void BuildBuyList()
    {
        if (buyListContent == null || _service == null) return;
        ClearChildren(buyListContent);
        IReadOnlyList<ItemDefinition> items = _service.GetBuyableItems();
        if (items.Count == 0)
        {
            CreateEmptyRow(buyListContent, "No goods available");
            return;
        }
        for (int i = 0; i < items.Count; i++)
        {
            ItemDefinition def = items[i];
            if (def == null) continue;
            CreateBuyRow(def, i);
        }
    }

    private void BuildSellList()
    {
        if (sellListContent == null) return;
        ClearChildren(sellListContent);
        if (_service == null || _inventory == null) return;

        int rowIndex = 0;
        bool anyShown = false;
        for (int slot = 0; slot < _inventory.Slots.Count; slot++)
        {
            ushort itemId = _inventory.Slots[slot];
            if (itemId == 0) continue;
            ItemDefinition def = ItemCatalog.Instance?.GetById(itemId);
            if (def == null) continue;
            CreateSellRow(def, slot, rowIndex++);
            anyShown = true;
        }
        if (!anyShown) CreateEmptyRow(sellListContent, "Inventory empty");
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // ============================ Row builders ============================

    private void CreateBuyRow(ItemDefinition def, int rowIndex)
    {
        GameObject row = CreateRowShell(buyListContent, $"BuyRow_{def.Id}", rowIndex);
        CreateIconCell(row.transform, def.Icon, "?");

        var (nameTxt, hintTxt) = CreateTextStack(row.transform);
        nameTxt.text = def.DisplayName;
        hintTxt.text = "ACQUIRE";
        hintTxt.color = MarketTheme.AccentDim;

        int price = _service.GetBuyPriceFor(def.Id);
        CreatePriceCell(row.transform, $"{price} CR", MarketTheme.Accent);

        Button btn = CreateActionButton(row.transform, "BUY", MarketTheme.AccentButton());
        ushort capturedId = def.Id;
        btn.onClick.AddListener(() =>
        {
            if (_service == null || _inventory == null) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && _service.IsSpawned && !_service.IsServer)
            {
                _service.RequestBuy(capturedId, _inventory);
                SetStatus("Acquisition requested...", StatusKind.Info);
            }
            else
            {
                bool ok = _service.TryBuy(capturedId, out string msg);
                SetStatus(string.IsNullOrEmpty(msg) ? "Acquisition complete." : msg,
                    ok ? StatusKind.Success : StatusKind.Warning);
            }
        });
    }

    private void CreateSellRow(ItemDefinition def, int slotIndex, int rowIndex)
    {
        GameObject row = CreateRowShell(sellListContent, $"SellRow_{slotIndex}_{def.Id}", rowIndex);
        CreateIconCell(row.transform, def.Icon, "?");

        var (nameTxt, hintTxt) = CreateTextStack(row.transform);
        nameTxt.text = def.DisplayName;
        hintTxt.text = $"SLOT {slotIndex}";
        hintTxt.color = MarketTheme.SellDim;

        int price = _service.GetSellPriceFor(def.Id);
        bool sellable = price > 0 && def.IsSellable;
        CreatePriceCell(row.transform, sellable ? $"{price} CR" : "—", sellable ? MarketTheme.Sell : MarketTheme.InertText);

        Button btn = CreateActionButton(row.transform, sellable ? "SELL" : "LOCKED",
            sellable ? MarketTheme.SellButton() : MarketTheme.IconButton());
        btn.interactable = sellable;

        int capturedSlot = slotIndex;
        btn.onClick.AddListener(() =>
        {
            if (_service == null || _inventory == null) return;
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening && _service.IsSpawned && !_service.IsServer)
            {
                _service.RequestSellOne(_inventory, capturedSlot);
                SetStatus("Extraction requested...", StatusKind.Info);
            }
            else
            {
                bool ok = _service.TrySellOne(_inventory, capturedSlot, out string msg);
                SetStatus(msg, ok ? StatusKind.Success : StatusKind.Warning);
            }
        });
    }

    private GameObject CreateRowShell(Transform parent, string name, int rowIndex)
    {
        GameObject row = new(name);
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();

        Image bg = row.AddComponent<Image>();
        bg.color = (rowIndex % 2 == 0) ? MarketTheme.RowIdle : MarketTheme.RowAlt;

        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.minHeight = 52f;
        layout.preferredHeight = 52f;

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.padding = new RectOffset(8, 8, 6, 6);
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth  = true;
        hlg.childControlHeight = true;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        return row;
    }

    private void CreateIconCell(Transform parent, Sprite sprite, string placeholderChar)
    {
        GameObject cell = new("Icon");
        cell.transform.SetParent(parent, false);
        cell.AddComponent<RectTransform>();
        Image bg = cell.AddComponent<Image>();
        bg.color = MarketTheme.IconBg;

        LayoutElement le = cell.AddComponent<LayoutElement>();
        le.minWidth = 40f; le.preferredWidth = 40f;
        le.minHeight = 40f; le.preferredHeight = 40f;

        GameObject inner = new("Sprite");
        inner.transform.SetParent(cell.transform, false);
        RectTransform irt = inner.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.1f, 0.1f);
        irt.anchorMax = new Vector2(0.9f, 0.9f);
        irt.offsetMin = Vector2.zero; irt.offsetMax = Vector2.zero;

        if (sprite != null)
        {
            Image img = inner.AddComponent<Image>();
            img.sprite = sprite;
            img.color = Color.white;
            img.preserveAspect = true;
        }
        else
        {
            TextMeshProUGUI ph = inner.AddComponent<TextMeshProUGUI>();
            ph.text = placeholderChar;
            ph.fontSize = 24;
            ph.color = MarketTheme.TextMuted;
            ph.alignment = TextAlignmentOptions.Center;
        }
    }

    private (TMP_Text name, TMP_Text hint) CreateTextStack(Transform parent)
    {
        GameObject stack = new("TextStack");
        stack.transform.SetParent(parent, false);
        stack.AddComponent<RectTransform>();

        LayoutElement le = stack.AddComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 60f;

        VerticalLayoutGroup vlg = stack.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 0f;
        vlg.padding = new RectOffset(4, 0, 0, 0);
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth  = true;
        vlg.childControlHeight = true;
        vlg.childAlignment = TextAnchor.MiddleLeft;

        TMP_Text nameTxt = CreateText(stack.transform, "Name", "", 18, TextAlignmentOptions.MidlineLeft, MarketTheme.TextPrimary, FontStyles.Bold);
        TMP_Text hintTxt = CreateText(stack.transform, "Hint", "", 11, TextAlignmentOptions.MidlineLeft, MarketTheme.TextDim, FontStyles.Normal);
        hintTxt.characterSpacing = 4f;
        return (nameTxt, hintTxt);
    }

    private void CreatePriceCell(Transform parent, string text, Color color)
    {
        GameObject cell = new("Price");
        cell.transform.SetParent(parent, false);
        cell.AddComponent<RectTransform>();

        LayoutElement le = cell.AddComponent<LayoutElement>();
        le.minWidth = 70f; le.preferredWidth = 80f;

        TMP_Text label = CreateText(cell.transform, "Label", text, 18, TextAlignmentOptions.MidlineRight, color, FontStyles.Bold);
        // stretch label to fill cell
        RectTransform rt = label.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private Button CreateActionButton(Transform parent, string label, ColorBlock colors)
    {
        GameObject obj = new("Action");
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        Image img = obj.AddComponent<Image>();
        img.color = colors.normalColor;

        Button btn = obj.AddComponent<Button>();
        btn.colors = colors;
        btn.targetGraphic = img;

        LayoutElement le = obj.AddComponent<LayoutElement>();
        le.minWidth = 84f; le.preferredWidth = 92f;
        le.minHeight = 40f; le.preferredHeight = 40f;

        TMP_Text txt = CreateText(obj.transform, "Label", label, 14, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        txt.characterSpacing = 6f;
        RectTransform rt = txt.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return btn;
    }

    private void CreateEmptyRow(Transform parent, string message)
    {
        GameObject row = new("EmptyRow");
        row.transform.SetParent(parent, false);
        row.AddComponent<RectTransform>();
        Image bg = row.AddComponent<Image>();
        bg.color = MarketTheme.PanelDeep;

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = 52f; le.preferredHeight = 52f;

        TMP_Text label = CreateText(row.transform, "Label", message, 13, TextAlignmentOptions.Center, MarketTheme.TextMuted, FontStyles.Italic);
        label.characterSpacing = 4f;
        RectTransform rt = label.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(12f, 0f); rt.offsetMax = new Vector2(-12f, 0f);
    }

    private TMP_Text CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions align, Color color, FontStyles style)
    {
        GameObject obj = new(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = align;
        label.fontStyle = style;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    // ============================ Header / status ============================

    private void UpdateInventoryHeader()
    {
        if (selectedItemText == null) return;
        if (_inventory == null) { selectedItemText.text = "CARGO //  --"; return; }

        int used = 0;
        for (int i = 0; i < _inventory.Slots.Count; i++)
            if (_inventory.Slots[i] != 0) used++;
        selectedItemText.text = $"CARGO //  {used}/{PlayerInventory.MaxSlots}";
    }

    private void OnCreditsChanged(int credits)
    {
        if (_creditTickCo != null) StopCoroutine(_creditTickCo);
        _creditTickCo = StartCoroutine(TickCreditsTo(credits));
    }

    private IEnumerator TickCreditsTo(int target)
    {
        int from = _displayedCredits;
        float t = 0f;
        while (t < creditTickDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / creditTickDuration);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            int v = Mathf.RoundToInt(Mathf.Lerp(from, target, eased));
            RenderCreditsText(v);
            yield return null;
        }
        _displayedCredits = target;
        RenderCreditsText(target);

        // Pulse: brand renkten parlak parlat ve geri don
        if (creditsText != null) StartCoroutine(PulseCredits());
    }

    private IEnumerator PulseCredits()
    {
        if (creditsText == null) yield break;
        Color baseCol = MarketTheme.Accent;
        Color hi = Color.white;
        float dur = 0.32f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float yoyo = 1f - Mathf.Abs(k * 2f - 1f);
            creditsText.color = Color.Lerp(baseCol, hi, yoyo);
            yield return null;
        }
        creditsText.color = baseCol;
    }

    private void RenderCreditsText(int v)
    {
        if (creditsText != null) creditsText.text = $"CR {v}";
    }

    private void SetStatus(string message, StatusKind kind = StatusKind.Info)
    {
        if (statusText == null)
        {
            if (!string.IsNullOrEmpty(message)) Debug.Log($"[Market] {message}");
            return;
        }
        statusText.text = string.IsNullOrEmpty(message) ? "" : $"> {message}";
        statusText.color = kind switch
        {
            StatusKind.Success => MarketTheme.Success,
            StatusKind.Warning => MarketTheme.Warning,
            StatusKind.Error   => MarketTheme.Error,
            _                  => MarketTheme.TextDim
        };
    }

    public void SetExternalStatus(string message)
    {
        StatusKind kind = ClassifyStatusKind(message);
        SetStatus(message, kind);
    }

    private static StatusKind ClassifyStatusKind(string m)
    {
        if (string.IsNullOrEmpty(m)) return StatusKind.Info;
        string s = m.ToLowerInvariant();
        if (s.StartsWith("bought") || s.StartsWith("sold") || s.StartsWith("liquidated")) return StatusKind.Success;
        if (s.Contains("not enough") || s.Contains("cannot") || s.Contains("invalid") || s.Contains("empty")) return StatusKind.Warning;
        if (s.Contains("missing")    || s.Contains("could not")) return StatusKind.Error;
        return StatusKind.Info;
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

    public void ConfigureAnimation(CanvasGroup backdrop, CanvasGroup panel, RectTransform panelRectTr)
    {
        backdropGroup = backdrop;
        panelGroup    = panel;
        panelRect     = panelRectTr;
    }
}

