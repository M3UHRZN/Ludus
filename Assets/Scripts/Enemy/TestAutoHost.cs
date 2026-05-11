using Unity.Netcode;
using UnityEngine;

/// <summary>
/// AlpTest.unity sahnesi icin yardimci. Play moduna gecince otomatik
/// olarak NetworkManager.StartHost() cagirir; boylece EnemyController'in
/// IsServer kontrolu gecip AI calisir.
///
/// Sadece test sahnesinde kullanilir, gercek lobby/menu akisinda devre disi
/// birakilir veya GameObject silinir.
/// </summary>
[RequireComponent(typeof(NetworkManager))]
public class TestAutoHost : MonoBehaviour
{
    [SerializeField] private bool _autoHostOnStart = true;

    private void Start()
    {
        if (!_autoHostOnStart) return;

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[TestAutoHost] NetworkManager.Singleton bulunamadi.");
            return;
        }

        if (nm.IsHost || nm.IsServer || nm.IsClient)
        {
            Debug.Log("[TestAutoHost] Network zaten aktif, atlandi.");
            return;
        }

        nm.StartHost();
        Debug.Log("[TestAutoHost] StartHost cagrildi.");
    }
}
