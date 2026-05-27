using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class ExitZoneInteractable : MonoBehaviour, IInteractable
{
    [Header("Prompts")]
    public string readyPrompt    = "Return to Lobby [E]";
    public string notReadyPrompt = "Drop an item first!";

    private PlayerStateMachine _playerInZone;

    public string InteractPrompt =>
        (ExtractionManager.Instance != null && ExtractionManager.Instance.HasExtractedItems)
            ? readyPrompt
            : notReadyPrompt;

    public bool CanInteract(PlayerStateMachine machine)
    {
        if (ExtractionManager.Instance == null) return false;
        return ExtractionManager.Instance.HasExtractedItems;
    }

    public void Interact(PlayerStateMachine machine)
    {
        Debug.Log("[ExitZone] Interact çağrıldı!");
        if (!CanInteract(machine)) return;
        ExtractionManager.Instance?.RequestTeamExtractionRpc();
    }

    private void OnTriggerEnter(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine == null || !machine.IsOwner) return;

        _playerInZone = machine;
        Debug.Log("[ExitZone] Oyuncu girdi!");
    }

    private void OnTriggerExit(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>();
        if (machine == null) machine = other.GetComponentInParent<PlayerStateMachine>();
        if (machine == null) return;

        if (_playerInZone == machine)
            _playerInZone = null;
    }

    private void Update()
    {
        if (_playerInZone == null) return;
        if (!_playerInZone.IsOwner) return;

        var playerInput = _playerInZone.PlayerInput;
        if (playerInput != null)
        {
            var interactAction = playerInput.actions.FindAction("Interact") ?? playerInput.actions["Gameplay/Interact"];
            if (interactAction != null && interactAction.WasPressedThisFrame())
            {
                Debug.Log("[ExitZone] Interact aksiyonu tetiklendi!");
                Interact(_playerInZone);
            }
        }
    }

}