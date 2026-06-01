using NUnit.Framework;
using Ludus.Extraction.Core;

public class MissionStateTests
{
    private static readonly MissionConfig Cfg = MissionConfig.Default;

    [SetUp]
    public void ResetStaticState() => MissionState.Reset();

    [Test]
    public void EnsureInitialized_StartsAtMission1()
    {
        MissionState.EnsureInitialized(Cfg);
        Assert.IsTrue(MissionState.HasValue);
        Assert.AreEqual(1, MissionState.Snapshot.Mission);
        Assert.AreEqual(0, MissionState.Snapshot.RunsInMission);
        Assert.AreEqual(300, MissionState.Snapshot.Quota);
    }

    [Test]
    public void ThreeRuns_QuotaMet_AdvancesAndDoesNotFlagReset()
    {
        var r1 = MissionState.RegisterRunCompleted(0, Cfg);
        var r2 = MissionState.RegisterRunCompleted(0, Cfg);
        var r3 = MissionState.RegisterRunCompleted(400, Cfg);   // bank >= 300

        Assert.AreEqual(MissionResult.Continue, r1);
        Assert.AreEqual(MissionResult.Continue, r2);
        Assert.AreEqual(MissionResult.Cleared, r3);
        Assert.AreEqual(2, MissionState.Snapshot.Mission);
        Assert.AreEqual(390, MissionState.Snapshot.Quota);
        Assert.IsFalse(MissionState.PendingFullReset);
    }

    [Test]
    public void ThreeRuns_QuotaMissed_ResetsAndFlagsFullReset()
    {
        MissionState.RegisterRunCompleted(0, Cfg);
        MissionState.RegisterRunCompleted(0, Cfg);
        var r3 = MissionState.RegisterRunCompleted(100, Cfg);   // bank < 300

        Assert.AreEqual(MissionResult.Failed, r3);
        Assert.AreEqual(1, MissionState.Snapshot.Mission);
        Assert.AreEqual(0, MissionState.Snapshot.RunsInMission);
        Assert.AreEqual(300, MissionState.Snapshot.Quota);
        Assert.IsTrue(MissionState.PendingFullReset);
    }
}
