using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MarketRuntimeBootstrap
{
    private const string MarketRootName = "MarketSystem";
    private const string MarketCanvasName = "MarketCanvas";
    private const string MarketTerminalName = "MarketTerminal";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySetup(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TrySetup(scene);
    }

    private static void TrySetup(Scene scene)
    {
        if (!scene.IsValid() || scene.name != SceneNames.Lobby)
            return;

        MarketUIController ui = Object.FindFirstObjectByType<MarketUIController>(FindObjectsInactive.Include);
        if (ui == null)
            ui = CreateMarketCanvas();

        GameObject root = GameObject.Find(MarketRootName);
        if (root == null)
            root = new GameObject(MarketRootName);

        MarketWallet wallet = EnsureComponent<MarketWallet>(root);
        MarketCatalog catalog = EnsureComponent<MarketCatalog>(root);
        MarketTransactionService service = EnsureComponent<MarketTransactionService>(root);
        service.SetWallet(wallet);
        service.SetCatalog(catalog);

        Transform deliveryPoint = root.transform.Find("MarketDeliveryPoint");
        if (deliveryPoint == null)
        {
            GameObject delivery = new GameObject("MarketDeliveryPoint");
            delivery.transform.SetParent(root.transform, false);
            delivery.transform.position = GetTerminalPosition() + Vector3.up * 0.4f + Vector3.forward * 0.8f;
            deliveryPoint = delivery.transform;
        }
        service.SetDeliveryPoint(deliveryPoint);

        MarketTerminal terminal = Object.FindFirstObjectByType<MarketTerminal>(FindObjectsInactive.Include);
        if (terminal == null)
            terminal = CreateTerminal(root.transform);

        terminal.Configure(ui, service);
        EnsureEventSystem();
    }

    private static MarketTerminal CreateTerminal(Transform parent)
    {
        GameObject terminalObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        terminalObject.name = MarketTerminalName;
        terminalObject.transform.SetParent(parent, false);
        terminalObject.transform.position = GetTerminalPosition();
        terminalObject.transform.localScale = new Vector3(1.2f, 0.15f, 0.8f);

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            terminalObject.layer = interactableLayer;

        Renderer renderer = terminalObject.GetComponent<Renderer>();
        if (renderer != null)
            renderer.material.color = new Color(0.08f, 0.35f, 0.42f, 1f);

        return terminalObject.AddComponent<MarketTerminal>();
    }

    private static Vector3 GetTerminalPosition()
    {
        LobbySpawnPoint spawnPoint = Object.FindFirstObjectByType<LobbySpawnPoint>();
        if (spawnPoint != null)
            return spawnPoint.transform.position + spawnPoint.transform.forward * 2f + Vector3.up * 0.9f;

        return new Vector3(0f, 0.9f, 2f);
    }

    private static MarketUIController CreateMarketCanvas()
    {
        GameObject canvasObject = new GameObject(MarketCanvasName);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = CreateChild(canvasObject.transform, "MarketPanel");
        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.04f, 0.05f, 0.06f, 0.92f);
        RectTransform panelRect = EnsureRect(panel);
        panelRect.anchorMin = new Vector2(0.25f, 0.18f);
        panelRect.anchorMax = new Vector2(0.75f, 0.82f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        TMP_Text credits = CreateText(panel.transform, "CreditsText", "Credits: 0", 20, new Vector2(0.58f, 0.72f), new Vector2(0.95f, 0.82f));
        TMP_Text selected = CreateText(panel.transform, "SelectedItemText", "Inventory Slots: -", 18, new Vector2(0.05f, 0.64f), new Vector2(0.95f, 0.72f));
        TMP_Text status = CreateText(panel.transform, "StatusText", "Open market from terminal.", 16, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.15f));
        CreateText(panel.transform, "Title", "VOIDHAUL MARKET", 28, new Vector2(0.05f, 0.83f), new Vector2(0.72f, 0.96f));

        Button buy = CreateButton(panel.transform, "BuyFlashbangButton", "Buy Flashbang", new Vector2(0.05f, 0.46f), new Vector2(0.45f, 0.58f));
        Button buyTorch = CreateButton(panel.transform, "BuyTorchButton", "Buy Torch", new Vector2(0.05f, 0.30f), new Vector2(0.45f, 0.42f));
        Button sellOne = CreateButton(panel.transform, "SellSelectedButton", "Sell One", new Vector2(0.55f, 0.46f), new Vector2(0.95f, 0.58f));
        Button sellAll = CreateButton(panel.transform, "SellAllButton", "Sell All", new Vector2(0.55f, 0.30f), new Vector2(0.95f, 0.42f));
        Button close = CreateButton(panel.transform, "CloseButton", "Close", new Vector2(0.78f, 0.84f), new Vector2(0.98f, 0.96f));
        TMP_InputField slotInput = CreateInput(panel.transform, "SellSlotInput", "0", new Vector2(0.55f, 0.18f), new Vector2(0.75f, 0.28f));

        MarketUIController ui = canvasObject.AddComponent<MarketUIController>();
        ui.Configure(panel, credits, selected, status, buy, buyTorch, sellOne, sellAll, slotInput, close);
        panel.SetActive(false);
        return ui;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, int fontSize, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = CreateChild(parent, name);
        TMP_Text label = obj.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.MidlineLeft;

        RectTransform rect = EnsureRect(obj);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = CreateChild(parent, name);
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.16f, 0.22f, 0.28f, 1f);
        Button button = obj.AddComponent<Button>();

        RectTransform rect = EnsureRect(obj);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text text = CreateText(obj.transform, "Label", label, 18, Vector2.zero, Vector2.one);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_InputField CreateInput(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = CreateChild(parent, name);
        Image image = obj.AddComponent<Image>();
        image.color = new Color(0.12f, 0.14f, 0.16f, 1f);
        TMP_InputField input = obj.AddComponent<TMP_InputField>();

        RectTransform rect = EnsureRect(obj);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text label = CreateText(obj.transform, "Text", text, 18, Vector2.zero, Vector2.one);
        label.margin = new Vector4(8f, 0f, 8f, 0f);
        input.textComponent = label;
        input.text = text;
        return input;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private static RectTransform EnsureRect(GameObject obj)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect == null)
            rect = obj.AddComponent<RectTransform>();
        return rect;
    }

    private static T EnsureComponent<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }
}
