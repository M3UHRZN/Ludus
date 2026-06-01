using NUnit.Framework;
using Ludus.Extraction.Core;

public class MissionQuotaTests
{
    private static readonly MissionConfig Cfg = MissionConfig.Default; // base 300, rate 0.30

    [Test] public void Mission1_EqualsBaseQuota()
        => Assert.AreEqual(300, MissionQuota.For(1, Cfg));

    [Test] public void Mission2_IsBaseTimesGrowth()
        => Assert.AreEqual(390, MissionQuota.For(2, Cfg));   // 300 * 1.3

    [Test] public void Mission3_CompoundsGrowth()
        => Assert.AreEqual(507, MissionQuota.For(3, Cfg));   // 300 * 1.3^2 = 507

    [Test] public void Mission4_CompoundsGrowth()
        => Assert.AreEqual(659, MissionQuota.For(4, Cfg));   // 300 * 1.3^3 = 659.1 -> 659

    [Test] public void MissionIndexBelowOne_ClampsToMission1()
        => Assert.AreEqual(300, MissionQuota.For(0, Cfg));
}
