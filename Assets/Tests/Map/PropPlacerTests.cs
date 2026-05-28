using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class PropPlacerTests
{
    // ---- helpers ----
    private static DecorPropEntry Entry(float weight, bool randomYaw, Vector2 jitter, params PropCategory[] cats)
    {
        return new DecorPropEntry
        {
            prefab = null,           // seçim mantığı prefab'a dokunmaz
            categories = cats,
            weight = weight,
            randomYaw = randomYaw,
            scaleJitter = jitter
        };
    }

    private static List<PropCategory> Anchors(int count, PropCategory cat)
    {
        var list = new List<PropCategory>(count);
        for (int i = 0; i < count; i++) list.Add(cat);
        return list;
    }

    // ---- RoomSeed ----
    [Test]
    public void RoomSeed_SameInputs_SameValue()
    {
        Assert.AreEqual(
            PropPlacer.RoomSeed(42, new Vector2Int(3, -2)),
            PropPlacer.RoomSeed(42, new Vector2Int(3, -2)));
    }

    [Test]
    public void RoomSeed_DifferentCoords_DifferentValue()
    {
        Assert.AreNotEqual(
            PropPlacer.RoomSeed(42, new Vector2Int(0, 0)),
            PropPlacer.RoomSeed(42, new Vector2Int(1, 0)));
    }

    // ---- SelectPlacements ----
    [Test]
    public void SelectPlacements_SameSeed_IdenticalResult()
    {
        var anchors = Anchors(6, PropCategory.Floor);
        var entries = new List<DecorPropEntry> {
            Entry(1f, true, new Vector2(0.9f, 1.1f), PropCategory.Floor),
            Entry(2f, true, new Vector2(0.9f, 1.1f), PropCategory.Floor),
        };

        var a = PropPlacer.SelectPlacements(anchors, entries, 2, 4, new System.Random(123));
        var b = PropPlacer.SelectPlacements(anchors, entries, 2, 4, new System.Random(123));

        Assert.AreEqual(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.AreEqual(a[i].AnchorIndex, b[i].AnchorIndex);
            Assert.AreEqual(a[i].EntryIndex, b[i].EntryIndex);
            Assert.AreEqual(a[i].Yaw, b[i].Yaw, 1e-6f);
            Assert.AreEqual(a[i].Scale, b[i].Scale, 1e-6f);
        }
    }

    [Test]
    public void SelectPlacements_RespectsMaxAndAnchorCount()
    {
        var anchors = Anchors(10, PropCategory.Floor);
        var entries = new List<DecorPropEntry> { Entry(1f, false, Vector2.one, PropCategory.Floor) };

        var result = PropPlacer.SelectPlacements(anchors, entries, 0, 3, new System.Random(1));

        Assert.LessOrEqual(result.Count, 3);
        Assert.LessOrEqual(result.Count, anchors.Count);
    }

    [Test]
    public void SelectPlacements_MinEqualsMax_FillsExactlyThat_WhenEnoughAnchors()
    {
        var anchors = Anchors(5, PropCategory.Floor);
        var entries = new List<DecorPropEntry> { Entry(1f, false, Vector2.one, PropCategory.Floor) };

        var result = PropPlacer.SelectPlacements(anchors, entries, 3, 3, new System.Random(7));

        Assert.AreEqual(3, result.Count);
    }

    [Test]
    public void SelectPlacements_FewerAnchorsThanTarget_FillsAllAnchors()
    {
        var anchors = Anchors(2, PropCategory.Floor);
        var entries = new List<DecorPropEntry> { Entry(1f, false, Vector2.one, PropCategory.Floor) };

        var result = PropPlacer.SelectPlacements(anchors, entries, 5, 5, new System.Random(7));

        Assert.AreEqual(2, result.Count);
    }

    [Test]
    public void SelectPlacements_CategoryFilter_OnlyCompatibleEntryChosen()
    {
        var anchors = Anchors(4, PropCategory.Wall);
        var entries = new List<DecorPropEntry> {
            Entry(1f, false, Vector2.one, PropCategory.Floor), // index 0 — uyumsuz
            Entry(1f, false, Vector2.one, PropCategory.Wall),  // index 1 — uyumlu
        };

        var result = PropPlacer.SelectPlacements(anchors, entries, 4, 4, new System.Random(5));

        Assert.AreEqual(4, result.Count);
        foreach (var p in result)
            Assert.AreEqual(1, p.EntryIndex, "Wall anchor sadece Wall girdisini almalı");
    }

    [Test]
    public void SelectPlacements_NoCompatibleEntry_SkipsAnchors_NoException()
    {
        var anchors = Anchors(3, PropCategory.Ceiling);
        var entries = new List<DecorPropEntry> { Entry(1f, false, Vector2.one, PropCategory.Floor) };

        var result = PropPlacer.SelectPlacements(anchors, entries, 3, 3, new System.Random(5));

        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void SelectPlacements_EmptyPalette_ReturnsEmpty()
    {
        var anchors = Anchors(3, PropCategory.Floor);
        var result = PropPlacer.SelectPlacements(anchors, new List<DecorPropEntry>(), 1, 3, new System.Random(5));
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void SelectPlacements_EmptyAnchors_ReturnsEmpty()
    {
        var entries = new List<DecorPropEntry> { Entry(1f, false, Vector2.one, PropCategory.Floor) };
        var result = PropPlacer.SelectPlacements(new List<PropCategory>(), entries, 1, 3, new System.Random(5));
        Assert.AreEqual(0, result.Count);
    }

    [Test]
    public void SelectPlacements_NoDuplicateAnchorIndices()
    {
        var anchors = Anchors(6, PropCategory.Floor);
        var entries = new List<DecorPropEntry> { Entry(1f, false, Vector2.one, PropCategory.Floor) };

        var result = PropPlacer.SelectPlacements(anchors, entries, 4, 4, new System.Random(99));

        var seen = new HashSet<int>();
        foreach (var p in result)
            Assert.IsTrue(seen.Add(p.AnchorIndex), "Aynı anchor iki kez doldurulmamalı");
    }

    [Test]
    public void SelectPlacements_ScaleWithinJitterRange()
    {
        var anchors = Anchors(5, PropCategory.Floor);
        var entries = new List<DecorPropEntry> { Entry(1f, false, new Vector2(0.8f, 1.2f), PropCategory.Floor) };

        var result = PropPlacer.SelectPlacements(anchors, entries, 5, 5, new System.Random(3));

        foreach (var p in result)
        {
            Assert.GreaterOrEqual(p.Scale, 0.8f - 1e-4f);
            Assert.LessOrEqual(p.Scale, 1.2f + 1e-4f);
        }
    }

    // ---- Golden-value / determinism pin tests ----

    [Test]
    public void RoomSeed_KnownValue_IsStable()
    {
        Assert.AreEqual(-6619181, PropPlacer.RoomSeed(42, new Vector2Int(3, -2)));
    }

    [Test]
    public void SelectPlacements_KnownSeed_PinnedSequence()
    {
        // 6 Floor anchors; entry[0] weight=1, entry[1] weight=2; min=2 max=4; seed=123.
        // Golden values captured after Fix 1 (double accumulator) was applied.
        var anchors = Anchors(6, PropCategory.Floor);
        var entries = new List<DecorPropEntry>
        {
            Entry(1f, false, Vector2.one, PropCategory.Floor),  // index 0
            Entry(2f, false, Vector2.one, PropCategory.Floor),  // index 1
        };

        var result = PropPlacer.SelectPlacements(anchors, entries, 2, 4, new System.Random(123));

        Assert.AreEqual(4, result.Count);

        Assert.AreEqual(1, result[0].AnchorIndex);
        Assert.AreEqual(0, result[0].EntryIndex);
        Assert.AreEqual(0f, result[0].Yaw,   1e-6f);
        Assert.AreEqual(1f, result[0].Scale, 1e-6f);

        Assert.AreEqual(0, result[1].AnchorIndex);
        Assert.AreEqual(0, result[1].EntryIndex);
        Assert.AreEqual(0f, result[1].Yaw,   1e-6f);
        Assert.AreEqual(1f, result[1].Scale, 1e-6f);

        Assert.AreEqual(2, result[2].AnchorIndex);
        Assert.AreEqual(0, result[2].EntryIndex);
        Assert.AreEqual(0f, result[2].Yaw,   1e-6f);
        Assert.AreEqual(1f, result[2].Scale, 1e-6f);

        Assert.AreEqual(4, result[3].AnchorIndex);
        Assert.AreEqual(1, result[3].EntryIndex);
        Assert.AreEqual(0f, result[3].Yaw,   1e-6f);
        Assert.AreEqual(1f, result[3].Scale, 1e-6f);
    }
}
