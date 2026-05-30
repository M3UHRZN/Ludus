using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Kullanılınca bir şey yapan item'ların tabanı (kullanımda tüketilir semantiği).
/// Server tam bir kez aktive eder; alt sınıflar OnServerActivate'i uygular.
/// </summary>
public abstract class UsableItem : BaseItem
{
    private bool _activated;

    /// <summary>Aktive (arm) edildi mi? Tum peer'lara replike — "armed item cantaya
    /// atilamaz" guard'i ve UI icin. Server yazar, herkes okur.</summary>
    public readonly NetworkVariable<bool> NetActivated = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void ServerActivate(in UsableActivationContext context)
    {
        if (!IsServer)
        {
            Debug.LogWarning($"[{GetType().Name}] ServerActivate off-server; ignored.");
            return;
        }
        if (_activated) return;   // idempotent: tek instance asla iki kez aktive olmaz
        _activated = true;
        NetActivated.Value = true;
        OnServerActivate(context);
    }

    protected abstract void OnServerActivate(in UsableActivationContext context);

    protected void ServerDespawnSelf()
    {
        if (!IsServer) return;
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        else Destroy(gameObject);
    }
}
