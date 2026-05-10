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
}
