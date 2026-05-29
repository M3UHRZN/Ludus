using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Base for every networked usable item. Concrete items (ThrownFlashbang, …)
/// override ServerActivate. The base guards the server-only contract and exposes
/// a helper to despawn safely once the item is finished.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public abstract class NetworkUsableItem : NetworkBehaviour, INetworkUsable
{
    private bool _activated;

    public void ServerActivate(in UsableActivationContext context)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[{GetType().Name}] ServerActivate called off-server; ignored.");
            return;
        }
        if (_activated) return;     // idempotent: never double-activate one instance
        _activated = true;
        OnServerActivate(context);
    }

    /// <summary>Server-only activation body. Runs exactly once per instance.</summary>
    protected abstract void OnServerActivate(in UsableActivationContext context);

    /// <summary>Server-only: despawn + destroy this item's NetworkObject if still spawned.</summary>
    protected void ServerDespawnSelf()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
        else
            Destroy(gameObject);
    }
}
