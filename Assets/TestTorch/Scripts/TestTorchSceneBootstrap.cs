using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Test-only bootstrap for the TestTorch scene. It starts a local host so the
/// existing networked player, inventory, interaction, and torch RPC flow work
/// without entering through the real main menu/lobby path.
/// </summary>
public sealed class TestTorchSceneBootstrap : MonoBehaviour
{
    private const string SceneName = "TestTorch";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForTestTorchScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
            return;

        if (FindFirstObjectByType<TestTorchSceneBootstrap>() != null)
            return;

        GameObject bootstrap = new GameObject("TestTorchSceneBootstrap");
        bootstrap.AddComponent<TestTorchSceneBootstrap>();
    }

    private bool _startedHostHere;

    private IEnumerator Start()
    {
        yield return null;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogWarning("[TestTorch] NetworkManager not found. Player movement will not run.");
            yield break;
        }

        if (!networkManager.IsListening)
        {
            // Unity Editor süreci, Play durdurulduktan sonra bile UDP socket'leri
            // bir süre tutuyor. Aynı portu tekrar denediğimizde "address already
            // in use" hatası alıyoruz. Her Play oturumunda yüksek aralıkta random
            // bir port seçiyoruz; bind başarısız olursa farklı portlarla retry.
            ushort port = PickRandomTestPort();
            int attempts = 0;
            while (!TryStartHostOnPort(networkManager, port) && attempts < 5)
            {
                attempts++;
                port = PickRandomTestPort();
            }

            if (networkManager.IsListening)
            {
                _startedHostHere = true;
                Debug.Log($"[TestTorch] Local host started for torch testing on port {port}.");
            }
            else
            {
                Debug.LogError("[TestTorch] Host bind failed after 5 attempts. Unity'yi tamamen kapatıp aç.");
                yield break;
            }
        }

        yield return null;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield break;

        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogWarning("[TestTorch] PlayerMovement not found in scene.");
            yield break;
        }

        ConfigureSceneTorches(movement.transform);
    }

    private static void ConfigureSceneTorches(Transform playerTransform)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        TestTorchItem[] torches = FindObjectsByType<TestTorchItem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Vector3 pickupPosition = playerTransform.position + playerTransform.forward * 2f + Vector3.up * 1.1f;
        foreach (TestTorchItem torch in torches)
        {
            if (torch == null)
                continue;

            torch.transform.SetPositionAndRotation(pickupPosition, Quaternion.LookRotation(playerTransform.forward, Vector3.up));

            Rigidbody rigidbody = torch.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.linearVelocity = Vector3.zero;
                rigidbody.angularVelocity = Vector3.zero;
            }

            PhysicsObject physicsObject = torch.GetComponent<PhysicsObject>();
            if (physicsObject == null)
                continue;

            ushort itemId = torch.ItemId;
            if (itemId == 0)
                itemId = 3;

            physicsObject.ServerConfigureInventoryPickup(true, itemId);
        }
    }

    private static bool TryStartHostOnPort(NetworkManager networkManager, ushort port)
    {
        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null) transport = networkManager.NetworkConfig?.NetworkTransport as UnityTransport;
        if (transport == null)
        {
            Debug.LogWarning("[TestTorch] UnityTransport bulunamadı.");
            return false;
        }

        transport.SetConnectionData("127.0.0.1", port);
        return networkManager.StartHost();
    }

    private static ushort PickRandomTestPort()
    {
        // 30000-60000 aralığında random — register edilmiş port'larla çakışma şansı düşük.
        return (ushort)UnityEngine.Random.Range(30000, 60000);
    }

    private void OnApplicationQuit() => GracefulShutdown();
    private void OnDestroy() => GracefulShutdown();

    private void GracefulShutdown()
    {
        if (!_startedHostHere) return;
        _startedHostHere = false;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null) return;
        if (!networkManager.IsListening) return;

        // Play modu sona ererken Netcode'un kendi OnApplicationQuit'inden önce
        // host'u kapatıyoruz; aksi halde NetworkSceneManager.Dispose ve
        // in-scene NetworkObject.OnDestroy NRE atıyor.
        try { networkManager.Shutdown(discardMessageQueue: true); }
        catch (System.Exception ex) { Debug.LogWarning($"[TestTorch] Shutdown warning: {ex.Message}"); }
    }
}
