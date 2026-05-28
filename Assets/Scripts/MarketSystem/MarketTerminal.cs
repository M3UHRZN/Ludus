using UnityEngine;

public class MarketTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private MarketUIController marketUI;
    [SerializeField] private MarketTransactionService transactionService;
    [SerializeField] private string prompt = "E - Open Market";

    public string InteractPrompt => prompt;

    private void Awake()
    {
        EnsureInteractableLayer();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureInteractableLayer();
    }
#endif

    public bool CanInteract(PlayerStateMachine player)
    {
        ResolveReferences();
        return player != null && marketUI != null && transactionService != null;
    }

    public void Interact(PlayerStateMachine player)
    {
        ResolveReferences();
        if (!CanInteract(player))
            return;

        marketUI.Open(transactionService, player.Inventory);
    }

    public void Configure(MarketUIController ui, MarketTransactionService service)
    {
        marketUI = ui;
        transactionService = service;
    }

    private void ResolveReferences()
    {
        if (marketUI == null)
            marketUI = FindFirstObjectByType<MarketUIController>(FindObjectsInactive.Include);

        if (transactionService == null)
            transactionService = FindFirstObjectByType<MarketTransactionService>(FindObjectsInactive.Include);
    }

    private void EnsureInteractableLayer()
    {
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0)
            gameObject.layer = layer;
    }
}
