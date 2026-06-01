using NUnit.Framework;
using Ludus.Extraction.Core;

public class MissionEvaluatorTests
{
    private static readonly MissionConfig Cfg = MissionConfig.Default; // base 300, rate 0.30, runs 3

    [Test]
    public void NonFinalRun_ContinuesSameMission()
    {
        var start = MissionSnapshot.Initial(Cfg);                 // M1, runs 0, quota 300
        var o = MissionEvaluator.RegisterRunCompleted(start, bankCredits: 0, Cfg);

        Assert.AreEqual(MissionResult.Continue, o.Result);
        Assert.AreEqual(1, o.Next.Mission);
        Assert.AreEqual(1, o.Next.RunsInMission);
        Assert.AreEqual(300, o.Next.Quota);
    }

    [Test]
    public void FinalRun_QuotaMet_AdvancesAndRaisesQuota()
    {
        var atThird = new MissionSnapshot { Mission = 1, RunsInMission = 2, Quota = 300 };
        var o = MissionEvaluator.RegisterRunCompleted(atThird, bankCredits: 350, Cfg);

        Assert.AreEqual(MissionResult.Cleared, o.Result);
        Assert.AreEqual(2, o.Next.Mission);
        Assert.AreEqual(0, o.Next.RunsInMission);
        Assert.AreEqual(390, o.Next.Quota);                       // 300 * 1.3
    }

    [Test]
    public void FinalRun_QuotaMissed_ResetsToMission1()
    {
        var atThird = new MissionSnapshot { Mission = 4, RunsInMission = 2, Quota = 659 };
        var o = MissionEvaluator.RegisterRunCompleted(atThird, bankCredits: 658, Cfg);

        Assert.AreEqual(MissionResult.Failed, o.Result);
        Assert.AreEqual(1, o.Next.Mission);
        Assert.AreEqual(0, o.Next.RunsInMission);
        Assert.AreEqual(300, o.Next.Quota);
    }

    [Test]
    public void FinalRun_ExactlyAtQuota_Clears()
    {
        var atThird = new MissionSnapshot { Mission = 1, RunsInMission = 2, Quota = 300 };
        var o = MissionEvaluator.RegisterRunCompleted(atThird, bankCredits: 300, Cfg);
        Assert.AreEqual(MissionResult.Cleared, o.Result);
    }

    [Test]
    public void FinalRun_StaleQuotaInSnapshot_IgnoredInFavorOfDerivedQuota()
    {
        // Snapshot carries a bogus Quota (999), but Mission 1's real quota is 300.
        // The evaluator must judge against the derived 300, not the stale 999.
        var atThird = new MissionSnapshot { Mission = 1, RunsInMission = 2, Quota = 999 };
        var o = MissionEvaluator.RegisterRunCompleted(atThird, bankCredits: 350, Cfg);
        Assert.AreEqual(MissionResult.Cleared, o.Result);
    }
}
