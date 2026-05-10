using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkObject))]
public class LobbyRoomManager : NetworkBehaviour
{
    // Serialized UI references for the lobby room display
    [SerializeField] private Transform playerListParent;     // parent for player name rows
    [SerializeField] private GameObject playerRowPrefab;     // prefab with a TMP_Text
    [SerializeField] private TMPro.TMP_Text startPromptText; // "Press E to start" shown only to host

    // Synced player name list (server writes, everyone reads)
    private NetworkList<FixedString64Bytes> _playerNames;

    private void Awake()
    {
        _playerNames = new NetworkList<FixedString64Bytes>(
            new List<FixedString64Bytes>(),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
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

        // Show start prompt only to host
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

    // Called by ElevatorInteractable (Task 4)
    public void StartRun()
    {
        if (!IsServer) return;
        NetworkManager.SceneManager.LoadScene("SampleScene", LoadSceneMode.Single);
    }

    // ── Server-side player list management ────────────────────────────────

    private void OnClientConnected(ulong clientId) => RefreshPlayerList();
    private void OnClientDisconnected(ulong clientId) => RefreshPlayerList();

    private void RefreshPlayerList()
    {
        if (!IsServer) return;
        _playerNames.Clear();
        foreach (var clientId in NetworkManager.ConnectedClientsIds)
            _playerNames.Add(new FixedString64Bytes($"Player-{clientId}"));
    }

    // ── Client-side rendering ─────────────────────────────────────────────

    private void OnPlayerListChanged(NetworkListEvent<FixedString64Bytes> changeEvent)
        => RenderPlayerList();

    private void RenderPlayerList()
    {
        if (playerListParent == null) return;

        // Clear old rows
        foreach (Transform child in playerListParent)
            Destroy(child.gameObject);

        // Rebuild
        foreach (var name in _playerNames)
        {
            if (playerRowPrefab == null) break;
            var go = Instantiate(playerRowPrefab, playerListParent);
            var label = go.GetComponent<TMPro.TMP_Text>();
            if (label != null) label.text = name.ToString();
        }
    }
}
