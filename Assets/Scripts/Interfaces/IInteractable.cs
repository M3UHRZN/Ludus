public interface IInteractable
{
    bool CanInteract(PlayerStateMachine player);
    void Interact(PlayerStateMachine player);
    string InteractPrompt { get; }
}
