using System;

namespace Ludus.Extraction.Core
{
    /// <summary>Mission/quota ayarlari — saf, agdan bagimsiz (EditMode testli).</summary>
    public struct MissionConfig
    {
        public int BaseQuota;      // Mission 1 kotasi
        public float GrowthRate;   // 0.30 = her mission +%30
        public int RunsPerMission; // 3 run = 1 mission

        public static MissionConfig Default => new MissionConfig
        {
            BaseQuota = 300,
            GrowthRate = 0.30f,
            RunsPerMission = 3
        };
    }

    /// <summary>quota(mission) = round(BaseQuota * (1+GrowthRate)^(mission-1)). Mission 1-tabanli.</summary>
    public static class MissionQuota
    {
        public static int For(int missionIndex, MissionConfig config)
        {
            if (missionIndex < 1) missionIndex = 1;
            double q = config.BaseQuota * Math.Pow(1.0 + config.GrowthRate, missionIndex - 1);
            return (int)System.Math.Round(q, System.MidpointRounding.AwayFromZero);
        }
    }
}
