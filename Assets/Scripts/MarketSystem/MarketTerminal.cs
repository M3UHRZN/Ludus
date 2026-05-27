using UnityEngine;

public class MarketTerminal : MonoBehaviour, IInteractable
{
    [SerializeField] private MarketUIController marketUI;
    [SerializeField] private MarketTransactionService transactionService;
    [SerializeField] private string prompt = "E - Open Market";

    public string InteractPrompt => prompt;

    public bool CanInteract(PlayerStateMachine player)
    {
        return player != null && marketUI != null && transactionService != null;
    }

    public void Interact(PlayerStateMachine player)
    {
        if (!CanInteract(player))
            return;

        marketUI.Open(transactionService, player.Inventory);
    }

    public void Configure(MarketUIController ui, MarketTransactionService service)
    {
        marketUI = ui;
        transactionService = service;
    }
}
