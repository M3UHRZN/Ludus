using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NetworkManager))]
public class PlayerSpawnCoordinator : MonoBehaviour
{
    public static PlayerSpawnCoordinator Instance { get; private set; }

    [Header("Player Prefabs")]
    [SerializeField] private GameObject lobbyPlayerPrefab;
    [SerializeField] private GameObject gameplayPlayerPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float lobbySpawnDelay = 0.5f;
    [SerializeField] private float gameplaySpawnDelay = 2f;
    [SerializeField] private float spawnHeightOffset = 0.1f;

    private readonly Dictionary<ulong, Coroutine> _pendingGameplaySpawns = new();

    private Coroutine _lobbySpawnPassCoroutine;
    private Coroutine _gameplaySpawnPassCoroutine;
    private Coroutine _pendingSceneReadyCoroutine;
    private NetworkManager _networkManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _networkManager = GetComponent<NetworkManager>();
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        if (_networkManager == null)
        {
            _networkManager = GetComponent<NetworkManager>();
        }

        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;

        if (_networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (_networkManager == null)
        {
            return;
        }

        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;

        if (_networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        CancelLobbySpawnPass();
        CancelAllPendingGameplaySpawns();
        CancelPendingSceneReadyCheck();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StartGameTransition()
    {
        if (!IsServerReady() || _networkManager.SceneManager == null)
        {
            Debug.LogWarning($"[{nameof(PlayerSpawnCoordinator)}] Ignored StartGameTransition because server is not ready.");
            return;
        }

        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Starting game transition.");
        CancelLobbySpawnPass();
        DespawnAllPlayerObjects();
        _networkManager.SceneManager.LoadScene(SceneNames.Game, LoadSceneMode.Single);
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServerReady())
        {
            return;
        }

        var activeSceneName = SceneManager.GetActiveScene().name;
        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Client connected: {clientId}, activeScene={activeSceneName}");
        if (activeSceneName == SceneNames.Lobby)
        {
            SpawnLobbyPlayer(clientId);
            return;
        }

        if (activeSceneName == SceneNames.Game)
        {
            QueueGameplaySpawnForClient(clientId);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        CancelPendingGameplaySpawn(clientId);
    }

    private void OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode, List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (!IsServerReady())
        {
            return;
        }

        if (sceneName == SceneNames.Lobby)
        {
            HandleLobbySceneBecameActive();
            return;
        }

        if (sceneName == SceneNames.Game)
        {
            HandleGameSceneBecameActive();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
    {
        if (!IsServerReady())
        {
            return;
        }

        if (scene.name == SceneNames.Lobby)
        {
            HandleLobbySceneBecameActive();
            return;
        }

        if (scene.name == SceneNames.Game)
        {
            HandleGameSceneBecameActive();
        }
    }

    private void HandleLobbySceneBecameActive()
    {
        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Lobby scene became active. serverReady={IsServerReady()}");
        CancelAllPendingGameplaySpawns();

        if (IsServerReady())
        {
            StartLobbySpawnPass();
            return;
        }

        WaitForServerReady(SceneNames.Lobby);
    }

    private void HandleGameSceneBecameActive()
    {
        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Game scene became active. serverReady={IsServerReady()}");
        if (IsServerReady())
        {
            StartGameplaySpawnPass();
            return;
        }

        WaitForServerReady(SceneNames.Game);
    }

    private void WaitForServerReady(string sceneName)
    {
        CancelPendingSceneReadyCheck();
        _pendingSceneReadyCoroutine = StartCoroutine(WaitForServerReadyCoroutine(sceneName));
    }

    private IEnumerator WaitForServerReadyCoroutine(string sceneName)
    {
        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Waiting for server ready in scene {sceneName}.");
        while (_networkManager != null && _networkManager.IsListening && !_networkManager.IsServer)
        {
            yield return null;
        }

        while (_networkManager != null && !_networkManager.IsListening)
        {
            yield return null;
        }

        _pendingSceneReadyCoroutine = null;

        if (!IsServerReady() || SceneManager.GetActiveScene().name != sceneName)
        {
            Debug.LogWarning($"[{nameof(PlayerSpawnCoordinator)}] Server-ready wait aborted for scene {sceneName}. activeScene={SceneManager.GetActiveScene().name} serverReady={IsServerReady()}");
            yield break;
        }

        if (sceneName == SceneNames.Lobby)
        {
            Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Server ready in lobby. Spawning lobby players.");
            StartLobbySpawnPass();
            yield break;
        }

        if (sceneName == SceneNames.Game)
        {
            Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Server ready in game. Starting gameplay spawn pass.");
            StartGameplaySpawnPass();
        }
    }

    private void SpawnLobbyPlayersForAllConnectedClients()
    {
        var clientIds = GetSortedConnectedClientIds();
        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Spawning lobby players for {clientIds.Count} connected clients.");
        for (var i = 0; i < clientIds.Count; i++)
        {
            SpawnPlayerForClient(clientIds[i], lobbyPlayerPrefab, GetLobbySpawnPose(clientIds[i], i));
        }
    }

    private void SpawnLobbyPlayer(ulong clientId)
    {
        var clientIds = GetSortedConnectedClientIds();
        var spawnIndex = clientIds.IndexOf(clientId);
        if (spawnIndex < 0)
        {
            spawnIndex = clientIds.Count;
        }

        SpawnPlayerForClient(clientId, lobbyPlayerPrefab, GetLobbySpawnPose(clientId, spawnIndex));
    }

    private void StartLobbySpawnPass()
    {
        CancelLobbySpawnPass();
        _lobbySpawnPassCoroutine = StartCoroutine(LobbySpawnPassCoroutine());
    }

    private IEnumerator LobbySpawnPassCoroutine()
    {
        if (lobbySpawnDelay > 0f)
        {
            yield return new WaitForSeconds(lobbySpawnDelay);
        }

        _lobbySpawnPassCoroutine = null;

        if (!IsServerReady() || SceneManager.GetActiveScene().name != SceneNames.Lobby)
        {
            yield break;
        }

        SpawnLobbyPlayersForAllConnectedClients();
    }

    private void StartGameplaySpawnPass()
    {
        CancelAllPendingGameplaySpawns();

        if (_gameplaySpawnPassCoroutine != null)
        {
            StopCoroutine(_gameplaySpawnPassCoroutine);
        }

        _gameplaySpawnPassCoroutine = StartCoroutine(GameplaySpawnPassCoroutine());
    }

    private IEnumerator GameplaySpawnPassCoroutine()
    {
        if (gameplaySpawnDelay > 0f)
        {
            yield return new WaitForSeconds(gameplaySpawnDelay);
        }

        _gameplaySpawnPassCoroutine = null;

        if (!IsServerReady() || SceneManager.GetActiveScene().name != SceneNames.Game)
        {
            yield break;
        }

        var clientIds = GetSortedConnectedClientIds();
        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Spawning gameplay players for {clientIds.Count} connected clients.");
        for (var i = 0; i < clientIds.Count; i++)
        {
            SpawnPlayerForClient(clientIds[i], gameplayPlayerPrefab, GetGameplaySpawnPose(clientIds[i], i));
        }
    }

    private void QueueGameplaySpawnForClient(ulong clientId)
    {
        CancelPendingGameplaySpawn(clientId);
        _pendingGameplaySpawns[clientId] = StartCoroutine(DelayedGameplaySpawnForClient(clientId));
    }

    private IEnumerator DelayedGameplaySpawnForClient(ulong clientId)
    {
        if (gameplaySpawnDelay > 0f)
        {
            yield return new WaitForSeconds(gameplaySpawnDelay);
        }

        _pendingGameplaySpawns.Remove(clientId);

        if (!IsServerReady() || SceneManager.GetActiveScene().name != SceneNames.Game)
        {
            yield break;
        }

        if (!_networkManager.ConnectedClients.ContainsKey(clientId))
        {
            yield break;
        }

        var clientIds = GetSortedConnectedClientIds();
        var spawnIndex = clientIds.IndexOf(clientId);
        if (spawnIndex < 0)
        {
            spawnIndex = clientIds.Count;
        }

        SpawnPlayerForClient(clientId, gameplayPlayerPrefab, GetGameplaySpawnPose(clientId, spawnIndex));
    }

    private void SpawnPlayerForClient(ulong clientId, GameObject prefab, SpawnPose spawnPose)
    {
        if (!IsServerReady())
        {
            return;
        }

        if (prefab == null)
        {
            Debug.LogWarning($"[{nameof(PlayerSpawnCoordinator)}] Player prefab is not assigned.");
            return;
        }

        if (!_networkManager.ConnectedClients.TryGetValue(clientId, out var client))
        {
            return;
        }

        if (client.PlayerObject != null)
        {
            Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Skip spawn for client {clientId}; player object already exists.");
            return;
        }

        var instance = Instantiate(prefab, spawnPose.Position, spawnPose.Rotation);
        if (!instance.TryGetComponent(out NetworkObject networkObject))
        {
            Debug.LogError($"[{nameof(PlayerSpawnCoordinator)}] Prefab '{prefab.name}' has no NetworkObject.", prefab);
            Destroy(instance);
            return;
        }

        Debug.Log($"[{nameof(PlayerSpawnCoordinator)}] Spawning '{prefab.name}' for client {clientId} at {spawnPose.Position}.");
        networkObject.SpawnAsPlayerObject(clientId, true);
    }

    private void DespawnAllPlayerObjects()
    {
        CancelAllPendingGameplaySpawns();

        foreach (var clientId in GetSortedConnectedClientIds())
        {
            if (!_networkManager.ConnectedClients.TryGetValue(clientId, out var client))
            {
                continue;
            }

            var playerObject = client.PlayerObject;
            if (playerObject == null)
            {
                continue;
            }

            if (playerObject.IsSpawned)
            {
                playerObject.Despawn(true);
            }
            else
            {
                Destroy(playerObject.gameObject);
            }
        }
    }

    private SpawnPose GetLobbySpawnPose(ulong clientId, int spawnIndex)
    {
        var points = FindSpawnPoints<LobbySpawnPoint>();
        return GetSpawnPose(points, spawnIndex);
    }

    private SpawnPose GetGameplaySpawnPose(ulong clientId, int spawnIndex)
    {
        var points = FindSpawnPoints<GameplaySpawnPoint>();
        return GetSpawnPose(points, spawnIndex);
    }

    private SpawnPose GetSpawnPose<T>(T[] points, int spawnIndex) where T : MonoBehaviour
    {
        if (points != null && points.Length > 0)
        {
            var baseIndex = spawnIndex % points.Length;
            var overflowIndex = spawnIndex / points.Length;
            var point = points[baseIndex].transform;
            var position = point.position + Vector3.up * spawnHeightOffset + GetOverflowOffset(overflowIndex);
            return new SpawnPose(position, point.rotation);
        }

        var fallbackPosition = new Vector3(spawnIndex * 1.5f, spawnHeightOffset, 0f);
        Debug.LogWarning($"[{nameof(PlayerSpawnCoordinator)}] No spawn points of type {typeof(T).Name} found in scene '{SceneManager.GetActiveScene().name}'. Using fallback positions.");
        return new SpawnPose(fallbackPosition, Quaternion.identity);
    }

    private static Vector3 GetOverflowOffset(int overflowIndex)
    {
        if (overflowIndex <= 0)
        {
            return Vector3.zero;
        }

        var ring = overflowIndex;
        return new Vector3(ring * 1.5f, 0f, ring * 1.5f);
    }

    private static T[] FindSpawnPoints<T>() where T : MonoBehaviour
    {
        var allPoints = FindObjectsOfType<T>(true);
        var activeScene = SceneManager.GetActiveScene();
        var pointsInActiveScene = new List<T>();

        foreach (var point in allPoints)
        {
            if (point == null || point.gameObject.scene != activeScene || !point.gameObject.activeInHierarchy)
            {
                continue;
            }

            pointsInActiveScene.Add(point);
        }

        pointsInActiveScene.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return pointsInActiveScene.ToArray();
    }

    private List<ulong> GetSortedConnectedClientIds()
    {
        var clientIds = new List<ulong>(_networkManager.ConnectedClientsIds);
        clientIds.Sort();
        return clientIds;
    }

    private void CancelLobbySpawnPass()
    {
        if (_lobbySpawnPassCoroutine != null)
        {
            StopCoroutine(_lobbySpawnPassCoroutine);
            _lobbySpawnPassCoroutine = null;
        }
    }

    private void CancelPendingGameplaySpawn(ulong clientId)
    {
        if (!_pendingGameplaySpawns.TryGetValue(clientId, out var coroutine))
        {
            return;
        }

        if (coroutine != null)
        {
            StopCoroutine(coroutine);
        }

        _pendingGameplaySpawns.Remove(clientId);
    }

    private void CancelAllPendingGameplaySpawns()
    {
        if (_gameplaySpawnPassCoroutine != null)
        {
            StopCoroutine(_gameplaySpawnPassCoroutine);
            _gameplaySpawnPassCoroutine = null;
        }

        foreach (var pendingSpawn in _pendingGameplaySpawns.Values)
        {
            if (pendingSpawn != null)
            {
                StopCoroutine(pendingSpawn);
            }
        }

        _pendingGameplaySpawns.Clear();
    }

    private void CancelPendingSceneReadyCheck()
    {
        if (_pendingSceneReadyCoroutine == null)
        {
            return;
        }

        StopCoroutine(_pendingSceneReadyCoroutine);
        _pendingSceneReadyCoroutine = null;
    }

    private bool IsServerReady()
    {
        return _networkManager != null && _networkManager.IsListening && _networkManager.IsServer;
    }

    private readonly struct SpawnPose
    {
        public SpawnPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }
}
