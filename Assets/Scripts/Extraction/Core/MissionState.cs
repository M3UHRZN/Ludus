namespace Ludus.Extraction.Core
{
    /// <summary>
    /// Oturum-boyu yasayan mission ilerleyisi — server-side truth (MarketCreditBank deseni).
    /// Run sahnesi her yuklenisinde sifirlanmaz; static oldugu icin oturum boyu korunur.
    /// Yalniz server yazar; client'lar durumu MissionStateEvent ile ogrenir.
    /// </summary>
    public static class MissionState
    {
        private static MissionSnapshot _snapshot;
        private static bool _initialized;

        public static bool HasValue => _initialized;
        public static MissionSnapshot Snapshot => _snapshot;

        /// <summary>Mission 1 / runs 0 / base quota ile kur; zaten kuruluysa dokunma.</summary>
        public static void EnsureInitialized(MissionConfig config)
        {
            if (_initialized) return;
            _snapshot = MissionSnapshot.Initial(config);
            _initialized = true;
        }

        /// <summary>
        /// Server-only: bir run bitince cagrilir. Mevcut snapshot'i ilerletir, sonucu doner.
        /// Failed sonucunda PendingFullReset bayragini set eder (lobide item/inventory temizligi icin).
        /// </summary>
        public static MissionResult RegisterRunCompleted(int bankCredits, MissionConfig config)
        {
            EnsureInitialized(config);
            MissionOutcome outcome = MissionEvaluator.RegisterRunCompleted(_snapshot, bankCredits, config);
            _snapshot = outcome.Next;
            if (outcome.Result == MissionResult.Failed)
                PendingFullReset = true;
            return outcome.Result;
        }

        /// <summary>Lobi yeniden yuklenince islenecek "tam reset" bayragi (loose item + inventory wipe).</summary>
        public static bool PendingFullReset { get; set; }

        /// <summary>Test/debug: tum mission durumunu sifirla.</summary>
        public static void Reset()
        {
            _initialized = false;
            PendingFullReset = false;
            _snapshot = default;
        }
    }
}
