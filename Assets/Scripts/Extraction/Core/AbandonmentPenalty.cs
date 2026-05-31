using UnityEngine;

namespace Ludus.Extraction.Core
{
    /// <summary>
    /// GDD §6.4 abandonment cezası — saf, ağdan bağımsız (EditMode test edilir).
    /// penaltyPerCorpse = max(0.25, playerCount/100); toplam = min(gross, gross * per * abandonedCount).
    /// </summary>
    public static class AbandonmentPenalty
    {
        public static int Calculate(int gross, int abandonedCount, int playerCount)
        {
            if (gross <= 0 || abandonedCount <= 0)
                return 0;

            float per = Mathf.Max(0.25f, playerCount / 100f);
            float deduction = Mathf.Min(gross, gross * per * abandonedCount);
            return Mathf.RoundToInt(deduction);
        }
    }
}
