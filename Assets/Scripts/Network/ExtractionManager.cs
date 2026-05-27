using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ExtractionManager : NetworkBehaviour
{
    public static ExtractionManager Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string lobbySceneName = "LobbyScene";
    public string LobbySceneName => lobbySceneName;


    // Server-only: bir oyuncu extraction'ı tetikledikten sonra tekrar tetiklemeyi engeller.
    private bool _runEnding;

    public readonly NetworkVariable<int> ExtractedItemCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<int> TotalCredits = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool HasExtractedItems => ExtractedItemCount.Value > 0;

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterExtractedItem(ushort itemId, int creditValue)
    {
        if (!IsServer) return;

        ExtractedItemCount.Value++;
        TotalCredits.Value += creditValue;

        Debug.Log($"[ExtractionManager] Çıkarıldı → ID:{itemId} | " +
                  $"+{creditValue} kredi | Toplam: {ExtractedItemCount.Value} eşya, {TotalCredits.Value} kredi");

        NotifyClientsRpc(itemId, creditValue, TotalCredits.Value);
    }

    [Rpc(SendTo.Everyone)]
    private void NotifyClientsRpc(ushort itemId, int creditValue, int totalCredits)
    {
        GameEventBus.Publish(new ItemExtractedEvent
        {
            ItemId       = itemId,
            CreditValue  = creditValue,
            TotalCredits = totalCredits
        });
    }

    public void ResetForNewRun()
    {
        if (!IsServer) return;
        ExtractedItemCount.Value = 0;
        TotalCredits.Value       = 0;
        _runEnding               = false;
    }

    // Bir oyuncu exit zone'da E'ye basınca çağrılır. Server tüm takımı doğrudan lobby'e döndürür.
    // (Sonuç ekranı sonradan lobby tarafında gösterilecek.)
    [Rpc(SendTo.Server)]
    public void RequestTeamExtractionRpc()
    {
        if (_runEnding) return;
        if (!HasExtractedItems) return;
        _runEnding = true;   // çift tetiklemeye karşı kilit; sahne reload'unda manager taze spawn olur

        if (ExitZoneInteractable.Instance != null)
        {
            ExitZoneInteractable.Instance.PerformExtractionOnServer();
        }
        else
        {
            if (GameSessionManager.Instance != null)
            {
                GameSessionManager.Instance.EndSessionWithPenalty();
            }
            NetworkManager.SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
    }
}