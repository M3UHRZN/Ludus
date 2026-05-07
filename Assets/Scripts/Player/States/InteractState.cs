using UnityEngine;
using UnityEngine.InputSystem;

public class InteractState : IPlayerState
{
    public void Enter(PlayerStateMachine machine)
    {
        machine.NetState.Value = (byte)PlayerStateEnum.Interacting;
        machine.SwitchActionMap("UI");
        machine.SetComponentsEnabled(
            movement: false,
            look: false,
            interaction: false,
            inventory: false,
            spectator: false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Tick(PlayerStateMachine machine)
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame ||
            Keyboard.current.eKey.wasPressedThisFrame)
        {
            machine.ChangeState(new AliveState());
        }
    }

    public void Exit(PlayerStateMachine machine)
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
