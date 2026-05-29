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

        // Loading Ekranýni cagiriyoruz
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadSceneNetwork("RNGMap");
        }
        else
        {
            // GÜVENLÝK: Eðer test yaparken LoadingManager'ý sahneye koymayý unutursanýz
            // oyun çökmesin, eski usül takým arkadaþýnýn yazdýðý sistemden devam etsin.
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
