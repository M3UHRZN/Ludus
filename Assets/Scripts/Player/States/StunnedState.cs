using UnityEngine;

public class StunnedState : IPlayerState
{
    private float _stunDuration;
    private float _elapsed;

    public StunnedState(float duration = 3f)
    {
        _stunDuration = duration;
    }

    public void Enter(PlayerStateMachine machine)
    {
        machine.SetComponentsEnabled(
            movement: false,
            look: false,
            interaction: false,
            inventory: false,
            spectator: false);
        _elapsed = 0f;
    }

    public void Tick(PlayerStateMachine machine)
    {
        _elapsed += Time.deltaTime;
        if (_elapsed >= _stunDuration)
            machine.ChangeState(new AliveState());
    }

    public void Exit(PlayerStateMachine machine)
    {
    }
}
