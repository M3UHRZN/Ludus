using NUnit.Framework;
using Ludus.Extraction.Core;

public class ExtractionMathTests
{
    [Test]
    public void NoAbandon_NetEqualsGross()
    {
        var r = ExtractionMath.Compute(grossRaw: 100, abandonedCount: 0, playerCount: 2, isWipe: false);
        Assert.AreEqual(100, r.Gross);
        Assert.AreEqual(0, r.Penalty);
        Assert.AreEqual(100, r.Net);
    }

    [Test]
    public void OneAbandon_NetIsGrossMinusPenalty()
    {
        var r = ExtractionMath.Compute(grossRaw: 100, abandonedCount: 1, playerCount: 2, isWipe: false);
        Assert.AreEqual(100, r.Gross);
        Assert.AreEqual(25, r.Penalty);
        Assert.AreEqual(75, r.Net);
    }

    [Test]
    public void Wipe_ForcesZero()
    {
        var r = ExtractionMath.Compute(grossRaw: 500, abandonedCount: 0, playerCount: 4, isWipe: true);
        Assert.AreEqual(0, r.Gross);
        Assert.AreEqual(0, r.Penalty);
        Assert.AreEqual(0, r.Net);
    }

    [Test]
    public void NegativeGrossRaw_ClampedToZero()
    {
        var r = ExtractionMath.Compute(grossRaw: -50, abandonedCount: 0, playerCount: 2, isWipe: false);
        Assert.AreEqual(0, r.Gross);
        Assert.AreEqual(0, r.Net);
    }

    [Test]
    public void PenaltyCappedAtGross_NetNeverNegative()
    {
        var r = ExtractionMath.Compute(grossRaw: 100, abandonedCount: 5, playerCount: 2, isWipe: false);
        Assert.AreEqual(100, r.Penalty);
        Assert.AreEqual(0, r.Net);
    }
}
