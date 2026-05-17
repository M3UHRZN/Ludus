// Assets/Scripts/Items/ItemSpawnerTester.cs
using UnityEngine;

public class ItemSpawnerTester : MonoBehaviour
{
    [SerializeField] private ItemSpawner _spawner;
    private BaseItem _lastSpawned;

    private void Update()
    {
        // T → spawn
        if (Input.GetKeyDown(KeyCode.T))
            _lastSpawned = _spawner.Spawn(Vector3.zero, Quaternion.identity);

        // Y → despawn
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (_lastSpawned != null)
                _spawner.Despawn(_lastSpawned);
        }
    }
}