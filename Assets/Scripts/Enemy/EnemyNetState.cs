using Unity.Netcode;
using UnityEngine;

// Enemy GameObject'ine ek component. EnemyController MonoBehaviour kalir;
// NetworkVariable'lar bu component uzerinden tasinir.
// Prefab gereksinimi: NetworkObject + EnemyNetState (Sprint 2'de EnemyController'in kendisi NetworkBehaviour'a cevrilince burasi temizlenir).
[RequireComponent(typeof(NetworkObject))]
public class EnemyNetState : NetworkBehaviour
{
    public readonly NetworkVariable<bool> NetIsBlinded = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<float> NetBlindUntilServerTime = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public void ServerSetBlinded(float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        NetIsBlinded.Value = true;
        NetBlindUntilServerTime.Value = (float)NetworkManager.Singleton.ServerTime.Time + duration;
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!NetIsBlinded.Value) return;

        if ((float)NetworkManager.Singleton.ServerTime.Time >= NetBlindUntilServerTime.Value)
            NetIsBlinded.Value = false;
    }
}
