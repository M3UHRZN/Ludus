using System;
using System.Collections.Generic;
using UnityEngine;

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

/// <summary>!!!Oyuncu bir eşyayı yere bıraktığında yayınlanır. <----- sonradan ekledim -esmnr- </summary>
public struct ItemDroppedEvent
{
    public string ItemName;
    public int Weight;

    public ItemDroppedEvent(string itemName, int weight)
    {
        ItemName = itemName;
        Weight = weight;
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

///// <summary>Event timer tetiklendiğinde yayınlanır.</summary>
//public struct TimerEventTriggered
//{
//    public float RemainingSeconds;
//    public bool IsUrgent;          // 10 saniyenin altında mı

//    public TimerEventTriggered(float remainingSeconds)
//    {
//        RemainingSeconds = remainingSeconds;
//        IsUrgent = remainingSeconds <= 10f;
//    }
//}

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

public enum SessionEndReason { TimeUp, AllDead, Escaped }

/// <summary>Oyun oturumu başladığında yayınlanır.</summary>
public struct SessionStartedEvent
{
    public int PlayerCount;
    public float SessionDuration;

    public SessionStartedEvent(int playerCount, float sessionDuration)
    {
        PlayerCount = playerCount;
        SessionDuration = sessionDuration;
    }
}

/// <summary>Oyun oturumu sona erdiğinde yayınlanır.</summary>
public struct SessionEndedEvent
{
    public SessionEndReason Reason;
    public float TotalCreditCollected;

    public SessionEndedEvent(SessionEndReason reason, float totalCredit)
    {
        Reason = reason;
        TotalCreditCollected = totalCredit;
    }
}

/// <summary>
/// Prosedürel harita üretimi tamamlandığında MapGenerator tarafından yayınlanır.
/// EnemySpawner ve diğer dinleyiciler bu event'ten sonra
/// sahnedeki marker'ları (EnemySpawnPoint, PatrolWaypointGroup) taramaya başlar.
/// </summary>
public struct MapReadyEvent
{
    public int Seed;
    public int RoomCount;
    /// <summary>
    /// Her bir oda icin world-space AABB. NetworkedItemSpawner bu bounds'larin
    /// icine NavMesh snap'li rastgele item pozisyonlari uretir.
    /// Null veya empty array de gecerlidir (eski caller'lar icin) — bu durumda
    /// item spawn atlanir.
    /// </summary>
    public Bounds[] RoomBounds;

    public MapReadyEvent(int seed, int roomCount)
    {
        Seed = seed;
        RoomCount = roomCount;
        RoomBounds = null;
    }

    public MapReadyEvent(int seed, int roomCount, Bounds[] roomBounds)
    {
        Seed = seed;
        RoomCount = roomCount;
        RoomBounds = roomBounds;
    }
}

/// <summary>Ölen bir oyuncu Infirmary'de canlandırıldığında yayınlanır. <----- sprint2 de eklenecek ben simdiden ekledim 8.5.26 -esmnr- </summary>
public struct PlayerRevivedEvent
{
    public int PlayerId;

    public PlayerRevivedEvent(int playerId)
    {
        PlayerId = playerId;
    }
}

///// <summary>Seviye sona erdiğinde yayınlanır.</summary>
//public struct LevelEndedEvent
//{
//    public bool IsSuccess;
//    public int CollectedCredits;
//    public int PenaltyAmount;
//    public float QuotaFillAmount;

//    public LevelEndedEvent(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
//    {
//        IsSuccess = isSuccess;
//        CollectedCredits = collectedCredits;
//        PenaltyAmount = penaltyAmount;
//        QuotaFillAmount = quotaFillAmount;
//    }
//}

/// <summary>
/// Sahnede bir ses kaynagi olustugunda yayinlanir (ayak sesi, item dusurme,
/// silah sesi, vs.). EnemyController bunu dinler ve menzilindeyse Chase'e gecer.
/// </summary>
public struct NoiseEmittedEvent
{
    public UnityEngine.Vector3 Position;
    public float Range;
    public string Source;

    public NoiseEmittedEvent(UnityEngine.Vector3 position, float range, string source = null)
    {
        Position = position;
        Range = range;
        Source = source;
    }
}

/// <summary>Seviye sona erdiğinde yayınlanır.</summary>
public struct LevelEndedEvent
{
    public bool IsSuccess;
    public int CollectedCredits;
    public int PenaltyAmount;
    public float QuotaFillAmount;

    // --- UI İÇİN EKLENEN YENİ VERİLER ---
    public float TimeLeft;
    public bool[] PlayerAliveStates;

    // Constructor (Oluşturucu) fonksiyonu da yeni verilere göre güncellendi
    public LevelEndedEvent(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount, float timeLeft, bool[] playerAliveStates)
    {
        IsSuccess = isSuccess;
        CollectedCredits = collectedCredits;
        PenaltyAmount = penaltyAmount;
        QuotaFillAmount = quotaFillAmount;
        TimeLeft = timeLeft;
        PlayerAliveStates = playerAliveStates;
    }

    // Overloaded constructor for compatibility with 4-parameter calls
    public LevelEndedEvent(bool isSuccess, int collectedCredits, int penaltyAmount, float quotaFillAmount)
    {
        IsSuccess = isSuccess;
        CollectedCredits = collectedCredits;
        PenaltyAmount = penaltyAmount;
        QuotaFillAmount = quotaFillAmount;
        TimeLeft = 0f;
        PlayerAliveStates = null;
    }
}

// Karakter koştuğunda veya staminası değiştiğinde fırlatılacak Event
public struct StaminaUpdatedEvent
{
    public float CurrentStamina;
    public float MaxStamina;
    public bool IsExhausted; // True ise karakter nefes nefese kalmış ve HP kaybetmeye başlamıştır
}

/// <summary>Event timer tetiklendiğinde/zaman aktığında yayınlanır.</summary>
public struct TimerEventTriggered 
{
    public float RemainingSeconds;
    public float MaxSeconds;       // <-- UI BARI İÇİN ŞART! 
    public bool IsUrgent;          // 10 saniyenin altında mı

    // Constructor (Yapıcı Metot) da yeni veriye göre güncellendi
    public TimerEventTriggered(float remainingSeconds, float maxSeconds)
    {
        RemainingSeconds = remainingSeconds;
        MaxSeconds = maxSeconds;
        IsUrgent = remainingSeconds <= 10f;
    }
}