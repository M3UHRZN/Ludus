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
        var mgr = Object.FindFirstObjectByType<LobbyRoomManager>();
        if (mgr != null)
            mgr.StartRun();
        else
            Debug.LogWarning("[ElevatorInteractable] LobbyRoomManager not found in scene.");
    }
}
