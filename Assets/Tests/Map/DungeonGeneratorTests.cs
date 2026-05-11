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
}
