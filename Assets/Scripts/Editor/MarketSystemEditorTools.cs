using UnityEditor;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MarketSystemEditorTools
{
    private const ushort FlashbangItemId = 100;

    [MenuItem("VoidHaul/Market/Setup Test Scene Objects")]
    public static void SetupTestSceneObjects()
    {
        GameObject root = FindOrCreate("MarketSystem");

        MarketWallet wallet = EnsureComponent<MarketWallet>(root);
        MarketCatalog catalog = EnsureComponent<MarketCatalog>(root);
        EnsureComponent<NetworkObject>(root);
        MarketTransactionService service = EnsureComponent<MarketTransactionService>(root);
        MarketDebugTools debugTools = EnsureComponent<MarketDebugTools>(root);
        MarketUIController ui = EnsureMarketCanvas();
        EnsureEventSystem();

        service.SetWallet(wallet);
        service.SetCatalog(catalog);

        Transform deliveryPoint = FindOrCreateDeliveryPoint(root.transform);
        service.SetDeliveryPoint(deliveryPoint);
        GameObject baseItemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BaseItem.prefab");
        service.SetFallbackDeliveryPrefab(baseItemPrefab);
        MarketTerminal terminal = EnsureMarketTerminal(root.transform, service, ui);

        MarketCatalogItem flashbang = new MarketCatalogItem();
        flashbang.EditorSetDefaults(
            FlashbangItemId,
            "Flashbang",
            "Temporary buyable flashbang item. It cannot be sold back to the market.",
            40,
            0,
            true,
            false,
            -1);
        flashbang.EditorSetDeliveryPrefab(baseItemPrefab);
        catalog.EditorAddOrReplace(flashbang);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(wallet);
        EditorUtility.SetDirty(catalog);
        EditorUtility.SetDirty(service);
        EditorUtility.SetDirty(debugTools);
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(terminal);

        Debug.Log("[MarketSystemEditorTools] Market test objects are ready.");
    }

    [MenuItem("VoidHaul/Market/Add 100 Credits")]
    public static void AddCredits()
    {
        MarketWallet wallet = Object.FindFirstObjectByType<MarketWallet>();
        if (wallet == null)
        {
            Debug.LogWarning("[MarketSystemEditorTools] MarketWallet not found.");
            return;
        }

        Undo.RecordObject(wallet, "Add Market Credits");
        wallet.AddCredits(100);
        EditorUtility.SetDirty(wallet);
    }

    [MenuItem("VoidHaul/Market/Reset Credits")]
    public static void ResetCredits()
    {
        MarketWallet wallet = Object.FindFirstObjectByType<MarketWallet>();
        if (wallet == null)
        {
            Debug.LogWarning("[MarketSystemEditorTools] MarketWallet not found.");
            return;
        }

        Undo.RecordObject(wallet, "Reset Market Credits");
        wallet.ResetToStartingCredits();
        EditorUtility.SetDirty(wallet);
    }

    [MenuItem("VoidHaul/Market/Clear Local Inventory")]
    public static void ClearLocalInventory()
    {
        PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning("[MarketSystemEditorTools] PlayerInventory not found.");
            return;
        }

        for (int i = inventory.Slots.Count - 1; i >= 0; i--)
            inventory.RemoveAtSlot(i);

        Debug.Log("[MarketSystemEditorTools] Inventory clear requested.");
    }

    private static GameObject FindOrCreate(string name)
    {
        GameObject existing = GameObject.Find(name);
        if (existing != null)
            return existing;

        GameObject created = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        return created;
    }

    private static Transform FindOrCreateDeliveryPoint(Transform parent)
    {
        Transform existing = parent.Find("MarketDeliveryPoint");
        if (existing != null)
            return existing;

        GameObject delivery = new GameObject("MarketDeliveryPoint");
        Undo.RegisterCreatedObjectUndo(delivery, "Create Market Delivery Point");
        delivery.transform.SetParent(parent, false);
        delivery.transform.localPosition = new Vector3(0f, 1f, 1.25f);
        return delivery.transform;
    }

    private static MarketTerminal EnsureMarketTerminal(
        Transform parent,
        MarketTransactionService service,
        MarketUIController ui)
    {
        GameObject terminalObj = GameObject.Find("MarketTerminal");
        if (terminalObj == null)
        {
            terminalObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            terminalObj.name = "MarketTerminal";
            Undo.RegisterCreatedObjectUndo(terminalObj, "Create Market Terminal");
            terminalObj.transform.SetParent(parent, false);
            terminalObj.transform.position = GetTerminalPosition();
            terminalObj.transform.localScale = new Vector3(1.2f, 0.15f, 0.8f);

            Renderer renderer = terminalObj.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = CreateTerminalMaterial();
        }

        int interactableLayer = LayerMask.NameToLayer("Interactable");
        if (interactableLayer >= 0)
            terminalObj.layer = interactableLayer;

        BoxCollider collider = terminalObj.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = false;
            collider.size = Vector3.one;
        }

        MarketTerminal terminal = EnsureComponent<MarketTerminal>(terminalObj);
        terminal.Configure(ui, service);
        return terminal;
    }

    private static Vector3 GetTerminalPosition()
    {
        LobbySpawnPoint spawnPoint = Object.FindFirstObjectByType<LobbySpawnPoint>();
        if (spawnPoint != null)
            return spawnPoint.transform.position + spawnPoint.transform.forward * 2f + Vector3.up * 0.9f;

        return new Vector3(0f, 0.9f, 2f);
    }

    private static MarketUIController EnsureMarketCanvas()
    {
        GameObject canvasObj = GameObject.Find("MarketCanvas");
        if (canvasObj == null)
        {
            canvasObj = new GameObject("MarketCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObj, "Create Market Canvas");
        }

        Canvas canvas = EnsureComponent<Canvas>(canvasObj);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        EnsureComponent<CanvasScaler>(canvasObj);
        EnsureComponent<GraphicRaycaster>(canvasObj);

        GameObject panel = FindOrCreateChild(canvasObj.transform, "MarketPanel");
        Image panelImage = EnsureComponent<Image>(panel);
        panelImage.color = new Color(0.04f, 0.05f, 0.06f, 0.92f);
        RectTransform panelRect = EnsureRect(panel);
        panelRect.anchorMin = new Vector2(0.25f, 0.18f);
        panelRect.anchorMax = new Vector2(0.75f, 0.82f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        TMP_Text title = EnsureText(panel.transform, "Title", "VOIDHAUL MARKET", 28, new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.96f));
        TMP_Text credits = EnsureText(panel.transform, "CreditsText", "Credits: 0", 20, new Vector2(0.58f, 0.72f), new Vector2(0.95f, 0.82f));
        TMP_Text selected = EnsureText(panel.transform, "SelectedItemText", "Inventory Slots: -", 18, new Vector2(0.05f, 0.64f), new Vector2(0.95f, 0.72f));
        TMP_Text status = EnsureText(panel.transform, "StatusText", "Open market from terminal.", 16, new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.15f));

        Button buyFlashbang = EnsureButton(panel.transform, "BuyFlashbangButton", "Buy Flashbang", new Vector2(0.05f, 0.46f), new Vector2(0.45f, 0.58f));
        Button buyTorch     = EnsureButton(panel.transform, "BuyTorchButton",     "Buy Torch",      new Vector2(0.05f, 0.30f), new Vector2(0.45f, 0.42f));
        Button sellSelected = EnsureButton(panel.transform, "SellSelectedButton", "Sell One",      new Vector2(0.55f, 0.46f), new Vector2(0.95f, 0.58f));
        Button sellAll      = EnsureButton(panel.transform, "SellAllButton",      "Sell All",      new Vector2(0.55f, 0.30f), new Vector2(0.95f, 0.42f));
        Button closeBtn     = EnsureButton(panel.transform, "CloseButton",        "✕ Close",       new Vector2(0.78f, 0.84f), new Vector2(0.98f, 0.96f));
        TMP_InputField slotInput = EnsureInput(panel.transform, "SellSlotInput", "0", new Vector2(0.55f, 0.18f), new Vector2(0.75f, 0.28f));

        MarketUIController ui = EnsureComponent<MarketUIController>(canvasObj);
        ui.Configure(panel, credits, selected, status, buyFlashbang, buyTorch, sellSelected, sellAll, slotInput, closeBtn);
        panel.SetActive(false);

        EditorUtility.SetDirty(canvasObj);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(title);
        return ui;
    }

    private static TMP_Text EnsureText(
        Transform parent,
        string name,
        string text,
        int fontSize,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = FindOrCreateChild(parent, name);
        TMP_Text label = EnsureComponent<TextMeshProUGUI>(obj);
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

    private static Button EnsureButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = FindOrCreateChild(parent, name);
        Image image = EnsureComponent<Image>(obj);
        image.color = new Color(0.16f, 0.22f, 0.28f, 1f);
        Button button = EnsureComponent<Button>(obj);

        RectTransform rect = EnsureRect(obj);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text text = EnsureText(obj.transform, "Label", label, 18, Vector2.zero, Vector2.one);
        text.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_InputField EnsureInput(
        Transform parent,
        string name,
        string text,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject obj = FindOrCreateChild(parent, name);
        Image image = EnsureComponent<Image>(obj);
        image.color = new Color(0.09f, 0.11f, 0.13f, 1f);

        RectTransform rect = EnsureRect(obj);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TMP_Text textComponent = EnsureText(obj.transform, "Text", text, 18, Vector2.zero, Vector2.one);
        textComponent.margin = new Vector4(12f, 0f, 12f, 0f);
        TMP_InputField input = EnsureComponent<TMP_InputField>(obj);
        input.textComponent = textComponent;
        input.text = text;
        return input;
    }

    private static GameObject FindOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject created = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(created, $"Create {name}");
        created.transform.SetParent(parent, false);
        return created;
    }

    private static RectTransform EnsureRect(GameObject obj)
    {
        RectTransform rect = obj.GetComponent<RectTransform>();
        if (rect != null)
            return rect;

        return Undo.AddComponent<RectTransform>(obj);
    }

    private static Material CreateTerminalMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = new Color(0.04f, 0.12f, 0.18f, 1f);
        return material;
    }

    private static void EnsureEventSystem()
    {
        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing != null)
        {
            if (existing.GetComponent<InputSystemUIInputModule>() == null)
                Undo.AddComponent<InputSystemUIInputModule>(existing.gameObject);
            return;
        }

        GameObject eventSystem = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
            return component;

        Undo.RecordObject(target, $"Add {typeof(T).Name}");
        return target.AddComponent<T>();
    }
}
