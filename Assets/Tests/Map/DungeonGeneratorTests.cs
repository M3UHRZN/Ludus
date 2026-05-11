using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class DungeonGeneratorTests
{
    [Test]
    public void Assembly_IsSetUp()
    {
        Assert.Pass();
    }

    [Test]
    public void DirectionHelper_Opposite_North_ReturnsSouth()
    {
        Assert.AreEqual(ConnectionDirection.South, DirectionHelper.Opposite(ConnectionDirection.North));
    }

    [Test]
    public void DirectionHelper_Opposite_East_ReturnsWest()
    {
        Assert.AreEqual(ConnectionDirection.West, DirectionHelper.Opposite(ConnectionDirection.East));
    }

    [Test]
    public void DirectionHelper_ToVector_North_ReturnsVectorUp()
    {
        Assert.AreEqual(Vector2Int.up, DirectionHelper.ToVector(ConnectionDirection.North));
    }

    [Test]
    public void DirectionHelper_All_HasExactlyFourDirections()
    {
        Assert.AreEqual(4, DirectionHelper.All.Length);
    }

    [Test]
    public void RoomNode_NewNode_HasNoConnections()
    {
        var node = new RoomNode(Vector2Int.zero);
        Assert.AreEqual(ConnectionDirection.None, node.Connections);
    }

    [Test]
    public void RoomNode_AddConnection_SetsFlag()
    {
        var node = new RoomNode(Vector2Int.zero);
        node.AddConnection(ConnectionDirection.North);
        Assert.IsTrue(node.HasConnection(ConnectionDirection.North));
        Assert.IsFalse(node.HasConnection(ConnectionDirection.South));
    }

    [Test]
    public void RoomNode_AddMultiple_AllFlagsSet()
    {
        var node = new RoomNode(new Vector2Int(1, 2));
        node.AddConnection(ConnectionDirection.North);
        node.AddConnection(ConnectionDirection.East);
        Assert.IsTrue(node.HasConnection(ConnectionDirection.North));
        Assert.IsTrue(node.HasConnection(ConnectionDirection.East));
        Assert.IsFalse(node.HasConnection(ConnectionDirection.South));
    }

    [Test]
    public void RoomNode_RemoveConnection_ClearsFlag()
    {
        var node = new RoomNode(Vector2Int.zero);
        node.AddConnection(ConnectionDirection.West);
        node.RemoveConnection(ConnectionDirection.West);
        Assert.IsFalse(node.HasConnection(ConnectionDirection.West));
    }

    [Test]
    public void RoomNode_Coordinates_MatchConstructorInput()
    {
        var coords = new Vector2Int(3, -2);
        var node = new RoomNode(coords);
        Assert.AreEqual(coords, node.Coordinates);
    }

    [Test]
    public void RoomNode_DefaultType_IsStandard()
    {
        var node = new RoomNode(Vector2Int.zero);
        Assert.AreEqual(RoomType.Standard, node.Type);
    }

    [Test]
    public void DungeonData_AddRoom_IncreasesCount()
    {
        var data = new DungeonData();
        data.AddRoom(new RoomNode(Vector2Int.zero, RoomType.Start));
        Assert.AreEqual(1, data.RoomCount);
    }

    [Test]
    public void DungeonData_TryGetRoom_ReturnsTrue_WhenExists()
    {
        var data = new DungeonData();
        var coords = new Vector2Int(2, 3);
        data.AddRoom(new RoomNode(coords));
        Assert.IsTrue(data.TryGetRoom(coords, out _));
    }

    [Test]
    public void DungeonData_TryGetRoom_ReturnsFalse_WhenMissing()
    {
        var data = new DungeonData();
        Assert.IsFalse(data.TryGetRoom(new Vector2Int(99, 99), out _));
    }

    [Test]
    public void DungeonData_IsOccupied_ReturnsTrue_WhenRoomExists()
    {
        var data = new DungeonData();
        var coords = new Vector2Int(1, 1);
        data.AddRoom(new RoomNode(coords));
        Assert.IsTrue(data.IsOccupied(coords));
    }

    [Test]
    public void DungeonData_GetFreeNeighbors_ReturnsAll4_WhenEmpty()
    {
        var data = new DungeonData();
        data.AddRoom(new RoomNode(Vector2Int.zero));
        var neighbors = data.GetFreeNeighbors(Vector2Int.zero);
        Assert.AreEqual(4, neighbors.Count);
    }

    [Test]
    public void DungeonData_GetFreeNeighbors_ExcludesOccupied()
    {
        var data = new DungeonData();
        data.AddRoom(new RoomNode(Vector2Int.zero));
        data.AddRoom(new RoomNode(Vector2Int.up));   // North taken
        var neighbors = data.GetFreeNeighbors(Vector2Int.zero);
        Assert.AreEqual(3, neighbors.Count);
        CollectionAssert.DoesNotContain(neighbors, Vector2Int.up);
    }

    [Test]
    public void DungeonData_GetOccupiedNeighbors_EmptyGrid_ReturnsNone()
    {
        var data = new DungeonData();
        data.AddRoom(new RoomNode(Vector2Int.zero));
        var occupied = data.GetOccupiedNeighbors(Vector2Int.zero);
        Assert.AreEqual(0, occupied.Count);
    }

    [Test]
    public void DungeonData_GetOccupiedNeighbors_NorthNeighbor_ReturnedWithNorthDirection()
    {
        var data = new DungeonData();
        data.AddRoom(new RoomNode(Vector2Int.zero));
        data.AddRoom(new RoomNode(Vector2Int.up)); // North neighbor
        var occupied = data.GetOccupiedNeighbors(Vector2Int.zero);
        Assert.AreEqual(1, occupied.Count);
        Assert.AreEqual(Vector2Int.up, occupied[0].coords);
        Assert.AreEqual(ConnectionDirection.North, occupied[0].direction);
    }

    [Test]
    public void DungeonData_GetOccupiedNeighbors_MultipleNeighbors_AllReturned()
    {
        var data = new DungeonData();
        data.AddRoom(new RoomNode(Vector2Int.zero));
        data.AddRoom(new RoomNode(Vector2Int.up));    // North
        data.AddRoom(new RoomNode(Vector2Int.right)); // East
        var occupied = data.GetOccupiedNeighbors(Vector2Int.zero);
        Assert.AreEqual(2, occupied.Count);
    }

    // Helper: creates a minimal config SO for testing
    private DungeonGeneratorSO MakeConfig(int maxRooms = 20, int seed = 42)
    {
        var cfg = ScriptableObject.CreateInstance<DungeonGeneratorSO>();
        cfg.maxRooms = maxRooms;
        cfg.extraConnectionChance = 0f;
        cfg.enableRoomMerging = false;
        cfg.useRandomSeed = false;
        cfg.seed = seed;
        return cfg;
    }

    [Test]
    public void DungeonGenerator_Generate_ProducesExactRoomCount()
    {
        var generator = new DungeonGenerator(MakeConfig(maxRooms: 20, seed: 42));
        var data = generator.Generate();
        Assert.AreEqual(20, data.RoomCount);
    }

    [Test]
    public void DungeonGenerator_Generate_StartRoomAtOrigin()
    {
        var generator = new DungeonGenerator(MakeConfig(maxRooms: 10, seed: 1));
        var data = generator.Generate();
        Assert.IsTrue(data.TryGetRoom(Vector2Int.zero, out var startRoom));
        Assert.AreEqual(RoomType.Start, startRoom.Type);
    }

    [Test]
    public void DungeonGenerator_Generate_AllRoomsReachableFromOrigin()
    {
        var generator = new DungeonGenerator(MakeConfig(maxRooms: 25, seed: 7));
        var data = generator.Generate();

        var visited = new System.Collections.Generic.HashSet<Vector2Int>();
        var queue = new System.Collections.Generic.Queue<Vector2Int>();
        queue.Enqueue(Vector2Int.zero);
        visited.Add(Vector2Int.zero);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!data.TryGetRoom(current, out var node)) continue;
            foreach (var dir in DirectionHelper.All)
            {
                if (!node.HasConnection(dir)) continue;
                var nb = current + DirectionHelper.ToVector(dir);
                if (visited.Add(nb)) queue.Enqueue(nb);
            }
        }

        Assert.AreEqual(data.RoomCount, visited.Count,
            "BFS from origin must reach every room — dungeon must be fully connected");
    }

    [Test]
    public void DungeonGenerator_Generate_AllConnectionsBidirectional()
    {
        var generator = new DungeonGenerator(MakeConfig(maxRooms: 15, seed: 99));
        var data = generator.Generate();

        foreach (var room in data.AllRooms)
        {
            foreach (var dir in DirectionHelper.All)
            {
                if (!room.HasConnection(dir)) continue;
                var nbCoords = room.Coordinates + DirectionHelper.ToVector(dir);
                Assert.IsTrue(data.TryGetRoom(nbCoords, out var nb),
                    $"Room at {room.Coordinates} points {dir} but neighbor {nbCoords} does not exist");
                Assert.IsTrue(nb.HasConnection(DirectionHelper.Opposite(dir)),
                    $"Room at {nbCoords} is missing reverse {DirectionHelper.Opposite(dir)} connection");
            }
        }
    }

    [Test]
    public void DungeonGenerator_ZeroLoopChance_IsExactSpanningTree()
    {
        // A spanning tree of N nodes has exactly N-1 undirected edges
        var generator = new DungeonGenerator(MakeConfig(maxRooms: 20, seed: 42));
        var data = generator.Generate();

        int totalConnections = 0;
        foreach (var room in data.AllRooms)
            foreach (var dir in DirectionHelper.All)
                if (room.HasConnection(dir)) totalConnections++;

        Assert.AreEqual(data.RoomCount - 1, totalConnections / 2,
            "No extra connections: dungeon must be a perfect spanning tree");
    }

    [Test]
    public void DungeonGenerator_FullLoopChance_HasMoreEdgesThanSpanningTree()
    {
        var cfg = MakeConfig(maxRooms: 20, seed: 42);
        cfg.extraConnectionChance = 1f;
        var generator = new DungeonGenerator(cfg);
        var data = generator.Generate();

        int totalConnections = 0;
        foreach (var room in data.AllRooms)
            foreach (var dir in DirectionHelper.All)
                if (room.HasConnection(dir)) totalConnections++;

        Assert.Greater(totalConnections / 2, data.RoomCount - 1,
            "With extraConnectionChance=1, every valid extra wall should become a door");
    }
}
