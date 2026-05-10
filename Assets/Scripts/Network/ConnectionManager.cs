using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
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
        _networkManager = GetComponent<NetworkManager>();
        _networkManager.OnClientConnectedCallback    += OnClientConnectedCallback;
        _networkManager.OnClientDisconnectCallback   += OnClientDisconnectCallback;
        _initializeTask = UnityServices.InitializeAsync();
    }

    private void OnDestroy()
    {
        if (_networkManager != null)
        {
            _networkManager.OnClientConnectedCallback  -= OnClientConnectedCallback;
            _networkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
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
        if (_networkManager.LocalClientId == clientId)
        {
            Debug.Log($"Client-{clientId} is connected and can spawn {nameof(NetworkObject)}s.");
        }
    }

    private void OnClientDisconnectCallback(ulong clientId)
    {
        // If we are a pure client and the server disconnected us, raise the event.
        if (!_networkManager.IsServer && clientId == NetworkManager.ServerClientId)
        {
            OnDisconnected?.Invoke("Host disconnected");
        }
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
    public async Task CreateOrJoinByNameAsync(string displayName, string sessionName)
    {
        GuardInFlight();

        _inFlight = CreateOrJoinByNameInternalAsync(displayName, sessionName);
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
    public async Task<QuerySessionsResults> QuerySessionsAsync()
    {
        await _initializeTask;

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
        if (_session == null) return;

        await LeaveSessionInternalAsync();
        await ResetNetworkManagerAfterFailedStartAsync();
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
        _state = ConnectionState.Connecting;
        try
        {
            await _initializeTask;
            await SignInWithProfileAsync(displayName);

            if (_session != null)
            {
                await LeaveSessionInternalAsync();
                await ResetNetworkManagerAfterFailedStartAsync();
            }

            var options = new SessionOptions
            {
                Name       = sessionName,
                MaxPlayers = maxPlayers
            }.WithRelayNetwork();

            _session = await MultiplayerService.Instance.CreateSessionAsync(options);
            _state = ConnectionState.Connected;

            OnConnected?.Invoke();

            if (_networkManager.IsHost)
            {
                _networkManager.SceneManager.LoadScene(
                    "LobbyScene",
                    UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
        }
        catch (Exception e)
        {
            _state = ConnectionState.Disconnected;
            Debug.LogException(e);
            await ResetNetworkManagerAfterFailedStartAsync();
            throw;
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task CreateOrJoinByNameInternalAsync(string displayName, string sessionName)
    {
        _state = ConnectionState.Connecting;
        try
        {
            await _initializeTask;
            await SignInWithProfileAsync(displayName);

            if (_session != null)
            {
                await LeaveSessionInternalAsync();
                await ResetNetworkManagerAfterFailedStartAsync();
            }

            var options = new SessionOptions
            {
                Name = sessionName
            }.WithRelayNetwork();

            _session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(sessionName, options);
            _state = ConnectionState.Connected;

            OnConnected?.Invoke();
            // Clients do NOT call LoadScene — NGO SceneManager syncs them to the host's scene.
        }
        catch (Exception e)
        {
            _state = ConnectionState.Disconnected;
            Debug.LogException(e);
            await ResetNetworkManagerAfterFailedStartAsync();
            throw;
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task JoinBySessionIdInternalAsync(string displayName, string sessionId)
    {
        _state = ConnectionState.Connecting;
        try
        {
            await _initializeTask;
            await SignInWithProfileAsync(displayName);

            if (_session != null)
            {
                await LeaveSessionInternalAsync();
                await ResetNetworkManagerAfterFailedStartAsync();
            }

            _session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId);
            _state = ConnectionState.Connected;

            OnConnected?.Invoke();
            // Clients do NOT call LoadScene — NGO SceneManager syncs them to the host's scene.
        }
        catch (Exception e)
        {
            _state = ConnectionState.Disconnected;
            Debug.LogException(e);
            await ResetNetworkManagerAfterFailedStartAsync();
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

    private async Task ResetNetworkManagerAfterFailedStartAsync()
    {
        if (_networkManager == null)
            return;

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
}
