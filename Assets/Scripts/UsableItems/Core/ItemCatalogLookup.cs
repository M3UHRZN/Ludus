using System.Collections.Generic;

namespace Ludus.UsableItems.Core
{
    /// <summary>
    /// Item kayıtları üzerinde saf id-tabanlı aramalar. Unity/NGO tipi yok →
    /// tamamen EditMode test edilebilir. ItemCatalog (main asm) bunu sarmalar.
    /// </summary>
    public static class ItemCatalogLookup
    {
        public static int IndexOf(IReadOnlyList<IItemRecord> records, ushort id)
        {
            if (records == null) return -1;
            for (int i = 0; i < records.Count; i++)
            {
                IItemRecord r = records[i];
                if (r != null && r.Id == id) return i;
            }
            return -1;
        }

        public static bool Contains(IReadOnlyList<IItemRecord> records, ushort id)
            => IndexOf(records, id) >= 0;

        public static int GetWeight(IReadOnlyList<IItemRecord> records, ushort id)
        {
            int idx = IndexOf(records, id);
            return idx >= 0 ? records[idx].Weight : 0;
        }

        public static void CollectBuyableIds(IReadOnlyList<IItemRecord> records, List<ushort> result)
        {
            if (records == null || result == null) return;
            for (int i = 0; i < records.Count; i++)
            {
                IItemRecord r = records[i];
                if (r != null && r.IsBuyable) result.Add(r.Id);
            }
        }
    }
}
