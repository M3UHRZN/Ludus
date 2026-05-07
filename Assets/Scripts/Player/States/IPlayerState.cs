public interface IPlayerState
{
    void Enter(PlayerStateMachine machine);
    void Tick(PlayerStateMachine machine);
    void Exit(PlayerStateMachine machine);
}
