public class CarryingState : IPlayerState
{
    public void Enter(PlayerStateMachine machine)
    {
        machine.SwitchActionMap("Gameplay");
        machine.SetComponentsEnabled(
            movement: true,
            look: true,
            interaction: true,
            inventory: false,
            spectator: false);
        machine.Movement.SetSpeedMultiplier(0.5f);
    }

    public void Tick(PlayerStateMachine machine) { }

    public void Exit(PlayerStateMachine machine)
    {
        machine.Movement.SetSpeedMultiplier(1f);
    }
}
