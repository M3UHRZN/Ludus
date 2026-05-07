public interface IUsable
{
    bool CanUse(PlayerStateMachine user);
    void Use(PlayerStateMachine user);
    float Cooldown { get; }
}
