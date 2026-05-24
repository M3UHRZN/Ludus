using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// AlpTest.unity sahnesi icin yardimci. T tusuna basilinca
/// GameEventBus.Publish(new MapReadyEvent(...)) cagirir; boylece
/// EnemySpawner devreye girip sahnedeki marker'lari tarayarak
/// enemy spawn eder.
///
/// Gercek oyunda bunun yerine MapGenerator.cs (Anil) bu eventi yayinlayacak.
/// </summary>
public class TestMapReadyTrigger : MonoBehaviour
{
    [SerializeField] private int _seed = 42;
    [SerializeField] private int _roomCount = 3;
    [SerializeField] private bool _publishOnStart = false;

    [Tooltip("PublishOnStart aktifse, NGO'nun OnNetworkSpawn cagrilmasini beklemek icin gecikme.")]
    [SerializeField] private float _publishOnStartDelay = 1.5f;

    private bool _published;

    private void Start()
    {
        // Race condition: NGO'nun scene-placed NetworkObject'lerinde OnNetworkSpawn
        // genelde Start'tan sonra cagrilir. Bu yuzden EnemySpawner.Subscribe
        // hazir olmadan Publish yaparsak event havada kalir. Kucuk bir gecikme cozer.
        if (_publishOnStart)
            Invoke(nameof(Publish), _publishOnStartDelay);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // T = MapReadyEvent yayinla
        if (kb.tKey.wasPressedThisFrame)
            Publish();
    }

    [ContextMenu("Publish MapReadyEvent")]
    public void Publish()
    {
        if (_published)
        {
            Debug.Log("[TestMapReadyTrigger] Zaten yayinlandi, atlandi.");
            return;
        }

        GameEventBus.Publish(new MapReadyEvent(_seed, _roomCount));
        _published = true;
        Debug.Log($"[TestMapReadyTrigger] MapReadyEvent yayinlandi (seed={_seed}, roomCount={_roomCount}).");
    }
}
