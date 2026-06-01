namespace Ludus.Extraction.Core
{
    public enum MissionResult { Continue, Cleared, Failed }

    /// <summary>Mission ilerleyisinin anlik durumu (saf veri).</summary>
    public struct MissionSnapshot
    {
        public int Mission;        // 1-tabanli
        public int RunsInMission;  // 0..RunsPerMission
        public int Quota;          // mevcut mission'in kotasi

        public static MissionSnapshot Initial(MissionConfig config) => new MissionSnapshot
        {
            Mission = 1,
            RunsInMission = 0,
            Quota = MissionQuota.For(1, config)
        };
    }

    public struct MissionOutcome
    {
        public MissionSnapshot Next;
        public MissionResult Result;
    }

    /// <summary>Bir run bitince mission durumunu nasil ilerletecegini hesaplayan saf fonksiyon.</summary>
    public static class MissionEvaluator
    {
        public static MissionOutcome RegisterRunCompleted(
            MissionSnapshot current, int bankCredits, MissionConfig config)
        {
            int runs = current.RunsInMission + 1;

            // Henuz mission'in son run'i degil → devam.
            if (runs < config.RunsPerMission)
            {
                return new MissionOutcome
                {
                    Result = MissionResult.Continue,
                    Next = new MissionSnapshot
                    {
                        Mission = current.Mission,
                        RunsInMission = runs,
                        Quota = current.Quota
                    }
                };
            }

            // Son run bitti → kotayi MISSION'dan taze turet (snapshot'taki cache'e guvenme) ve yargila.
            int activeQuota = MissionQuota.For(current.Mission, config);
            if (bankCredits >= activeQuota)
            {
                int next = current.Mission + 1;
                return new MissionOutcome
                {
                    Result = MissionResult.Cleared,
                    Next = new MissionSnapshot
                    {
                        Mission = next,
                        RunsInMission = 0,
                        Quota = MissionQuota.For(next, config)
                    }
                };
            }

            // Kota dolmadi → Mission 1'e tam reset.
            return new MissionOutcome
            {
                Result = MissionResult.Failed,
                Next = MissionSnapshot.Initial(config)
            };
        }
    }
}
