// Assets/Scripts/Core/GameSessionManager.cs
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class GameSessionManager : NetworkBehaviour
{
    [Header("Oturum Ayarları")]
    [SerializeField] private float sessionDuration = 60f;

    public static GameSessionManager Instance { get; private set; }

    public readonly NetworkVariable<float> NetRemainingTime = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<float> NetTotalCredit = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<bool> NetIsActive = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<int> NetPlayerCount = new(
        0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ── Abandonment (Sprint 3 — Yasin #41) ───────────────────────────────────
    private int _abandonedCorpseCount = 0;
    // ─────────────────────────────────────────────────────────────────────────

    public float RemainingTime         => NetRemainingTime.Value;
    public bool  IsSessionActive       => NetIsActive.Value;
    public float TotalCreditCollected  => NetTotalCredit.Value;
    public int   PlayerCount           => NetPlayerCount.Value;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            NetRemainingTime.OnValueChanged += OnTimerChanged;
            NetIsActive.OnValueChanged += OnSessionActiveChanged;
        }
        else
        {
            // === Sunucu oyuna girince süreyi otomatik başlat! ===
            int playersInGame = NetworkManager.Singleton.ConnectedClientsIds.Count;
            StartSession(playersInGame);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            NetRemainingTime.OnValueChanged -= OnTimerChanged;
            NetIsActive.OnValueChanged      -= OnSessionActiveChanged;
        }
    }

    public void StartSession(int playerCount)
    {
        if (NetworkManager.Singleton == null || !IsSpawned)
        {
            Debug.LogWarning("[GameSessionManager] StartSession network'e baglanmadi.");
            return;
        }
        if (!IsServer)
        {
            Debug.LogWarning("[GameSessionManager] StartSession host-only.");
            return;
        }

        NetPlayerCount.Value   = playerCount;
        NetRemainingTime.Value = sessionDuration;
        NetTotalCredit.Value   = 0f;
        NetIsActive.Value      = true;
        _abandonedCorpseCount  = 0;

        GameEventBus.Publish(new SessionStartedEvent(playerCount, sessionDuration));
    }

    public void EndSession(SessionEndReason reason)
    {
        if (!IsServer) return;
        if (!NetIsActive.Value) return;

        NetIsActive.Value      = false;
        NetRemainingTime.Value = 0f;
        GameEventBus.Publish(new SessionEndedEvent(reason, NetTotalCredit.Value));
        BroadcastSessionEndedRpc((byte)reason, NetTotalCredit.Value);
    }

    [Rpc(SendTo.NotServer)]
    private void BroadcastSessionEndedRpc(byte reason, float totalCredit)
    {
        GameEventBus.Publish(new SessionEndedEvent((SessionEndReason)reason, totalCredit));
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer) return;
        if (!NetIsActive.Value) return;

        NetRemainingTime.Value = Mathf.Max(0f, NetRemainingTime.Value - Time.deltaTime);
        GameEventBus.Publish(new TimerEventTriggered(NetRemainingTime.Value, sessionDuration));

        if (NetRemainingTime.Value <= 0f)
        {
            if (ExtractionService.Instance != null)
                ExtractionService.Instance.ForceExtraction(); // tek sonlandırma yolu
            else
                EndSession(SessionEndReason.TimeUp);           // güvenlik fallback
        }
    }

    private void OnTimerChanged(float previous, float current)
    {
        GameEventBus.Publish(new TimerEventTriggered(current, sessionDuration));
    }

    private void OnSessionActiveChanged(bool previous, bool current)
    {
        if (current)
            GameEventBus.Publish(new SessionStartedEvent(NetPlayerCount.Value, sessionDuration));
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        if (!IsServer) return;
        if (!NetIsActive.Value) return;

        // Tüm bağlı oyuncular öldüyse → wipe (toplam kayıp).
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        foreach (var kvp in nm.ConnectedClients)
        {
            var po = kvp.Value.PlayerObject;
            if (po == null) continue;
            var machine = po.GetComponent<PlayerStateMachine>();
            if (machine != null && machine.IsAlive) return; // en az bir canlı var
        }

        if (ExtractionService.Instance != null)
            ExtractionService.Instance.TriggerWipe();
    }

    // ── Abandonment Penalty (Sprint 3 — Yasin #41) ───────────────────────────

    /// <summary>
    /// Tesiste kalan her ceset için extraction öncesi çağrılır.
    /// CorpseItem veya ExtractionPoint trigger'ı çağırır.
    /// </summary>
    public void RegisterAbandonedCorpse()
    {
        if (!IsServer) return;
        _abandonedCorpseCount++;
        Debug.Log($"[GameSessionManager] Terk edilen ceset sayısı: {_abandonedCorpseCount}");
    }

    /// <summary>
    /// GDD §6.4: penalty = max(0.25, playerCount/100) * grossCredits — her ceset için.
    /// Dönen değer: toplam kesinti miktarı.
    /// </summary>
    public float CalculateAbandonmentPenalty(float grossCredits)
    {
        if (!IsServer) return 0f;
        if (_abandonedCorpseCount == 0) return 0f;

        int deduction = Ludus.Extraction.Core.AbandonmentPenalty.Calculate(
            Mathf.RoundToInt(grossCredits), _abandonedCorpseCount, NetPlayerCount.Value);

        GameEventBus.Publish(new CorpseAbandonedEvent(0, Mathf.Max(0.25f, NetPlayerCount.Value / 100f)));

        _abandonedCorpseCount = 0;
        return deduction;
    }

    /// <summary>
    /// Extraction'da çağrılır: penalty hesapla, net krediyi uygula, session'ı bitir.
    /// ExtractionPoint.cs bu metodu çağırır.
    /// </summary>
    public void EndSessionWithPenalty()
    {
        if (!IsServer) return;

        float gross     = NetTotalCredit.Value;
        float deduction = CalculateAbandonmentPenalty(gross);

        NetTotalCredit.Value = gross - deduction;
        EndSession(SessionEndReason.Escaped);
    }

    // ─────────────────────────────────────────────────────────────────────────
}