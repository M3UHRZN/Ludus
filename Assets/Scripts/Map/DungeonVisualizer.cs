using UnityEngine;

[RequireComponent(typeof(DungeonGeneratorRunner))]
public class DungeonVisualizer : MonoBehaviour
{
    [Header("Prefablar")]
    [SerializeField] private GameObject _floorPrefab;
    [SerializeField] private GameObject _wallNorthPrefab;
    [SerializeField] private GameObject _wallSouthPrefab;
    [SerializeField] private GameObject _wallEastPrefab;
    [SerializeField] private GameObject _wallWestPrefab;
    [SerializeField] private GameObject _doorNorthPrefab;
    [SerializeField] private GameObject _doorSouthPrefab;
    [SerializeField] private GameObject _doorEastPrefab;
    [SerializeField] private GameObject _doorWestPrefab;
    [SerializeField] private GameObject _corridorFloorPrefab;

    [Header("Boyutlar (dünya birimi)")]
    [Tooltip("Oda prefabının XZ boyutu")]
    [SerializeField] private float _roomSize = 6f;
    [Tooltip("Odalar arası koridor uzunluğu")]
    [SerializeField] private float _corridorLength = 4f;
    // Oda merkezleri arası mesafe
    private float Stride => _roomSize + _corridorLength;

    private Transform _root;

    public void Visualize(DungeonData data)
    {
        if (_root != null) Destroy(_root.gameObject);
        _root = new GameObject("DungeonLayout").transform;
        _root.SetParent(transform);

        foreach (var room in data.AllRooms)
            SpawnRoom(room);
    }

    private void SpawnRoom(RoomNode room)
    {
        float stride = Stride;
        Vector3 worldPos = new Vector3(
            room.Coordinates.x * stride, 0,
            room.Coordinates.y * stride);

        var parent = new GameObject(
            $"Room_{room.Coordinates.x}_{room.Coordinates.y}").transform;
        parent.SetParent(_root);
        parent.position = worldPos;

        if (_floorPrefab != null)
            Instantiate(_floorPrefab, worldPos, Quaternion.identity, parent);

        SpawnSide(room, ConnectionDirection.North, worldPos, parent);
        SpawnSide(room, ConnectionDirection.South, worldPos, parent);
        SpawnSide(room, ConnectionDirection.East,  worldPos, parent);
        SpawnSide(room, ConnectionDirection.West,  worldPos, parent);
    }

    private void SpawnSide(RoomNode room, ConnectionDirection dir,
                           Vector3 center, Transform parent)
    {
        bool hasDoor = room.HasConnection(dir);
        GameObject wallPrefab = (dir, hasDoor) switch
        {
            (ConnectionDirection.North, true)  => _doorNorthPrefab,
            (ConnectionDirection.North, false) => _wallNorthPrefab,
            (ConnectionDirection.South, true)  => _doorSouthPrefab,
            (ConnectionDirection.South, false) => _wallSouthPrefab,
            (ConnectionDirection.East,  true)  => _doorEastPrefab,
            (ConnectionDirection.East,  false) => _wallEastPrefab,
            (ConnectionDirection.West,  true)  => _doorWestPrefab,
            (ConnectionDirection.West,  false) => _wallWestPrefab,
            _                                  => null
        };

        Vector2Int dir2d = DirectionHelper.ToVector(dir);
        Vector3 wallOffset = new Vector3(dir2d.x, 0, dir2d.y) * (_roomSize * 0.5f);

        Quaternion rot = dir switch
        {
            ConnectionDirection.East  => Quaternion.Euler(0,  90, 0),
            ConnectionDirection.West  => Quaternion.Euler(0, -90, 0),
            ConnectionDirection.South => Quaternion.Euler(0, 180, 0),
            _                         => Quaternion.identity
        };

        if (wallPrefab != null)
            Instantiate(wallPrefab, center + wallOffset, rot, parent);

        // Koridor: her iki oda da spawn etmesin diye sadece East/North tarafı spawn eder
        if (!hasDoor) return;
        if (dir != ConnectionDirection.East && dir != ConnectionDirection.North) return;

        // _corridorFloorPrefab atanmamışsa _floorPrefab ile fallback — koridor her zaman çizilir
        GameObject corridorPrefab = _corridorFloorPrefab != null
            ? _corridorFloorPrefab
            : _floorPrefab;
        if (corridorPrefab == null) return;

        Vector3 corridorCenter = center +
            new Vector3(dir2d.x, 0, dir2d.y) *
            (_roomSize * 0.5f + _corridorLength * 0.5f);

        Instantiate(corridorPrefab, corridorCenter, rot, parent);
    }
}
