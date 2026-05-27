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

    private bool _last10SecPlayed = false;

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
        GameEventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
        GameEventBus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
        GameEventBus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
        {
            NetRemainingTime.OnValueChanged += OnTimerChanged;
            NetIsActive.OnValueChanged      += OnSessionActiveChanged;
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
        _last10SecPlayed = false;

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
        GameEventBus.Publish(new TimerEventTriggered(NetRemainingTime.Value));

        // === SON 10 SANİYE SES TETİKLEYİCİSİ ===
        if (NetRemainingTime.Value <= 10f && !_last10SecPlayed && NetRemainingTime.Value > 0f)
        {
            _last10SecPlayed = true;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.last10SecSound);
            }
        }
        // =======================================

        if (NetRemainingTime.Value <= 0f)
            EndSession(SessionEndReason.TimeUp);
    }

    private void OnTimerChanged(float previous, float current)
    {
        GameEventBus.Publish(new TimerEventTriggered(current));
    }

    private void OnSessionActiveChanged(bool previous, bool current)
    {
        if (current)
            GameEventBus.Publish(new SessionStartedEvent(NetPlayerCount.Value, sessionDuration));
    }

    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        if (!IsServer) return;
        NetTotalCredit.Value += evt.CreditValue;
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        // Ölüm takibi — ceset tesiste bırakılırsa RegisterAbandonedCorpse() çağrılır
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

        int   playerCount      = NetPlayerCount.Value;
        float penaltyPerCorpse = Mathf.Max(0.25f, playerCount / 100f);
        float totalDeduction   = Mathf.Min(
            grossCredits * penaltyPerCorpse * _abandonedCorpseCount,
            grossCredits);

        Debug.Log($"[GameSessionManager] Abandonment penalty: " +
                  $"{_abandonedCorpseCount} ceset x {penaltyPerCorpse:P0} = " +
                  $"{totalDeduction} kesinti | net={grossCredits - totalDeduction}");

        GameEventBus.Publish(new CorpseAbandonedEvent(0, penaltyPerCorpse));

        _abandonedCorpseCount = 0;
        return totalDeduction;
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