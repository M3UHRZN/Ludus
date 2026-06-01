using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class LobbyRoomManager : NetworkBehaviour
{
    public static LobbyRoomManager Instance { get; private set; }

    // Serialized UI references for the lobby room display
    [SerializeField] private Transform              playerListParent;
    [SerializeField] private GameObject             playerRowPrefab;
    [SerializeField] private TMPro.TMP_Text         startPromptText;

    private NetworkList<FixedString64Bytes> _playerNames;
    private Dictionary<ulong, string>       _clientNames = new(); // server-only

    private void Awake()
    {
        Instance = this;
        _playerNames = new NetworkList<FixedString64Bytes>(
            new List<FixedString64Bytes>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public override void OnNetworkSpawn()
    {
        _playerNames.OnListChanged += OnPlayerListChanged;
        RenderPlayerList();

        if (IsServer)
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (startPromptText != null)
            startPromptText.gameObject.SetActive(IsHost);

        var cm = FindFirstObjectByType<ConnectionManager>();
        var name = (cm != null && !string.IsNullOrWhiteSpace(cm.DisplayName))
            ? cm.DisplayName
            : $"Player-{NetworkManager.LocalClientId}";
        RegisterNameServerRpc(name);

        // --- LOB�YE G�R�NCE SES �ALI�IR ---
        if (AudioManager.Instance != null && AudioManager.Instance.lobbyMusic != null)
        {
            AudioManager.Instance.PlayMusic(AudioManager.Instance.lobbyMusic);
        }
        // ---------------------------------------------------
    }

    public override void OnNetworkDespawn()
    {
        _playerNames.OnListChanged -= OnPlayerListChanged;

        if (IsServer)
        {
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            _clientNames.Clear();
        }

        _playerNames.Dispose();
    }

    //public void StartRun()
    //{
    //    if (!IsServer) return;
    //    if (PlayerSpawnCoordinator.Instance != null)
    //    {
    //        PlayerSpawnCoordinator.Instance.StartGameTransition();
    //        return;
    //    }

    //    Debug.LogWarning($"[{nameof(LobbyRoomManager)}] {nameof(PlayerSpawnCoordinator)} was not found.");
    //}

    public void StartRun()
    {
        if (!IsServer) return;

        // Lobideki loose item'ları konumuyla snapshot'la + despawn et (sahne kapanmadan ÖNCE).
        // Geri dönünce LobbyLootDispenser tam yerlerine geri basar. Aksi halde Spawn(true)'lar
        // sahne kapanınca yok oluyordu, Spawn()'lar ise run sahnesine sızıyordu.
        CaptureLooseLobbyItems();

        // Loading Ekran�ni cagiriyoruz
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadSceneNetwork("RNGMap");
        }
        else
        {
            // G�VENL�K: E�er test yaparken LoadingManager'� sahneye koymay� unutursan�z
            // oyun ��kmesin, eski us�l tak�m arkada��n�n yazd��� sistemden devam etsin.
            if (PlayerSpawnCoordinator.Instance != null)
            {
                PlayerSpawnCoordinator.Instance.StartGameTransition();
            }
            else
            {
                Debug.LogWarning($"[{nameof(LobbyRoomManager)}] {nameof(PlayerSpawnCoordinator)} was not found.");
            }
        }
    }

    // Lobideki loose (held olmayan) BaseItem'ları konumuyla LobbyItemBuffer'a alır ve despawn eder.
    // Yalnız lobi sahnesi yüklü olduğundan FindObjectsByType sadece lobi item'larını bulur.
    private static void CaptureLooseLobbyItems()
    {
        foreach (var item in FindObjectsByType<BaseItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (item == null) continue;
            var po = item.GetComponent<PhysicsObject>();
            if (po != null && po.IsHeld) continue; // elde tutulanı atla (transition'da nadir)

            var t = item.transform;
            LobbyItemBuffer.Add(item.ItemId, t.position, t.rotation);

            var no = item.GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn(true);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterNameServerRpc(string name, ServerRpcParams rpc = default)
    {
        _clientNames[rpc.Receive.SenderClientId] = string.IsNullOrWhiteSpace(name)
            ? $"Player-{rpc.Receive.SenderClientId}"
            : name;
        RefreshPlayerList();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _clientNames.Remove(clientId);
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        if (!IsServer) return;
        _playerNames.Clear();
        foreach (var clientId in NetworkManager.ConnectedClientsIds)
        {
            var n = _clientNames.TryGetValue(clientId, out var stored) ? stored : $"Player-{clientId}";
            _playerNames.Add(new FixedString64Bytes(n));
        }
    }

    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
        => RenderPlayerList();

    private void RenderPlayerList()
    {
        if (playerListParent == null) return;

        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        foreach (var name in _playerNames)
        {
            if (playerRowPrefab == null) break;
            var go = Instantiate(playerRowPrefab, playerListParent);
            var label = go.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null) label.text = name.ToString();
        }
    }
}
