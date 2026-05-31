using UnityEngine;

namespace Ludus.Extraction.Core
{
    /// <summary>Run-sonu kredi kırılımı (gross / penalty / net). Saf, EditMode test edilir.</summary>
    public struct CreditBreakdown
    {
        public int Gross;
        public int Penalty;
        public int Net;
    }

    public static class ExtractionMath
    {
        /// <param name="grossRaw">LootSellZone içindeki item'ların ham toplam değeri.</param>
        /// <param name="isWipe">Tüm takım öldü → toplam kayıp (gross 0'a zorlanır).</param>
        public static CreditBreakdown Compute(int grossRaw, int abandonedCount, int playerCount, bool isWipe)
        {
            int gross = isWipe ? 0 : Mathf.Max(0, grossRaw);
            int penalty = AbandonmentPenalty.Calculate(gross, abandonedCount, playerCount);
            int net = Mathf.Max(0, gross - penalty);
            return new CreditBreakdown { Gross = gross, Penalty = penalty, Net = net };
        }
    }
}
