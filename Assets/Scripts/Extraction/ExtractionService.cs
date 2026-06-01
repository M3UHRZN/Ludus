using System.Collections;
using Ludus.Extraction.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Extraction'ın TEK otoritesi. Lever / timer / all-dead tetikleri tek FinalizeRun yoluna akar.
/// Gross LootSellZone'dan gelir; ceza GDD §6.4; net MarketCreditBank'a yazılır; sonuç RunResultEvent
/// ile yayınlanır; ardından lobi yüklenir. (Eski ExtractionManager + ExitZoneInteractable.PerformExtraction
/// muhasebesinin yeniden yazımı.)
/// </summary>
public class ExtractionService : NetworkBehaviour
{
    public static ExtractionService Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "LobbyScene";

    [Header("Akış")]
    [Tooltip("Sonuç paneli gösterildikten sonra lobiye yükleme gecikmesi (sn).")]
    [SerializeField] private float lobbyLoadDelay = 5f;

    [Header("Mission / Kota")]
    [Tooltip("Mission 1 kotasi (kredi).")]
    [SerializeField] private int baseQuota = 300;
    [Tooltip("Her mission kotasi bu oranda artar (0.30 = +%30).")]
    [SerializeField] private float quotaGrowthRate = 0.30f;
    [Tooltip("Bir mission kac run'dan olusur.")]
    [SerializeField] private int runsPerMission = 3;

    private Ludus.Extraction.Core.MissionConfig MissionConfig => new Ludus.Extraction.Core.MissionConfig
    {
        BaseQuota = baseQuota,
        GrowthRate = quotaGrowthRate,
        RunsPerMission = runsPerMission
    };

    private bool _runEnding;

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetForNewRun()
    {
        if (!IsServer) return;
        _runEnding = false;
    }

    // ── Tetikler ──────────────────────────────────────────────────────────────

    /// <summary>Lever çekilince (herhangi bir client).</summary>
    [Rpc(SendTo.Server)]
    public void RequestTeamExtractionServerRpc()
    {
        FinalizeRun(SessionEndReason.Escaped);
    }

    /// <summary>Timer dolunca GameSessionManager (server) çağırır.</summary>
    public void ForceExtraction()
    {
        FinalizeRun(SessionEndReason.TimeUp);
    }

    /// <summary>Tüm takım ölünce GameSessionManager (server) çağırır → wipe.</summary>
    public void TriggerWipe()
    {
        FinalizeRun(SessionEndReason.AllDead);
    }

    // ── Tek sonlandırma yolu ────────────────────────────────────────────────────

    private void FinalizeRun(SessionEndReason reason)
    {
        if (!IsServer) return;
        if (_runEnding) return;
        _runEnding = true;

        bool isWipe = reason == SessionEndReason.AllDead;

        // 1) Gross — wipe ise 0, değilse loot zone içeriği.
        int grossRaw = (!isWipe && LootSellZone.Instance != null)
            ? LootSellZone.Instance.ComputeGross()
            : 0;

        // 2) Rescue / abandon say.
        int rescuedAlive = 0, rescuedCorpses = 0, abandoned = 0;
        var zone = ExtractionZone.Instance;
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            foreach (var kvp in nm.ConnectedClients)
            {
                var po = kvp.Value.PlayerObject;
                if (po == null) continue;
                var machine = po.GetComponent<PlayerStateMachine>();
                if (machine == null) continue;

                bool alive = machine.IsAlive;
                bool inZone = zone != null && zone.ContainsPlayer(machine);

                if (alive && inZone)
                {
                    rescuedAlive++;
                }
                else if (!alive)
                {
                    var corpse = zone != null ? zone.FindCorpseForClient(kvp.Key) : null;
                    if (corpse != null)
                    {
                        rescuedCorpses++;
                        var cno = corpse.GetComponent<NetworkObject>();
                        if (cno != null && cno.IsSpawned) cno.Despawn(true);
                    }
                    else
                    {
                        abandoned++;
                    }
                }
                else // alive && !inZone
                {
                    abandoned++;
                }
            }
        }

        int playerCount = GameSessionManager.Instance != null
            ? GameSessionManager.Instance.PlayerCount
            : (nm != null ? nm.ConnectedClients.Count : 1);

        // 3) Hesap.
        CreditBreakdown b = ExtractionMath.Compute(grossRaw, abandoned, playerCount, isWipe);

        // 4) Para → market kasası (bekletilen bağlantı).
        MarketCreditBank.AddRunEarnings(b.Net);

        // 4b) Mission/kota ilerleyisi — 3 run = 1 mission. Banka GUNCEL bakiyesiyle yargila.
        var missionResult = MissionState.RegisterRunCompleted(MarketCreditBank.Credits, MissionConfig);
        if (missionResult == Ludus.Extraction.Core.MissionResult.Failed)
        {
            // Kota dolmadi → para sifir (wallet sonraki lobi spawn'inda startingCredits ile kurar).
            // Dönecek loot iptali 5b tamponu DOLDUKTAN sonra yapilir (asagi bak).
            MarketCreditBank.Reset();

            // Oyuncu envanterlerini SIMDI (despawn'dan ONCE, run sahnesinde) bosalt. Boylece
            // PlayerInventory.OnNetworkDespawn bos snapshot kaydeder ve lobide bos restore edilir.
            // (Lobide temizlemek snapshot-restore ile yaris kosuluna girerdi.)
            foreach (var inv in FindObjectsByType<PlayerInventory>(FindObjectsSortMode.None))
            {
                if (inv != null) inv.ClearInventory();
            }
        }
        var missionSnapshot = MissionState.Snapshot;

        // 5) Satılan loot'u despawn et.
        if (!isWipe && LootSellZone.Instance != null)
            LootSellZone.Instance.ConsumeAndDespawn();

        // 5b) Satılmayan item'ları (zone içi, sell zone dışı — eldeki dahil) lobide geri döndürmek
        //     için ID'lerini tampona yaz ve orijinalleri despawn et (sahne-persist duplikasyonu önlenir).
        //     Sold item'lar yukarıda despawn olduğu için GetItemsInside'da null/atlanır.
        if (!isWipe && ExtractionZone.Instance != null)
        {
            foreach (var po in ExtractionZone.Instance.GetItemsInside())
            {
                if (po == null) continue;
                if (!po.TryGetComponent<BaseItem>(out var item)) continue;
                ExtractedItemReturnBuffer.Add(item.ItemId);
                var no = po.NetworkObject;
                if (no != null && no.IsSpawned) no.Despawn(true);
            }
        }

        // Kota dolmadiysa bu run'da toplanan hicbir sey lobide geri DONMEZ (her sey sifir).
        // Item'lar yukarida yine despawn edildi (sahne-persist duplikasyonu onlenir); burada
        // sadece tampon bosaltilir, boylece LobbyLootDispenser hicbir sey firlatmaz.
        if (missionResult == Ludus.Extraction.Core.MissionResult.Failed)
            ExtractedItemReturnBuffer.Clear();

        Debug.Log($"[ExtractionService] reason={reason} gross={b.Gross} penalty={b.Penalty} net={b.Net} " +
                  $"alive={rescuedAlive} corpses={rescuedCorpses} abandoned={abandoned} " +
                  $"mission={missionSnapshot.Mission} runs={missionSnapshot.RunsInMission} quota={missionSnapshot.Quota} missionResult={missionResult}");

        // 6) Sonucu herkese yayınla.
        BroadcastRunResultRpc(b.Gross, b.Penalty, b.Net, rescuedAlive, rescuedCorpses, abandoned, (byte)reason);
        BroadcastMissionStateRpc(missionSnapshot.Mission, missionSnapshot.Quota,
            missionSnapshot.RunsInMission, runsPerMission, MarketCreditBank.Credits, (byte)missionResult);

        // 7) Session'ı bitir.
        if (GameSessionManager.Instance != null)
            GameSessionManager.Instance.EndSession(reason);

        // 8) Panel gösterilirken gecikmeli lobi yükü.
        StartCoroutine(LoadLobbyAfterDelay());
    }

    [Rpc(SendTo.Everyone)]
    private void BroadcastRunResultRpc(int gross, int penalty, int net,
        int rescuedAlive, int rescuedCorpses, int abandoned, byte reason)
    {
        GameEventBus.Publish(new RunResultEvent
        {
            Gross = gross,
            Penalty = penalty,
            Net = net,
            RescuedAlive = rescuedAlive,
            RescuedCorpses = rescuedCorpses,
            Abandoned = abandoned,
            Reason = (SessionEndReason)reason
        });
    }

    [Rpc(SendTo.Everyone)]
    private void BroadcastMissionStateRpc(int mission, int quota, int runsInMission, int runsPerMission, int bankCredits, byte result)
    {
        GameEventBus.Publish(new MissionStateEvent
        {
            Mission = mission,
            Quota = quota,
            RunsInMission = runsInMission,
            RunsPerMission = runsPerMission,
            BankCredits = bankCredits,
            Result = (Ludus.Extraction.Core.MissionResult)result
        });
    }

    private IEnumerator LoadLobbyAfterDelay()
    {
        yield return new WaitForSeconds(lobbyLoadDelay);
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
    }
}
