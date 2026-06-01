using Unity.Netcode;
using UnityEngine;
using Ludus.Extraction.Core;

/// <summary>
/// Lobi sahnesine gomulu (scene-baked), server-only. Mission BASARISIZ olunca
/// (MissionState.PendingFullReset) lobi yeniden yuklenince lobideki loose (held olmayan,
/// sahne-persist) item'lari despawn eder. Para reseti ExtractionService + MarketWallet'ta;
/// oyuncu envanteri ise ExtractionService.FinalizeRun'da (despawn'dan once, snapshot-yarisini
/// onlemek icin) temizlenir. Bu servis yalniz loose world item tarafini temizler.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class MissionResetService : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (!MissionState.PendingFullReset) return;

        WipeLooseItems();

        MissionState.PendingFullReset = false;
        Debug.Log("[MissionResetService] Mission basarisiz — lobideki loose item'lar temizlendi (envanter+para run sonunda sifirlandi).");
    }

    private static void WipeLooseItems()
    {
        // Sahne-persist (Spawn() => destroyWithScene=false) ile spawn edilmis item'lar lobiye tasinir; hepsini despawn et.
        // (Spawn(true) kullananlar zaten sahne yuklenince yok olur, burada gorunmezler.)
        var items = Object.FindObjectsByType<BaseItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var item in items)
        {
            if (item == null) continue;
            var no = item.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn(true);
            else Object.Destroy(item.gameObject);
        }
    }
}
