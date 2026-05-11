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
}
