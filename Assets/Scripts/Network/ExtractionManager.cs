using Unity.Netcode;
using UnityEngine;

public class ExtractionManager : NetworkBehaviour
{
    public static ExtractionManager Instance { get; private set; }

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
    }
}