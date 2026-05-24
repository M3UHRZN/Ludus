public class AliveState : IPlayerState
{
    public void Enter(PlayerStateMachine machine)
    {
        machine.SwitchActionMap("Gameplay");
        machine.SetComponentsEnabled(
            movement: true,
            look: true,
            interaction: true,
            inventory: true,
            spectator: false);
    }

    public void Tick(PlayerStateMachine machine) { }
    public void Exit(PlayerStateMachine machine) { }
}
