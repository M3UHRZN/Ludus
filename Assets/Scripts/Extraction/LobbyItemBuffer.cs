using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lobideki loose (held olmayan) BaseItem'ları run -> lobi geçişinde KONUMUYLA taşıyan
/// oturum-boyu static tampon. LobbyRoomManager.StartRun yazar (item'ı yakalar + despawn eder);
/// LobbyLootDispenser lobide boşaltıp item'ı tam bıraktığı yere geri spawn'lar.
/// (ExtractedItemReturnBuffer ile aynı desen; fark: konum+rotasyon taşır.)
/// </summary>
public static class LobbyItemBuffer
{
    public struct Entry
    {
        public ushort id;
        public Vector3 pos;
        public Quaternion rot;
    }

    private static readonly List<Entry> _items = new();

    /// <summary>Geri döndürülecek bir item'ı konumuyla ekle (0 = geçersiz, atlanır).</summary>
    public static void Add(ushort id, Vector3 pos, Quaternion rot)
    {
        if (id != 0) _items.Add(new Entry { id = id, pos = pos, rot = rot });
    }

    /// <summary>Birikmiş kayıtları verir ve tamponu temizler.</summary>
    public static List<Entry> Drain()
    {
        var copy = new List<Entry>(_items);
        _items.Clear();
        return copy;
    }

    /// <summary>Mission başarısızlığında / test: tamponu sıfırla.</summary>
    public static void Clear() => _items.Clear();
}
