using UnityEngine;

/// <summary>
/// Oturum boyu yasayan kredi kasasi — market parasinin TEK kaynagi (server-side truth).
///
/// MarketWallet, Lobby sahnesine gomulu (scene-baked) oldugu icin run'a girince sahneyle
/// birlikte yok olur ve geri donuste taze dogar. Para wallet'ta tutulsaydi her donuste
/// startingCredits'e resetlenirdi (yasanan bug buydu). Bunun yerine gercek bakiye burada
/// durur: wallet acilista buradan OKUR, her degisimde buraya YAZAR.
///
/// Kalicilik: oturum boyu (static). Oyun tamamen kapatilinca sifirlanir; disk persisti yok.
/// (Editor'de "Enter Play Mode > Reload Domain" kapaliysa play oturumlari arasi da kalabilir —
///  test sirasinda taze 100 istersen MarketDebugTools > Reset Credits kullan.)
/// </summary>
public static class MarketCreditBank
{
    private static int _credits;
    private static bool _initialized;

    public static bool HasValue => _initialized;
    public static int Credits => _credits;

    /// <summary>Kasa hic kurulmadiysa baslangic kredisiyle kur; zaten kuruluysa dokunma.</summary>
    public static void EnsureInitialized(int startingCredits)
    {
        if (_initialized)
            return;

        _credits = Mathf.Max(0, startingCredits);
        _initialized = true;
    }

    /// <summary>Kasayi mutlak degere ayarla (wallet harcama/satis sonrasi senkron icin).</summary>
    public static void Set(int amount)
    {
        _credits = Mathf.Max(0, amount);
        _initialized = true;
    }

    /// <summary>
    /// Run'da kazanilan net krediyi kasaya ekler.
    /// Yeni extraction sistemi run bitince bunu cagiracak — su an cagiran YOK (API hazir).
    /// </summary>
    public static void AddRunEarnings(int amount)
    {
        if (amount <= 0)
            return;

        Set(_credits + amount);
    }

    /// <summary>Test/debug: kasayi tamamen sifirla (sonraki wallet startingCredits ile kurar).</summary>
    public static void Reset()
    {
        _credits = 0;
        _initialized = false;
    }
}
