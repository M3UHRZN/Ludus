using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// TEST YARDIMCISI. Dungeon hazir olunca (MapReadyEvent) "Player" tag'li objeyi
/// NavMesh uzerindeki en yakin gecerli noktaya snap eder. Boylece prosedurel
/// harita degisse de oyuncu duvar icinde / harita disinda kalmaz.
///
/// NOT: Bu gecici bir test cozumudur. Kalici cozum Metin'in PlayerSpawnCoordinator
/// sisteminde olmali (player dungeon'un ilk odasina / gecerli spawn noktasina
/// dogmali). Bu script sadece AlpTest_WithMap sahnesinde enemy/mermi testini
/// engellememek icin var. Sahnedeki herhangi bir objeye (orn. DungeonManager
/// veya TestController) eklenir.
/// </summary>
public class PlayerSpawnSafetySnap : MonoBehaviour
{
    [Tooltip("NavMesh uzerinde gecerli nokta aramak icin yaricap")]
    [SerializeField] private float _searchRadius = 40f;

    [Tooltip("Snap sonrasi oyuncuyu zeminden bu kadar yukari koy")]
    [SerializeField] private float _heightOffset = 0.5f;

    private void OnEnable()  => GameEventBus.Subscribe<MapReadyEvent>(OnMapReady);
    private void OnDisable() => GameEventBus.Unsubscribe<MapReadyEvent>(OnMapReady);

    private void OnMapReady(MapReadyEvent evt)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning("[PlayerSpawnSafetySnap] 'Player' tag'li obje bulunamadi.");
            return;
        }

        if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit hit, _searchRadius, NavMesh.AllAreas))
        {
            // CharacterController acikken transform set edilirse cakisir; once kapat
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = hit.position + Vector3.up * _heightOffset;

            if (cc != null) cc.enabled = true;

            Debug.Log($"[PlayerSpawnSafetySnap] Player NavMesh'e snap edildi: {hit.position}");
        }
        else
        {
            Debug.LogWarning("[PlayerSpawnSafetySnap] Yakinda gecerli NavMesh noktasi bulunamadi.");
        }
    }
}
