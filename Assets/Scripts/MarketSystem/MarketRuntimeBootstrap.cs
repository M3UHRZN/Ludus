using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Lobby sahnesi yuklenince market sisteminin runtime baglantilarini kurar.
/// MarketCanvas ve MarketSystem iskeletleri scene-baked; bu sinif sadece
/// terminal/delivery-point/event-system gibi kucuk eksikleri tamamlar.
/// </summary>
public static class MarketRuntimeBootstrap
{
    private const string MarketRootName = "MarketSystem";
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
        {
            Debug.LogError("[Market] MarketUIController sahnede bulunamadi — Lobby sahnesinde MarketCanvas iskeleti olmali (scene-baked).");
            return;
        }

        GameObject root = GameObject.Find(MarketRootName);
        if (root == null)
            root = new GameObject(MarketRootName);

        MarketWallet wallet = EnsureComponent<MarketWallet>(root);
        MarketTransactionService service = EnsureComponent<MarketTransactionService>(root);
        service.SetWallet(wallet);

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

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
            eventSystem = new GameObject("EventSystem").AddComponent<EventSystem>();

        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    private static T EnsureComponent<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }
}
