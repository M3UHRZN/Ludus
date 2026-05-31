using System.Collections.Generic;

/// <summary>
/// Extraction'da satilmayan (LootSellZone disi) item'larin ID'lerini run -> lobby gecisinde
/// tasiyan oturum-boyu static tampon. ExtractionService.FinalizeRun yazar; LobbyLootDispenser
/// lobide bosaltip secilen noktadan fiziksel firlatir. (MarketCreditBank ile ayni desen.)
/// </summary>
public static class ExtractedItemReturnBuffer
{
    private static readonly List<ushort> _ids = new();

    /// <summary>Geri dondurulecek bir item ID ekle (0 = bos slot, atlanir).</summary>
    public static void Add(ushort id)
    {
        if (id != 0) _ids.Add(id);
    }

    /// <summary>Birikmis ID'leri verir ve tamponu temizler.</summary>
    public static List<ushort> Drain()
    {
        var copy = new List<ushort>(_ids);
        _ids.Clear();
        return copy;
    }

    /// <summary>Test/debug: tamponu sifirla.</summary>
    public static void Clear() => _ids.Clear();
}
