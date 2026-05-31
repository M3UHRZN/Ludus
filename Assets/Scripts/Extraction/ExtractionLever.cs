using UnityEngine;

/// <summary>
/// Extraction zone içindeki kol. Herhangi bir canlı oyuncu E ile çekince takım tahliyesini
/// tetikler. PlayerInteraction'ın IInteractable yolundan çağrılır (PhysicsObject değildir).
/// </summary>
public class ExtractionLever : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "Pull Extraction Lever [E]";
    private bool _pulled;

    public string InteractPrompt => prompt;

    public bool CanInteract(PlayerStateMachine player)
    {
        return !_pulled && player != null && player.IsAlive && ExtractionService.Instance != null;
    }

    public void Interact(PlayerStateMachine player)
    {
        if (!CanInteract(player)) return;
        _pulled = true; // lokal tek-atış; otorite kararı server'da (idempotent)
        ExtractionService.Instance.RequestTeamExtractionServerRpc();
    }
}
