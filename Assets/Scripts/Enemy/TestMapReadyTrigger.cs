using UnityEngine;

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
    [SerializeField] private KeyCode _triggerKey = KeyCode.T;

    private bool _published;

    private void Start()
    {
        if (_publishOnStart)
            Publish();
    }

    private void Update()
    {
        if (Input.GetKeyDown(_triggerKey))
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
