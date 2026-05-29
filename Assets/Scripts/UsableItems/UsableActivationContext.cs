using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Everything a usable item needs to know when the server activates it.
/// Built on the server inside PlayerInventory; never trusts raw client data
/// beyond what FlashbangMath validates.
/// </summary>
public readonly struct UsableActivationContext
{
    public readonly ulong UserClientId;
    public readonly NetworkObjectReference UserObject;
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;

    public UsableActivationContext(
        ulong userClientId, NetworkObjectReference userObject, Vector3 origin, Vector3 direction)
    {
        UserClientId = userClientId;
        UserObject = userObject;
        Origin = origin;
        Direction = direction;
    }
}
