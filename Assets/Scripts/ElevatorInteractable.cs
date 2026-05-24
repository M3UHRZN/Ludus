using Unity.Netcode;
using UnityEngine;

public class ElevatorInteractable : MonoBehaviour, IInteractable
{
    public string InteractPrompt => "Start Run";

    public bool CanInteract(PlayerStateMachine machine) =>
        machine.IsOwner && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

    public void Interact(PlayerStateMachine machine)
    {
        if (!CanInteract(machine)) return;
        if (LobbyRoomManager.Instance != null)
            LobbyRoomManager.Instance.StartRun();
        else
            Debug.LogWarning("[ElevatorInteractable] LobbyRoomManager not found in scene.");
    }
}
