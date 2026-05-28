using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Multiplayer;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
    private static readonly TimeSpan NetworkStartTimeout = TimeSpan.FromSeconds(60);

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    public string DisplayName { get; private set; }
    public bool IsConnected  => _state == ConnectionState.Connected;
    public bool IsConnecting => _state == ConnectionState.Connecting;

    public event Action         OnConnected;
    public event Action<string> OnDisconnected;   // string = reason

    // ──────────────────────────────────────────────────────────────────────────
    // Private state
    // ──────────────────────────────────────────────────────────────────────────

    private ConnectionState _state = ConnectionState.Disconnected;
    private ISession        _session;
    private NetworkManager  _networkManager;
    private Task            _initializeTask;
    private Task            _inFlight;          // in-progress network operation guard
    private bool            _isQuitting;

    private enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        ConfigureMultiplayerNetworkStartTimeout();
        _networkManager = GetComponent<NetworkManager>();
        _networkManager.OnClientConnectedCallback    += OnClientConnectedCallback;
        _networkManager.OnClientDisconnectCallback   += OnClientDisconnectCallback;
        _networkManager.ConnectionApprovalCallback   += OnConnectionApproval;
        _initializeTask = UnityServices.InitializeAsync();
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback  -= OnClientConnectedCallback;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
            _networkManager.ConnectionApprovalCallback -= OnConnectionApproval;
        }

        if (!_isQuitting && _session != null)
        {
            _ = LeaveAsync();
        }
    }

    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // NetworkManager callbacks
    // ──────────────────────────────────────────────────────────────────────────

    private void OnClientConnectedCallback(ulong clientId)
    {
        Debug.Log($"[{nameof(ConnectionManager)}] OnClientConnectedCallback clientId={clientId} localClientId={_networkManager.LocalClientId} isServer={_networkManager.IsServer} isClient={_networkManager.IsClient}");
        if (_networkManager.LocalClientId == clientId)
        {
            Debug.Log($"Client-{clientId} is connected and can spawn {nameof(NetworkObject)}s.");
        }
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        Debug.LogWarning($"[{nameof(ConnectionManager)}] OnClientDisconnectCallback clientId={clientId} localClientId={_networkManager.LocalClientId} isServer={_networkManager.IsServer} isClient={_networkManager.IsClient} disconnectReason='{_networkManager.DisconnectReason}'");
        // If we are a pure client and the server disconnected us, raise the event.
        if (!_networkManager.IsServer && clientId == NetworkManager.ServerClientId)
        {
            OnDisconnected?.Invoke("Host disconnected");
        }
    }

    /// <summary>
    /// Called by NGO when ConnectionApproval is enabled on the NetworkManager.
    /// Approves all incoming connection requests. Add custom validation here
    /// (e.g. password check, max-player cap) if needed in the future.
    /// </summary>
    private void OnConnectionApproval(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        Debug.Log($"[{nameof(ConnectionManager)}] ConnectionApproval for client. Approved.");
        response.Approved = true;
        response.CreatePlayerObject = false; // PlayerSpawnCoordinator handles spawning
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public async methods
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Creates a new session and starts as host.</summary>
    public async Task HostAsync(string displayName, string sessionName, int maxPlayers = 4)
    {
        GuardInFlight();

        _inFlight = HostInternalAsync(displayName, sessionName, maxPlayers);
        await _inFlight;
    }

    /// <summary>
    /// Creates a new session with the given name, or joins it if one already exists (create-or-join semantics).
    /// The underlying SDK does not support a join-by-name-only API, so this method will create the session
    /// if no session with that name is found.
    /// </summary>
    public async Task CreateOrJoinByNameAsync(string displayName, string sessionName, int maxPlayers = 6)
    {
        GuardInFlight();

        _inFlight = CreateOrJoinByNameInternalAsync(displayName, sessionName, maxPlayers);
        await _inFlight;
    }

    /// <summary>Joins an existing session by its unique session ID.</summary>
    public async Task JoinBySessionIdAsync(string displayName, string sessionId)
    {
        GuardInFlight();

        _inFlight = JoinBySessionIdInternalAsync(displayName, sessionId);
        await _inFlight;
    }

    /// <summary>Queries open sessions. Callers may call StartPolling on the result.</summary>
    public async Task<QuerySessionsResults> QuerySessionsAsync(string displayName = null)
    {
        await _initializeTask;

        // Sign in with the provided display name so browse and join share the same profile,
        // avoiding a sign-out/re-sign-in cycle when the player subsequently joins a session.
        var profile = !string.IsNullOrWhiteSpace(displayName) ? displayName
                    : !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName
                    : "browser-anon";

        if (!AuthenticationService.Instance.IsSignedIn || AuthenticationService.Instance.Profile != profile)
            await SignInWithProfileAsync(profile);

        var options = new QuerySessionsOptions
        {
            FilterOptions = new System.Collections.Generic.List<FilterOption>
            {
                new FilterOption(FilterField.AvailableSlots, "0", FilterOperation.Greater)
            },
            SortOptions = new System.Collections.Generic.List<SortOption>
            {
                new SortOption(SortOrder.Descending, SortField.AvailableSlots)
            }
        };

        return await MultiplayerService.Instance.QuerySessionsAsync(options);
    }

    /// <summary>Leaves the current session and shuts down the network.</summary>
    public async Task LeaveAsync()
    {
        await CleanupExistingMultiplayerStateAsync();
        _state = ConnectionState.Disconnected;
        OnDisconnected?.Invoke("Left session");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Private implementation helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void GuardInFlight()
    {
        if (_inFlight is { IsCompleted: false })
            throw new InvalidOperationException(
                "A network operation is already in progress. Wait for it to complete before calling another.");
    }

    private async Task HostInternalAsync(string displayName, string sessionName, int maxPlayers)
    {
        var options = new SessionOptions
        {
            Name       = sessionName,
            MaxPlayers = maxPlayers
        }.WithRelayNetwork();

        await ConnectInternalAsync(displayName,
            async () => await MultiplayerService.Instance.CreateSessionAsync(options));

        if (_networkManager.IsHost)
        {
            _networkManager.SceneManager.LoadScene(
                SceneNames.Lobby,
                UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private async Task CreateOrJoinByNameInternalAsync(string displayName, string sessionName, int maxPlayers = 4)
    {
        var options = new SessionOptions { Name = sessionName, MaxPlayers = maxPlayers }.WithRelayNetwork();

        await ConnectInternalAsync(displayName,
            () => MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionName, options));
        // Clients do NOT call LoadScene — NGO SceneManager syncs them to the host's scene.
    }

    private async Task JoinBySessionIdInternalAsync(string displayName, string sessionId)
    {
        var joinOptions = new JoinSessionOptions()
            .WithNetworkOptions(new NetworkOptions
            {
                RelayProtocol = RelayProtocol.Default
            });

        await ConnectInternalAsync(displayName,
            () => MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, joinOptions));
        // Clients do NOT call LoadScene — NGO SceneManager syncs them to the host's scene.
    }

    // Shared setup/teardown wrapper for all three connect paths.
    private async Task ConnectInternalAsync(string displayName, Func<Task<ISession>> sessionFactory)
    {
        _state = ConnectionState.Connecting;
        try
        {
            Debug.Log($"[{nameof(ConnectionManager)}] ConnectInternalAsync start displayName={displayName}");
            await _initializeTask;
            await SignInWithProfileAsync(displayName);
            await CleanupExistingMultiplayerStateAsync();

            _session = await RunSessionFactoryWithCleanupRetryAsync(sessionFactory);
            _state = ConnectionState.Connected;
            Debug.Log($"[{nameof(ConnectionManager)}] Session established.");

            OnConnected?.Invoke();
        }
        catch (Exception e)
        {
            _state = ConnectionState.Disconnected;
            Debug.LogException(e);
            Debug.LogWarning($"[{nameof(ConnectionManager)}] ConnectInternalAsync failed. disconnectReason='{_networkManager?.DisconnectReason}'");
            await CleanupExistingMultiplayerStateAsync();
            throw;
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task SignInWithProfileAsync(string displayName)
    {
        var auth = AuthenticationService.Instance;

        if (auth.IsSignedIn)
        {
            if (auth.Profile == displayName)
                return;

            auth.SignOut(true);
        }

        auth.SwitchProfile(displayName);

        await auth.SignInAnonymouslyAsync();
        DisplayName = displayName;
    }

    private async Task<ISession> RunSessionFactoryWithCleanupRetryAsync(Func<Task<ISession>> sessionFactory)
    {
        try
        {
            return await sessionFactory();
        }
        catch (SessionException e) when (e.Message.Contains("Object reference"))
        {
            Debug.LogWarning($"[{nameof(ConnectionManager)}] Session join hit stale lobby state. Cleaning up and retrying once.");
            await CleanupExistingMultiplayerStateAsync();
            await Task.Delay(250);
            return await sessionFactory();
        }
    }

    private async Task CleanupExistingMultiplayerStateAsync()
    {
        var sessions = new List<ISession>();
        if (_session != null)
            sessions.Add(_session);

        try
        {
            foreach (var session in MultiplayerService.Instance.Sessions.Values)
            {
                if (session != null && !sessions.Contains(session))
                    sessions.Add(session);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{nameof(ConnectionManager)}] Could not enumerate existing sessions: {e.Message}");
        }

        foreach (var session in sessions)
            await TryLeaveSessionAsync(session);

        _session = null;

        await RemoveJoinedLobbiesForCurrentPlayerAsync();
        await ResetNetworkManagerAfterFailedStartAsync();
    }

    private async Task TryLeaveSessionAsync(ISession session)
    {
        if (session == null) return;

        try
        {
            await session.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{nameof(ConnectionManager)}] Existing session cleanup skipped: {e.Message}");
        }
    }

    private async Task RemoveJoinedLobbiesForCurrentPlayerAsync()
    {
        var auth = AuthenticationService.Instance;
        string playerId = auth.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
            return;

        List<string> joinedLobbyIds;
        try
        {
            joinedLobbyIds = await LobbyService.Instance.GetJoinedLobbiesAsync();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[{nameof(ConnectionManager)}] Could not query joined lobbies: {e.Message}");
            return;
        }

        foreach (string lobbyId in joinedLobbyIds)
        {
            if (string.IsNullOrWhiteSpace(lobbyId))
                continue;

            try
            {
                var lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                if (lobby != null && lobby.HostId == playerId)
                    await LobbyService.Instance.DeleteLobbyAsync(lobbyId);
                else
                    await LobbyService.Instance.RemovePlayerAsync(lobbyId, playerId);

                Debug.Log($"[{nameof(ConnectionManager)}] Cleaned stale joined lobby {lobbyId}.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[{nameof(ConnectionManager)}] Joined lobby cleanup skipped for {lobbyId}: {e.Message}");
            }
        }
    }

    private async Task ResetNetworkManagerAfterFailedStartAsync()
    {
        if (_networkManager == null)
            return;

        Debug.Log($"[{nameof(ConnectionManager)}] ResetNetworkManagerAfterFailedStartAsync listening={_networkManager.IsListening} client={_networkManager.IsClient} server={_networkManager.IsServer} shutdownInProgress={_networkManager.ShutdownInProgress}");

        if (!_networkManager.ShutdownInProgress &&
            (_networkManager.IsListening || _networkManager.IsClient || _networkManager.IsServer))
        {
            _networkManager.Shutdown();
        }

        while (_networkManager.ShutdownInProgress)
        {
            await Task.Yield();
        }
    }

    private async Task LeaveSessionInternalAsync()
    {
        try
        {
            await _session.LeaveAsync();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        finally
        {
            _session = null;
            _state   = ConnectionState.Disconnected;
            OnDisconnected?.Invoke("Left session");
        }
    }

    private static void ConfigureMultiplayerNetworkStartTimeout()
    {
        try
        {
            var type = Type.GetType("Unity.Services.Multiplayer.NetworkManagerSession, Unity.Services.Multiplayer");
            if (type == null)
            {
                Debug.LogWarning($"[{nameof(ConnectionManager)}] Could not find Unity.Services.Multiplayer.NetworkManagerSession type.");
                return;
            }

            var property = type.GetProperty("CancellationTimeout", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
            {
                Debug.LogWarning($"[{nameof(ConnectionManager)}] Could not configure multiplayer network start timeout.");
                return;
            }

            property.SetValue(null, NetworkStartTimeout);
            Debug.Log($"[{nameof(ConnectionManager)}] Set multiplayer network start timeout to {NetworkStartTimeout.TotalSeconds:0} seconds.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[{nameof(ConnectionManager)}] Failed to configure multiplayer network start timeout: {exception.Message}");
        }
    }
}
