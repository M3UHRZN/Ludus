using System;
using System.Collections.Generic;

/// <summary>
/// GameEventBus - Observer Pattern
/// Sistemler birbirini tanımadan haberleşir.
/// 
/// KULLANIM:
/// - Abone ol  : GameEventBus.Subscribe<EnemyDiedEvent>(OnEnemyDied);
/// - Yayınla   : GameEventBus.Publish(new EnemyDiedEvent(enemyId));
/// - Aboneliği kes : GameEventBus.Unsubscribe<EnemyDiedEvent>(OnEnemyDied);
/// 
/// Assets/Scripts/Core/GameEventBus.cs
/// </summary>
public static class GameEventBus
{
    // Her event tipi için handler listesi tutan sözlük
    private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

    // Abone Ol
    public static void Subscribe<T>(Action<T> handler)
    {
        Type type = typeof(T);

        if (!_handlers.ContainsKey(type))
            _handlers[type] = new List<Delegate>();

        _handlers[type].Add(handler);
    }

    // Aboneliği Kes
    public static void Unsubscribe<T>(Action<T> handler)
    {
        Type type = typeof(T);

        if (_handlers.ContainsKey(type))
            _handlers[type].Remove(handler);
    }

    // Yayınla
    public static void Publish<T>(T eventData)
    {
        Type type = typeof(T);

        if (!_handlers.ContainsKey(type)) return;

        // Listeyi kopyala - yayın sırasında abonelik değişirse sorun çıkmasın
        var handlers = new List<Delegate>(_handlers[type]);

        foreach (var handler in handlers)
            (handler as Action<T>)?.Invoke(eventData);
    }

    // Tüm Abonelikleri Temizle (sahne geçişlerinde çağır)
    public static void Clear()
    {
        _handlers.Clear();
    }
}

// EVENT TİPLERİ

/// <summary>Düşman öldüğünde yayınlanır.</summary>
public struct EnemyDiedEvent
{
    public int EnemyId;
    public UnityEngine.Vector3 Position;

    public EnemyDiedEvent(int enemyId, UnityEngine.Vector3 position)
    {
        EnemyId = enemyId;
        Position = position;
    }
}

/// <summary>Oyuncu bir eşyayı aldığında yayınlanır.</summary>
public struct ItemPickedUpEvent
{
    public string ItemName;
    public int Weight;
    public float CreditValue;

    public ItemPickedUpEvent(string itemName, int weight, float creditValue)
    {
        ItemName = itemName;
        Weight = weight;
        CreditValue = creditValue;
    }
}

/// <summary>Oyuncu hasar aldığında yayınlanır.</summary>
public struct PlayerDamagedEvent
{
    public int PlayerId;
    public float Damage;
    public float RemainingHP;

    public PlayerDamagedEvent(int playerId, float damage, float remainingHP)
    {
        PlayerId = playerId;
        Damage = damage;
        RemainingHP = remainingHP;
    }
}

/// <summary>Event timer tetiklendiğinde yayınlanır.</summary>
public struct TimerEventTriggered
{
    public float RemainingSeconds;
    public bool IsUrgent;          // 10 saniyenin altında mı

    public TimerEventTriggered(float remainingSeconds)
    {
        RemainingSeconds = remainingSeconds;
        IsUrgent = remainingSeconds <= 10f;
    }
}

/// <summary>Oyuncu öldüğünde yayınlanır.</summary>
public struct PlayerDiedEvent
{
    public int PlayerId;
    public UnityEngine.Vector3 DeathPosition;

    public PlayerDiedEvent(int playerId, UnityEngine.Vector3 deathPosition)
    {
        PlayerId = playerId;
        DeathPosition = deathPosition;
    }
}

/// <summary>Seviye sona erdiğinde yayınlanır.</summary>
public struct LevelEndedEvent
{
    public bool IsSuccess;
    public int CollectedCredits;
    public int PenaltyAmount;
    public float QuotaFillAmount;

    public LevelEndedEvent(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
    {
        IsSuccess = isSuccess;
        CollectedCredits = collectedCredits;
        PenaltyAmount = penaltyAmount;
        QuotaFillAmount = quotaFillAmount;
    }
}