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

    [Header("Birleşik Oda Prefabları")]
    [Tooltip("2x2 oda için zemin prefabı (opsiyonel; atanmazsa _floorPrefab kullanılır)")]
    [SerializeField] private GameObject _mergedFloor2x2Prefab;
    [Tooltip("1x3 yatay oda için zemin prefabı")]
    [SerializeField] private GameObject _mergedFloorH1x3Prefab;
    [Tooltip("3x1 dikey oda için zemin prefabı")]
    [SerializeField] private GameObject _mergedFloorV3x1Prefab;

    [Header("Giriş Odası Prefabları")]
    [Tooltip("Giriş odası (Start) için özel zemin prefabı (opsiyonel)")]
    [SerializeField] private GameObject _entryFloorPrefab;
    [Tooltip("Giriş odası (Start) için özel duvar prefabı (opsiyonel)")]
    [SerializeField] private GameObject _entryWallPrefab;
    [Tooltip("Giriş odası (Start) için özel kapı prefabı (opsiyonel)")]
    [SerializeField] private GameObject _entryDoorPrefab;

    [Header("Boyutlar (dünya birimi)")]
    [Tooltip("Oda prefabının XZ boyutu")]
    [SerializeField] private float _roomSize = 6f;
    [Tooltip("Odalar arası koridor uzunluğu")]
    [SerializeField] private float _corridorLength = 4f;
    private float Stride => _roomSize + _corridorLength;

    private DungeonData _dungeonData;
    private Transform _root;

    public void Visualize(DungeonData data)
    {
        _dungeonData = data;
        if (_root != null)
        {
            if (Application.isPlaying)
                Destroy(_root.gameObject);
            else
                DestroyImmediate(_root.gameObject);
        }
        _root = new GameObject("DungeonLayout").transform;
        _root.SetParent(transform);
        foreach (var room in data.AllRooms)
            SpawnRoom(room, data);
    }

    private void SpawnRoom(RoomNode room, DungeonData data)
    {
        float stride = Stride;
        Vector3 worldPos = new Vector3(
            room.Coordinates.x * stride, 0,
            room.Coordinates.y * stride);

        // Birleşik odalar birden fazla parent GO'ya bölünür: origin hücre floor'u taşır,
        // diğer hücreler yalnızca dış duvarlarını taşır.
        var parent = new GameObject(RoomName(room)).transform;
        parent.SetParent(_root);
        parent.position = worldPos;

        SpawnFloor(room, data, parent);
        SpawnSide(room, ConnectionDirection.North, worldPos, parent, data);
        SpawnSide(room, ConnectionDirection.South, worldPos, parent, data);
        SpawnSide(room, ConnectionDirection.East,  worldPos, parent, data);
        SpawnSide(room, ConnectionDirection.West,  worldPos, parent, data);
    }

    private void SpawnFloor(RoomNode room, DungeonData data, Transform parent)
    {
        if (!IsGroupOrigin(room, data)) return;

        GameObject prefab;
        if (room.Type == RoomType.Start)
        {
            prefab = _entryFloorPrefab != null ? _entryFloorPrefab : _floorPrefab;
        }
        else
        {
            prefab = room.Size switch
            {
                RoomSize.Large_2x2 => _mergedFloor2x2Prefab  != null ? _mergedFloor2x2Prefab  : _floorPrefab,
                RoomSize.Long_1x3  => _mergedFloorH1x3Prefab != null ? _mergedFloorH1x3Prefab : _floorPrefab,
                RoomSize.Long_3x1  => _mergedFloorV3x1Prefab != null ? _mergedFloorV3x1Prefab : _floorPrefab,
                _                  => _floorPrefab
            };
        }

        if (prefab == null) return;
        Instantiate(prefab, GroupFloorCenter(room), Quaternion.identity, parent);
    }

    private void SpawnSide(RoomNode room, ConnectionDirection dir,
                           Vector3 center, Transform parent, DungeonData data)
    {
        if (IsInternalConnection(room, dir, data)) return;

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

        if (room.Type == RoomType.Start)
        {
            if (hasDoor && _entryDoorPrefab != null)
                wallPrefab = _entryDoorPrefab;
            else if (!hasDoor && _entryWallPrefab != null)
                wallPrefab = _entryWallPrefab;
        }

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

        // Koridor: sadece East/North tarafı spawn eder (her ikisi de spawn etmesin diye)
        if (!hasDoor) return;
        if (dir != ConnectionDirection.East && dir != ConnectionDirection.North) return;
        if (Mathf.Approximately(_corridorLength, 0f)) return;

        GameObject corridorPrefab = _corridorFloorPrefab != null ? _corridorFloorPrefab : _floorPrefab;
        if (corridorPrefab == null) return;

        Vector3 corridorCenter = center +
            new Vector3(dir2d.x, 0, dir2d.y) * (_roomSize * 0.5f + _corridorLength * 0.5f);

        Instantiate(corridorPrefab, corridorCenter, rot, parent);
    }

    // Grubun sol-alt hücresi: West ve South yönünde aynı gruptan komşu yok
    private bool IsGroupOrigin(RoomNode room, DungeonData data)
    {
        if (room.MergeGroupId == -1) return true;
        if (data.TryGetRoom(room.Coordinates + Vector2Int.left, out var west) &&
            west.MergeGroupId == room.MergeGroupId) return false;
        if (data.TryGetRoom(room.Coordinates + Vector2Int.down, out var south) &&
            south.MergeGroupId == room.MergeGroupId) return false;
        return true;
    }

    // Komşu aynı merge grubundaysa bu bağlantı iç duvardır — hiçbir şey spawn edilmez
    private bool IsInternalConnection(RoomNode room, ConnectionDirection dir, DungeonData data)
    {
        if (room.MergeGroupId == -1) return false;
        var nbCoords = room.Coordinates + DirectionHelper.ToVector(dir);
        return data.TryGetRoom(nbCoords, out var nb) && nb.MergeGroupId == room.MergeGroupId;
    }

    // Origin hücresinin world pozisyonundan grubun görsel merkezini hesaplar
    private Vector3 GroupFloorCenter(RoomNode origin)
    {
        float s = Stride;
        float bx = origin.Coordinates.x * s;
        float bz = origin.Coordinates.y * s;
        return origin.Size switch
        {
            RoomSize.Large_2x2 => new Vector3(bx + s * 0.5f, 0, bz + s * 0.5f),
            RoomSize.Long_1x3  => new Vector3(bx + s,        0, bz),
            RoomSize.Long_3x1  => new Vector3(bx,             0, bz + s),
            _                  => new Vector3(bx,             0, bz)
        };
    }

    private static string RoomName(RoomNode room) => room.Size switch
    {
        RoomSize.Large_2x2 => $"Room2x2_({room.Coordinates.x},{room.Coordinates.y})",
        RoomSize.Long_1x3  => $"RoomH1x3_({room.Coordinates.x},{room.Coordinates.y})",
        RoomSize.Long_3x1  => $"RoomV3x1_({room.Coordinates.x},{room.Coordinates.y})",
        _                  => $"Room1x1_({room.Coordinates.x},{room.Coordinates.y})"
    };

    private void OnDrawGizmos()
    {
        if (_dungeonData == null || _dungeonData.AllRooms == null) return;

        float stride = Stride;

        foreach (var room in _dungeonData.AllRooms)
        {
            Vector3 center = new Vector3(room.Coordinates.x * stride, 0f, room.Coordinates.y * stride);

            // 1. Draw grid / room boundary
            if (room.Type == RoomType.Start)
            {
                // Strong green color for Entry Point
                Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.4f);
                Gizmos.DrawCube(center, new Vector3(_roomSize, 0.2f, _roomSize));
                Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.9f);
                Gizmos.DrawWireCube(center, new Vector3(_roomSize, 0.2f, _roomSize));
            }
            else if (room.Type == RoomType.Boss)
            {
                // Purple/red color for Boss room
                Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.3f);
                Gizmos.DrawCube(center, new Vector3(_roomSize, 0.2f, _roomSize));
                Gizmos.color = new Color(0.8f, 0f, 0.8f, 0.9f);
                Gizmos.DrawWireCube(center, new Vector3(_roomSize, 0.2f, _roomSize));
            }
            else if (room.Type == RoomType.End)
            {
                // Orange/Red color for End room
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.3f);
                Gizmos.DrawCube(center, new Vector3(_roomSize, 0.2f, _roomSize));
                Gizmos.color = new Color(1f, 0.3f, 0f, 0.9f);
                Gizmos.DrawWireCube(center, new Vector3(_roomSize, 0.2f, _roomSize));
            }
            else
            {
                // Light cyan/blue for normal rooms
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
                Gizmos.DrawCube(center, new Vector3(_roomSize, 0.1f, _roomSize));
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.6f);
                Gizmos.DrawWireCube(center, new Vector3(_roomSize, 0.1f, _roomSize));
            }

            // 2. Draw connections (lines)
            Gizmos.color = new Color(1f, 0.9f, 0f, 0.8f); // Gold/yellow for paths
            foreach (var dir in DirectionHelper.All)
            {
                if (room.HasConnection(dir))
                {
                    Vector2Int dirVector = DirectionHelper.ToVector(dir);
                    Vector3 lineEnd = center + new Vector3(dirVector.x, 0f, dirVector.y) * (stride * 0.5f);
                    Gizmos.DrawLine(center, lineEnd);
                }
            }

#if UNITY_EDITOR
            // 3. Draw text label for Room Coordinates and Type
            string labelText = $"{room.Coordinates}\n[{room.Type}]";
            if (room.MergeGroupId != -1)
            {
                labelText += $"\nGroup: {room.MergeGroupId}";
            }
            UnityEditor.Handles.Label(center + Vector3.up * 0.5f, labelText);
#endif
        }
    }
}
