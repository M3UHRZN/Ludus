using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main-menu canvas controller.
///
/// Panel A — Direct Connect (host / join-by-name / open browser).
/// Panel B — Session Browser (scrollable list, polling, manual refresh).
///
/// Requires a ConnectionManager somewhere in the scene (found via FindFirstObjectByType).
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector — Panel A
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Panel A — Direct Connect")]
    [SerializeField] private GameObject    panelA;
    [SerializeField] private TMP_InputField displayNameInput;
    [SerializeField] private TMP_InputField sessionNameInput;
    [SerializeField] private Button         hostButton;
    [SerializeField] private Button         joinByNameButton;
    [SerializeField] private Button         browseLobbyButton;
    [SerializeField] private TMP_Text       statusText;

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector — Panel B
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Panel B — Session Browser")]
    [SerializeField] private GameObject panelB;
    [SerializeField] private Transform  sessionListParent;
    [SerializeField] private GameObject sessionEntryPrefab;
    [SerializeField] private Button     refreshButton;
    [SerializeField] private Button     backButton;

    // ──────────────────────────────────────────────────────────────────────────
    // Private state
    // ──────────────────────────────────────────────────────────────────────────

    private ConnectionManager    _cm;
    private QuerySessionsResults _queryResult;
    private float                _nextAllowedRefresh;

    // Fallback coroutine handle — used when QuerySessionsResults has no OnUpdated event.
    private Coroutine _pollingCoroutine;

    // ──────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cm = FindFirstObjectByType<ConnectionManager>();

        hostButton.onClick.AddListener(OnHostClicked);
        joinByNameButton.onClick.AddListener(OnJoinByNameClicked);
        browseLobbyButton.onClick.AddListener(OnBrowseLobbyClicked);
        refreshButton.onClick.AddListener(OnRefreshClicked);
        backButton.onClick.AddListener(CloseBrowser);
    }

    private void Start()
    {
        if (_cm != null)
        {
            _cm.OnConnected     += HandleConnected;
            _cm.OnDisconnected  += HandleDisconnected;
        }
        else
        {
            SetStatus("ConnectionManager not found in scene!", isError: true);
        }

        panelA.SetActive(true);
        panelB.SetActive(false);
        SetStatus(string.Empty);
    }

    private void OnDestroy()
    {
        if (_cm != null)
        {
            _cm.OnConnected    -= HandleConnected;
            _cm.OnDisconnected -= HandleDisconnected;
        }

        StopPollingCoroutine();
    }

    private bool _lastBusy;

    private void Update()
    {
        if (_cm == null) return;

        bool busy = _cm.IsConnecting;
        bool refreshReady = !busy && Time.unscaledTime >= _nextAllowedRefresh;

        if (busy == _lastBusy && refreshButton.interactable == refreshReady) return;
        _lastBusy = busy;

        hostButton.interactable        = !busy;
        joinByNameButton.interactable  = !busy;
        browseLobbyButton.interactable = !busy;
        refreshButton.interactable     = refreshReady;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Panel A — button handlers
    // ──────────────────────────────────────────────────────────────────────────

    private async void OnHostClicked()
    {
        if (_cm == null || _cm.IsConnecting) return;

        string displayName = DisplayName;
        string sessionName = SessionName;

        if (!ValidateInputs(displayName, sessionName)) return;
        SaveDisplayName(displayName);

        SetStatus("Connecting...");
        try
        {
            await _cm.HostAsync(displayName, sessionName);
        }
        catch (Exception e)
        {
            SetStatus($"Host failed: {e.Message}", isError: true);
        }
    }

    private async void OnJoinByNameClicked()
    {
        if (_cm == null || _cm.IsConnecting) return;

        string displayName = DisplayName;
        string sessionName = SessionName;

        if (!ValidateInputs(displayName, sessionName)) return;
        SaveDisplayName(displayName);

        SetStatus("Connecting...");
        try
        {
            await _cm.CreateOrJoinByNameAsync(displayName, sessionName);
        }
        catch (Exception e)
        {
            SetStatus($"Join failed: {e.Message}", isError: true);
        }
    }

    private async void OnBrowseLobbyClicked()
    {
        if (_cm == null || _cm.IsConnecting) return;
        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            SetStatus("Callsign required before browsing.", isError: true);
            return;
        }
        SaveDisplayName(DisplayName);
        try
        {
            await OpenBrowserAsync();
        }
        catch (Exception e)
        {
            SetStatus(e.Message, isError: true);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Panel B — open / close / refresh
    // ──────────────────────────────────────────────────────────────────────────

    private async System.Threading.Tasks.Task OpenBrowserAsync()
    {
        panelA.SetActive(false);
        panelB.SetActive(true);

        await RefreshSessionListAsync();   // initial fetch

        if (this == null) return;

        // Start SDK-level polling (updates _queryResult.Sessions automatically)
        _queryResult?.StartPolling(pollingDelaySeconds: 5);

        // Fallback coroutine — re-renders every 5 s (SDK 2.2.x has no OnUpdated event)
        StopPollingCoroutine();
        _pollingCoroutine = StartCoroutine(PollingFallbackCoroutine());
    }

    private void CloseBrowser()
    {
        _queryResult?.StopPolling();
        StopPollingCoroutine();
        ClearSessionList();

        panelA.SetActive(true);
        panelB.SetActive(false);
    }

    private async void OnRefreshClicked()
    {
        if (Time.unscaledTime < _nextAllowedRefresh) return;

        try
        {
            await RefreshSessionListAsync();
            _nextAllowedRefresh = Time.unscaledTime + 1.25f;
        }
        catch (Unity.Services.Core.RequestFailedException rfe) when (rfe.ErrorCode == 429)
        {
            _nextAllowedRefresh = Time.unscaledTime + 2.5f;
            SetStatus("Rate limited, try again soon.", isError: true);
        }
        catch (Exception e)
        {
            SetStatus($"Refresh failed: {e.Message}", isError: true);
        }
    }

    private async System.Threading.Tasks.Task RefreshSessionListAsync()
    {
        _queryResult = await _cm.QuerySessionsAsync(DisplayName);

        if (this == null) return;

        RenderSessionList(_queryResult.Sessions);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Session list helpers
    // ──────────────────────────────────────────────────────────────────────────

    private void RenderSessionList(IList<ISessionInfo> sessions)
    {
        ClearSessionList();

        if (sessions == null) return;

        foreach (var s in sessions)
        {
            var go = Instantiate(sessionEntryPrefab, sessionListParent);
            go.GetComponent<SessionEntryUI>().Init(s, OnJoinSessionById);
        }
    }

    private void ClearSessionList()
    {
        foreach (Transform child in sessionListParent)
        {
            Destroy(child.gameObject);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Join from browser
    // ──────────────────────────────────────────────────────────────────────────

    private async void OnJoinSessionById(string sessionId)
    {
        if (_cm == null || _cm.IsConnecting) return;

        string displayName = DisplayName;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Display name cannot be empty.", isError: true);
            return;
        }
        SaveDisplayName(displayName);

        SetStatus("Connecting...");
        try
        {
            await _cm.JoinBySessionIdAsync(displayName, sessionId);
        }
        catch (Exception e)
        {
            SetStatus($"Join failed: {e.Message}", isError: true);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ConnectionManager event handlers
    // ──────────────────────────────────────────────────────────────────────────

    private void HandleConnected()
    {
        // Hide entire menu canvas — NGO SceneManager will load the gameplay scene
        gameObject.SetActive(false);
    }

    private void HandleDisconnected(string reason)
    {
        CloseBrowser();
        gameObject.SetActive(true);
        SetStatus(reason, isError: true);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Polling fallback coroutine
    // ──────────────────────────────────────────────────────────────────────────

    private IEnumerator PollingFallbackCoroutine()
    {
        var wait = new WaitForSecondsRealtime(5f);
        while (true)
        {
            yield return wait;

            if (_queryResult != null)
                RenderSessionList(_queryResult.Sessions);
        }
    }

    private void StopPollingCoroutine()
    {
        if (_pollingCoroutine != null)
        {
            StopCoroutine(_pollingCoroutine);
            _pollingCoroutine = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Utility
    // ──────────────────────────────────────────────────────────────────────────

    private string DisplayName => displayNameInput.text.Trim();
    private string SessionName => sessionNameInput.text.Trim();

    private static void SaveDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;

        PlayerPrefs.SetString("DisplayName", displayName.Trim());
        PlayerPrefs.Save();
    }

    private bool ValidateInputs(string displayName, string sessionName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            SetStatus("Display name cannot be empty.", isError: true);
            return false;
        }

        if (string.IsNullOrWhiteSpace(sessionName))
        {
            SetStatus("Session name cannot be empty.", isError: true);
            return false;
        }

        return true;
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (statusText == null) return;
        statusText.text  = message;
        statusText.color = isError ? Color.red : Color.white;
    }
}
