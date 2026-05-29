using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Tüketilmeyip ele alınan/kuşanılan item'lar için uzantı noktası (fener, silah, ...).
/// Networked equipped state; alt sınıflar OnEquipped/OnHolstered'ı override eder.
/// Tasarım gereği şimdilik somut implementasyon yok — minimal tutuldu.
/// </summary>
public abstract class EquippableItem : BaseItem
{
    public readonly NetworkVariable<bool> IsEquipped = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>Server-only: kuşanma durumunu ayarla; equip/holster hook'larını tetikler.</summary>
    public void ServerSetEquipped(bool equipped)
    {
        if (!IsServer) return;
        if (IsEquipped.Value == equipped) return;
        IsEquipped.Value = equipped;
    }

    public override void OnNetworkSpawn() => IsEquipped.OnValueChanged += OnEquippedChanged;
    public override void OnNetworkDespawn() => IsEquipped.OnValueChanged -= OnEquippedChanged;

    private void OnEquippedChanged(bool previous, bool current)
    {
        if (current) OnEquipped();
        else OnHolstered();
    }

    /// <summary>Tüm peer'larda: bu item kuşanıldığında çağrılır.</summary>
    protected virtual void OnEquipped() { }
    /// <summary>Tüm peer'larda: bu item holster'landığında çağrılır.</summary>
    protected virtual void OnHolstered() { }
}
