using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ludus.UsableItems.Core;

public class FlashbangMathTests
{
    [Test]
    public void SelectAffectedIndices_ReturnsOnlyTargetsInsideRadius()
    {
        var positions = new List<Vector3>
        {
            new Vector3(0f, 0f, 0f),   // 0m  -> in
            new Vector3(3f, 0f, 0f),   // 3m  -> in
            new Vector3(10f, 0f, 0f),  // 10m -> out
        };
        var result = new List<int>();

        FlashbangMath.SelectAffectedIndices(positions, new Vector3(0f, 0f, 0f), 5f, result);

        CollectionAssert.AreEquivalent(new[] { 0, 1 }, result);
    }

    [Test]
    public void SelectAffectedIndices_UsesEyeHeightOffsetConsistently()
    {
        // A target exactly at radius on the horizontal plane is outside once the
        // +1 eye-height offset is added (sqrt(5^2 + 1^2) > 5).
        var positions = new List<Vector3> { new Vector3(5f, 0f, 0f) };
        var result = new List<int>();

        FlashbangMath.SelectAffectedIndices(positions, new Vector3(0f, 0f, 0f), 5f, result);

        Assert.IsEmpty(result);
    }

    [Test]
    public void SelectAffectedIndices_ClearsResultListFirst()
    {
        var positions = new List<Vector3> { new Vector3(0f, 0f, 0f) };
        var result = new List<int> { 99 };

        FlashbangMath.SelectAffectedIndices(positions, Vector3.zero, 5f, result);

        Assert.AreEqual(new List<int> { 0 }, result);
    }

    [Test]
    public void IsThrowOriginValid_RejectsFarOrigin()
    {
        Assert.IsFalse(FlashbangMath.IsThrowOriginValid(
            playerPosition: Vector3.zero, origin: new Vector3(0f, 0f, 10f), maxDistance: 4f));
    }

    [Test]
    public void IsThrowOriginValid_AcceptsNearOrigin()
    {
        Assert.IsTrue(FlashbangMath.IsThrowOriginValid(
            playerPosition: Vector3.zero, origin: new Vector3(0f, 1.4f, 0.4f), maxDistance: 4f));
    }

    [Test]
    public void IsThrowOriginValid_RejectsNaN()
    {
        Assert.IsFalse(FlashbangMath.IsThrowOriginValid(
            Vector3.zero, new Vector3(float.NaN, 0f, 0f), 4f));
    }
}
