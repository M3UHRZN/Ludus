using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

public class ConnectionManager : MonoBehaviour
{
   private string _profileName;
   private string _sessionName;
   private int _maxPlayers = 10;
   private ConnectionState _state = ConnectionState.Disconnected;
   private ISession _session;
   private NetworkManager m_NetworkManager;
   private Task _initializeTask;
   private Task _connectTask;
   private bool _isQuitting;

   private enum ConnectionState
   {
       Disconnected,
       Connecting,
       Connected,
   }

    private void Awake()
    {
        m_NetworkManager = GetComponent<NetworkManager>();
        m_NetworkManager.OnClientConnectedCallback += OnClientConnectedCallback;
        m_NetworkManager.OnSessionOwnerPromoted += OnSessionOwnerPromoted;
        _initializeTask = UnityServices.InitializeAsync();
    }

    private void OnSessionOwnerPromoted(ulong sessionOwnerPromoted)
    {
        if (m_NetworkManager.LocalClient.IsSessionOwner)
        {
            Debug.Log($"Client-{m_NetworkManager.LocalClientId} is the session owner!");
        }
    }

    private void OnClientConnectedCallback(ulong clientId)
    {
        if (m_NetworkManager.LocalClientId == clientId)
        {
            Debug.Log($"Client-{clientId} is connected and can spawn {nameof(NetworkObject)}s.");
        }
    }

   private void OnGUI()
   {
       if (_state == ConnectionState.Connected)
           return;

       GUI.enabled = _state != ConnectionState.Connecting;

       using (new GUILayout.HorizontalScope(GUILayout.Width(250)))
       {
           GUILayout.Label("Profile Name", GUILayout.Width(100));
           _profileName = GUILayout.TextField(_profileName);
       }

       using (new GUILayout.HorizontalScope(GUILayout.Width(250)))
       {
           GUILayout.Label("Session Name", GUILayout.Width(100));
           _sessionName = GUILayout.TextField(_sessionName);
       }

       GUI.enabled = GUI.enabled && !string.IsNullOrEmpty(_profileName) && !string.IsNullOrEmpty(_sessionName);

       if (GUILayout.Button("Create or Join Session"))
       {
           _connectTask = CreateOrJoinSessionAsync();
       }
   }

   private void OnDestroy()
   {
       if (m_NetworkManager != null)
       {
           m_NetworkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
           m_NetworkManager.OnSessionOwnerPromoted -= OnSessionOwnerPromoted;
       }

       if (!_isQuitting && _session != null)
       {
           _ = LeaveSessionAsync();
       }
   }

   private void OnApplicationQuit()
   {
       _isQuitting = true;
   }

   private async Task CreateOrJoinSessionAsync()
   {
       if (_connectTask != null && !_connectTask.IsCompleted)
           return;

       _state = ConnectionState.Connecting;

       try
       {
           await _initializeTask;
           await SignInWithProfileAsync();

           // Önceki session tam temizlenmeden yeni bağlantı açılırsa SDK task cancel atar
           if (_session != null)
           {
               await LeaveSessionAsync();
               await ResetNetworkManagerAfterFailedStartAsync();
           }

           var options = new SessionOptions() {
               Name = _sessionName,
               MaxPlayers = _maxPlayers
           }.WithDistributedAuthorityNetwork();

           _session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(_sessionName, options);
           _state = ConnectionState.Connected;
       }
       catch (Exception e)
       {
           _state = ConnectionState.Disconnected;
           Debug.LogException(e);
           await ResetNetworkManagerAfterFailedStartAsync();
       }
   }

   private async Task SignInWithProfileAsync()
   {
       var authenticationService = AuthenticationService.Instance;

       if (authenticationService.IsSignedIn)
       {
           if (authenticationService.Profile == _profileName)
           {
               return;
           }

           authenticationService.SignOut(true);
       }

       if (authenticationService.Profile != _profileName)
       {
           authenticationService.SwitchProfile(_profileName);
       }

       await authenticationService.SignInAnonymouslyAsync();
   }

   private async Task ResetNetworkManagerAfterFailedStartAsync()
   {
       if (m_NetworkManager == null)
       {
           return;
       }

       if (!m_NetworkManager.ShutdownInProgress &&
           (m_NetworkManager.IsListening || m_NetworkManager.IsClient || m_NetworkManager.IsServer))
       {
           m_NetworkManager.Shutdown();
       }

       while (m_NetworkManager.ShutdownInProgress)
       {
           await Task.Yield();
       }
   }

   private async Task LeaveSessionAsync()
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
           _state = ConnectionState.Disconnected;
       }
   }
}
