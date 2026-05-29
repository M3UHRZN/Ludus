using System.Collections.Generic;
using NUnit.Framework;
using Ludus.UsableItems.Core;

public class ItemCatalogLookupTests
{
    private sealed class FakeRecord : IItemRecord
    {
        public ushort Id { get; set; }
        public int Weight { get; set; }
        public bool IsBuyable { get; set; }
    }

    private static List<IItemRecord> Records(params IItemRecord[] r) => new List<IItemRecord>(r);

    [Test]
    public void IndexOf_ReturnsMinusOne_WhenMissing()
        => Assert.AreEqual(-1, ItemCatalogLookup.IndexOf(Records(new FakeRecord { Id = 1 }), 99));

    [Test]
    public void IndexOf_FindsFirstMatch()
        => Assert.AreEqual(1, ItemCatalogLookup.IndexOf(Records(new FakeRecord { Id = 5 }, new FakeRecord { Id = 7 }), 7));

    [Test]
    public void Contains_ReflectsPresence()
    {
        var records = Records(new FakeRecord { Id = 3 });
        Assert.IsTrue(ItemCatalogLookup.Contains(records, 3));
        Assert.IsFalse(ItemCatalogLookup.Contains(records, 4));
    }

    [Test]
    public void GetWeight_ReturnsWeight_OrZero()
    {
        var records = Records(new FakeRecord { Id = 2, Weight = 6 });
        Assert.AreEqual(6, ItemCatalogLookup.GetWeight(records, 2));
        Assert.AreEqual(0, ItemCatalogLookup.GetWeight(records, 8));
    }

    [Test]
    public void CollectBuyableIds_OnlyBuyables()
    {
        var records = Records(
            new FakeRecord { Id = 1, IsBuyable = true },
            new FakeRecord { Id = 2, IsBuyable = false },
            new FakeRecord { Id = 3, IsBuyable = true });
        var result = new List<ushort>();
        ItemCatalogLookup.CollectBuyableIds(records, result);
        CollectionAssert.AreEquivalent(new ushort[] { 1, 3 }, result);
    }

    [Test]
    public void NullInputs_AreSafe()
    {
        Assert.AreEqual(-1, ItemCatalogLookup.IndexOf(null, 1));
        Assert.AreEqual(0, ItemCatalogLookup.GetWeight(null, 1));
        Assert.IsFalse(ItemCatalogLookup.Contains(null, 1));
    }
}
