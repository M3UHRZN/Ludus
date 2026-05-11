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
    public void RoomNode_DefaultMergeGroupId_IsMinusOne()
    {
        var node = new RoomNode(Vector2Int.zero);
        Assert.AreEqual(-1, node.MergeGroupId);
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

    [Test]
    public void ApplyMerging_2x2Square_MarksLargeRoom()
    {
        var data = new DungeonData();
        var bl = new RoomNode(new Vector2Int(0, 0));
        var br = new RoomNode(new Vector2Int(1, 0));
        var tl = new RoomNode(new Vector2Int(0, 1));
        var tr = new RoomNode(new Vector2Int(1, 1));
        bl.AddConnection(ConnectionDirection.East);  bl.AddConnection(ConnectionDirection.North);
        br.AddConnection(ConnectionDirection.West);  br.AddConnection(ConnectionDirection.North);
        tl.AddConnection(ConnectionDirection.East);  tl.AddConnection(ConnectionDirection.South);
        tr.AddConnection(ConnectionDirection.West);  tr.AddConnection(ConnectionDirection.South);
        data.AddRoom(bl); data.AddRoom(br); data.AddRoom(tl); data.AddRoom(tr);

        var cfg = MakeConfig(maxRooms: 4);
        cfg.enableRoomMerging = true;
        new DungeonGenerator(cfg).ApplyMerging(data);

        data.TryGetRoom(new Vector2Int(0, 0), out var merged);
        Assert.AreEqual(RoomSize.Large_2x2, merged.Size);
    }

    [Test]
    public void ApplyMerging_1x3HorizontalLine_MarksLongRoom()
    {
        var data = new DungeonData();
        var r0 = new RoomNode(new Vector2Int(0, 0));
        var r1 = new RoomNode(new Vector2Int(1, 0));
        var r2 = new RoomNode(new Vector2Int(2, 0));
        r0.AddConnection(ConnectionDirection.East);
        r1.AddConnection(ConnectionDirection.West); r1.AddConnection(ConnectionDirection.East);
        r2.AddConnection(ConnectionDirection.West);
        data.AddRoom(r0); data.AddRoom(r1); data.AddRoom(r2);

        var cfg = MakeConfig(maxRooms: 3);
        cfg.enableRoomMerging = true;
        new DungeonGenerator(cfg).ApplyMerging(data);

        data.TryGetRoom(new Vector2Int(0, 0), out var merged);
        Assert.AreEqual(RoomSize.Long_1x3, merged.Size);
    }

    [Test]
    public void ApplyMerging_3x1VerticalLine_MarksLongRoom()
    {
        var data = new DungeonData();
        var r0 = new RoomNode(new Vector2Int(0, 0));
        var r1 = new RoomNode(new Vector2Int(0, 1));
        var r2 = new RoomNode(new Vector2Int(0, 2));
        r0.AddConnection(ConnectionDirection.North);
        r1.AddConnection(ConnectionDirection.South); r1.AddConnection(ConnectionDirection.North);
        r2.AddConnection(ConnectionDirection.South);
        data.AddRoom(r0); data.AddRoom(r1); data.AddRoom(r2);

        var cfg = MakeConfig(maxRooms: 3);
        cfg.enableRoomMerging = true;
        new DungeonGenerator(cfg).ApplyMerging(data);

        data.TryGetRoom(new Vector2Int(0, 0), out var merged);
        Assert.AreEqual(RoomSize.Long_3x1, merged.Size);
    }

    [Test]
    public void Phase4_Merge2x2_AssignsSameGroupId_ToAllFourRooms()
    {
        var data = new DungeonData();
        var bl = new RoomNode(new Vector2Int(0, 0));
        var br = new RoomNode(new Vector2Int(1, 0));
        var tl = new RoomNode(new Vector2Int(0, 1));
        var tr = new RoomNode(new Vector2Int(1, 1));
        bl.AddConnection(ConnectionDirection.East);  bl.AddConnection(ConnectionDirection.North);
        br.AddConnection(ConnectionDirection.West);  br.AddConnection(ConnectionDirection.North);
        tl.AddConnection(ConnectionDirection.East);  tl.AddConnection(ConnectionDirection.South);
        tr.AddConnection(ConnectionDirection.West);  tr.AddConnection(ConnectionDirection.South);
        data.AddRoom(bl); data.AddRoom(br); data.AddRoom(tl); data.AddRoom(tr);

        var cfg = MakeConfig(maxRooms: 4);
        cfg.enableRoomMerging = true;
        new DungeonGenerator(cfg).ApplyMerging(data);

        data.TryGetRoom(new Vector2Int(0, 0), out var r00);
        data.TryGetRoom(new Vector2Int(1, 0), out var r10);
        data.TryGetRoom(new Vector2Int(0, 1), out var r01);
        data.TryGetRoom(new Vector2Int(1, 1), out var r11);

        Assert.AreNotEqual(-1, r00.MergeGroupId, "Origin should have a group ID");
        Assert.AreEqual(r00.MergeGroupId, r10.MergeGroupId);
        Assert.AreEqual(r00.MergeGroupId, r01.MergeGroupId);
        Assert.AreEqual(r00.MergeGroupId, r11.MergeGroupId);
    }

    [Test]
    public void Phase4_MergeLinear_1x3_AssignsSameGroupId()
    {
        var data = new DungeonData();
        var r0 = new RoomNode(new Vector2Int(0, 0));
        var r1 = new RoomNode(new Vector2Int(1, 0));
        var r2 = new RoomNode(new Vector2Int(2, 0));
        r0.AddConnection(ConnectionDirection.East);
        r1.AddConnection(ConnectionDirection.West); r1.AddConnection(ConnectionDirection.East);
        r2.AddConnection(ConnectionDirection.West);
        data.AddRoom(r0); data.AddRoom(r1); data.AddRoom(r2);

        var cfg = MakeConfig(maxRooms: 3);
        cfg.enableRoomMerging = true;
        new DungeonGenerator(cfg).ApplyMerging(data);

        data.TryGetRoom(new Vector2Int(0, 0), out var ra);
        data.TryGetRoom(new Vector2Int(1, 0), out var rb);
        data.TryGetRoom(new Vector2Int(2, 0), out var rc);

        Assert.AreNotEqual(-1, ra.MergeGroupId);
        Assert.AreEqual(ra.MergeGroupId, rb.MergeGroupId);
        Assert.AreEqual(ra.MergeGroupId, rc.MergeGroupId);
    }

    [Test]
    public void Phase4_TwoSeparate2x2Groups_HaveDifferentGroupIds()
    {
        var data = new DungeonData();
        var g1bl = new RoomNode(new Vector2Int(0, 0));
        var g1br = new RoomNode(new Vector2Int(1, 0));
        var g1tl = new RoomNode(new Vector2Int(0, 1));
        var g1tr = new RoomNode(new Vector2Int(1, 1));
        g1bl.AddConnection(ConnectionDirection.East); g1bl.AddConnection(ConnectionDirection.North);
        g1br.AddConnection(ConnectionDirection.West); g1br.AddConnection(ConnectionDirection.North);
        g1tl.AddConnection(ConnectionDirection.East); g1tl.AddConnection(ConnectionDirection.South);
        g1tr.AddConnection(ConnectionDirection.West); g1tr.AddConnection(ConnectionDirection.South);

        var g2bl = new RoomNode(new Vector2Int(3, 0));
        var g2br = new RoomNode(new Vector2Int(4, 0));
        var g2tl = new RoomNode(new Vector2Int(3, 1));
        var g2tr = new RoomNode(new Vector2Int(4, 1));
        g2bl.AddConnection(ConnectionDirection.East); g2bl.AddConnection(ConnectionDirection.North);
        g2br.AddConnection(ConnectionDirection.West); g2br.AddConnection(ConnectionDirection.North);
        g2tl.AddConnection(ConnectionDirection.East); g2tl.AddConnection(ConnectionDirection.South);
        g2tr.AddConnection(ConnectionDirection.West); g2tr.AddConnection(ConnectionDirection.South);

        data.AddRoom(g1bl); data.AddRoom(g1br); data.AddRoom(g1tl); data.AddRoom(g1tr);
        data.AddRoom(g2bl); data.AddRoom(g2br); data.AddRoom(g2tl); data.AddRoom(g2tr);

        var cfg = MakeConfig(maxRooms: 8);
        cfg.enableRoomMerging = true;
        new DungeonGenerator(cfg).ApplyMerging(data);

        data.TryGetRoom(new Vector2Int(0, 0), out var rg1);
        data.TryGetRoom(new Vector2Int(3, 0), out var rg2);

        Assert.AreNotEqual(-1, rg1.MergeGroupId);
        Assert.AreNotEqual(-1, rg2.MergeGroupId);
        Assert.AreNotEqual(rg1.MergeGroupId, rg2.MergeGroupId,
            "Two separate merge groups must have different IDs");
    }
}
