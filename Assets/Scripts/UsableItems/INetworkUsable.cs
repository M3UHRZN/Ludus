/// <summary>
/// Implemented by NetworkBehaviour components on usable item world prefabs.
/// PlayerInventory spawns the prefab's NetworkObject and calls ServerActivate
/// on the server only. The component owns its entire networked behaviour
/// (fuse, blast, effects, despawn) from that point on.
/// </summary>
public interface INetworkUsable
{
    /// <summary>Server-only. The item has been spawned and should now do its thing.</summary>
    void ServerActivate(in UsableActivationContext context);
}
