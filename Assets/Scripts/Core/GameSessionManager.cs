// Assets/Scripts/Core/GameSessionManager.cs
using UnityEngine;

public class GameSessionManager : Singleton<GameSessionManager>
{
    [Header("Oturum Ayarları")]
    [SerializeField] private float sessionDuration = 60f;

    public int PlayerCount { get; private set; }
    public float RemainingTime { get; private set; }
    public bool IsSessionActive { get; private set; }
    public float TotalCreditCollected { get; private set; }

    protected override void Awake()
    {
        base.Awake();
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

    public void StartSession(int playerCount)
    {
        PlayerCount = playerCount;
        RemainingTime = sessionDuration;
        TotalCreditCollected = 0f;
        IsSessionActive = true;

        GameEventBus.Publish(new SessionStartedEvent(playerCount, sessionDuration));
    }

    private void Update()
    {
        if (!IsSessionActive) return;

        RemainingTime -= Time.deltaTime;
        GameEventBus.Publish(new TimerEventTriggered(RemainingTime));

        if (RemainingTime <= 0f)
            EndSession(SessionEndReason.TimeUp);
    }

    public void EndSession(SessionEndReason reason)
    {
        if (!IsSessionActive) return;

        IsSessionActive = false;
        RemainingTime = 0f;
        GameEventBus.Publish(new SessionEndedEvent(reason, TotalCreditCollected));
    }

    private void OnItemPickedUp(ItemPickedUpEvent evt)
    {
        TotalCreditCollected += evt.CreditValue;
    }

    private void OnPlayerDied(PlayerDiedEvent evt)
    {
        // Sprint 2: tüm oyuncular ölünce EndSession(SessionEndReason.AllDead)
    }
}
