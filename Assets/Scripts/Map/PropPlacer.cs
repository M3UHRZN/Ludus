using System.Collections.Generic;
using UnityEngine;

// Saf seçim mantığı: hangi anchor'a hangi palette girdisi gelecek?
// GameObject'e dokunmaz → EditMode'da prefab olmadan test edilebilir.
public static class PropPlacer
{
    // Dungeon seed + oda koordinatından deterministik tohum.
    public static int RoomSeed(int dungeonSeed, Vector2Int coords)
    {
        unchecked
        {
            int h = dungeonSeed;
            h = (h * 397) ^ coords.x;
            h = (h * 397) ^ coords.y;
            return h;
        }
    }

    public static List<PropPlacement> SelectPlacements(
        IReadOnlyList<PropCategory> anchorCategories,
        IReadOnlyList<DecorPropEntry> entries,
        int minProps,
        int maxProps,
        System.Random rng)
    {
        var result = new List<PropPlacement>();
        if (anchorCategories == null || anchorCategories.Count == 0) return result;
        if (entries == null || entries.Count == 0) return result;

        int min = Mathf.Max(0, minProps);
        int max = Mathf.Max(min, maxProps);
        int target = rng.Next(min, max + 1);
        target = Mathf.Min(target, anchorCategories.Count);
        if (target <= 0) return result;

        // Anchor index'lerini deterministik karıştır (Fisher-Yates).
        var order = new List<int>(anchorCategories.Count);
        for (int i = 0; i < anchorCategories.Count; i++) order.Add(i);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        int filled = 0;
        for (int k = 0; k < order.Count && filled < target; k++)
        {
            int anchorIdx = order[k];
            PropCategory cat = anchorCategories[anchorIdx];

            int entryIdx = PickWeightedEntry(entries, cat, rng);
            if (entryIdx < 0) continue; // bu kategoriye uygun girdi yok → anchor boş kalır

            var entry = entries[entryIdx];
            float yaw = entry.randomYaw ? (float)(rng.NextDouble() * 360.0) : 0f;
            float scale = SampleScale(entry.scaleJitter, rng);

            result.Add(new PropPlacement
            {
                AnchorIndex = anchorIdx,
                EntryIndex = entryIdx,
                Yaw = yaw,
                Scale = scale
            });
            filled++;
        }
        return result;
    }

    private static int PickWeightedEntry(IReadOnlyList<DecorPropEntry> entries, PropCategory cat, System.Random rng)
    {
        float total = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e != null && e.weight > 0f && e.SupportsCategory(cat)) total += e.weight;
        }
        if (total <= 0f) return -1;

        double r = rng.NextDouble() * total;
        double acc = 0.0;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.weight <= 0f || !e.SupportsCategory(cat)) continue;
            acc += e.weight;
            if (r < acc) return i;
        }
        // Kayan nokta artığı için son uyumlu girdiye düş.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e != null && e.weight > 0f && e.SupportsCategory(cat)) return i;
        }
        return -1;
    }

    private static float SampleScale(Vector2 jitter, System.Random rng)
    {
        float lo = jitter.x <= 0f ? 1f : jitter.x;
        float hi = jitter.y <= 0f ? 1f : jitter.y;
        if (hi < lo) (lo, hi) = (hi, lo);
        if (Mathf.Approximately(lo, hi)) return lo;
        return lo + (float)(rng.NextDouble() * (hi - lo));
    }
}
