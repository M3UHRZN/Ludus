using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class LobbyRoomManager : NetworkBehaviour
{
    public static LobbyRoomManager Instance { get; private set; }

    // Serialized UI references for the lobby room display
    [SerializeField] private Transform              playerListParent;
    [SerializeField] private GameObject             playerRowPrefab;
    [SerializeField] private TMPro.TMP_Text         startPromptText;

    private NetworkList<FixedString64Bytes> _playerNames;

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
        {
            NetworkManager.OnClientConnectedCallback    += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback  += OnClientDisconnected;
            RefreshPlayerList();
        }

        if (startPromptText != null)
            startPromptText.gameObject.SetActive(IsHost);

    }

    public override void OnNetworkDespawn()
    {
        _playerNames.OnListChanged -= OnPlayerListChanged;

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback    -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback  -= OnClientDisconnected;
        }

        _playerNames.Dispose();
    }

    public void StartRun()
    {
        if (!IsServer) return;
        NetworkManager.SceneManager.LoadScene(SceneNames.Game, LoadSceneMode.Single);
    }

    private void OnClientConnected(ulong clientId) => RefreshPlayerList();
    private void OnClientDisconnected(ulong clientId) => RefreshPlayerList();

    private void RefreshPlayerList()
    {
        if (!IsServer) return;
        _playerNames.Clear();
        foreach (var clientId in NetworkManager.ConnectedClientsIds)
            _playerNames.Add(new FixedString64Bytes($"Player-{clientId}"));
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
