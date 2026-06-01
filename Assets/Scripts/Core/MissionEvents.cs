using Ludus.Extraction.Core;

/// <summary>
/// Run sonunda ExtractionService (Rpc→Everyone) yayinlar. MissionHUD bunu dinleyip
/// mevcut mission/kota/run sayacini gosterir; Result panel feedback'i icin kullanilabilir.
/// </summary>
public struct MissionStateEvent
{
    public int Mission;        // 1-tabanli
    public int Quota;          // mevcut mission kotasi
    public int RunsInMission;  // 0..RunsPerMission
    public int RunsPerMission; // bir mission'daki toplam run (HUD paydasi)
    public int BankCredits;    // yayin anindaki banka bakiyesi
    public MissionResult Result; // Continue | Cleared | Failed
}
