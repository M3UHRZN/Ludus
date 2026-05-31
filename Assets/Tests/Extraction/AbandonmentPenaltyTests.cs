using NUnit.Framework;
using Ludus.Extraction.Core;

public class AbandonmentPenaltyTests
{
    [Test]
    public void NoAbandoned_NoPenalty()
    {
        Assert.AreEqual(0, AbandonmentPenalty.Calculate(gross: 100, abandonedCount: 0, playerCount: 2));
    }

    [Test]
    public void ZeroGross_NoPenalty()
    {
        Assert.AreEqual(0, AbandonmentPenalty.Calculate(gross: 0, abandonedCount: 3, playerCount: 2));
    }

    [Test]
    public void SmallTeam_UsesFloorOfQuarter()
    {
        // playerCount/100 = 0.02 < 0.25 -> floor 0.25; 100 * 0.25 * 1 = 25
        Assert.AreEqual(25, AbandonmentPenalty.Calculate(gross: 100, abandonedCount: 1, playerCount: 2));
    }

    [Test]
    public void LargeTeam_UsesPlayerCountFraction()
    {
        // playerCount/100 = 0.50 > 0.25; 100 * 0.50 * 1 = 50
        Assert.AreEqual(50, AbandonmentPenalty.Calculate(gross: 100, abandonedCount: 1, playerCount: 50));
    }

    [Test]
    public void ManyAbandoned_CapsAtGross()
    {
        // 100 * 0.25 * 5 = 125 -> capped to gross (100)
        Assert.AreEqual(100, AbandonmentPenalty.Calculate(gross: 100, abandonedCount: 5, playerCount: 2));
    }
}
