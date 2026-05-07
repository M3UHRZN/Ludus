using UnityEngine;

public class DeadState : IPlayerState
{
    public void Enter(PlayerStateMachine machine)
    {
        machine.NetState.Value = (byte)PlayerStateEnum.Dead;
        machine.SwitchActionMap("Spectator");
        machine.SetComponentsEnabled(
            movement: false,
            look: false,
            interaction: false,
            inventory: false,
            spectator: true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void Tick(PlayerStateMachine machine) { }
    public void Exit(PlayerStateMachine machine) { }
}
