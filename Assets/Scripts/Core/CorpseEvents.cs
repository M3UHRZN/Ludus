// CorpseEvents.cs
// Assets/Scripts/Core/Events/CorpseEvents.cs

// ------------------------------------------------------------------ Corpse Events

/// <summary>Bir oyuncu cesedi kaldırınca yayınlanır.</summary>
public struct CorpsePickedUpEvent
{
    public ulong CorpseOwnerClientId; 
    public ulong CarrierClientId;     

    public CorpsePickedUpEvent(ulong corpseOwner, ulong carrier)
    {
        CorpseOwnerClientId = corpseOwner;
        CarrierClientId     = carrier;
    }
}

/// <summary>Ceset bırakılınca yayınlanır (abandonment riski — timer tur sonu).</summary>
public struct CorpseDroppedEvent
{
    public ulong CorpseOwnerClientId;

    public CorpseDroppedEvent(ulong corpseOwner)
    {
        CorpseOwnerClientId = corpseOwner;
    }
}


/// <summary>
/// Tur sonunda ceset tesiste bırakıldı — GameSessionManager abandonment penalty hesaplar.
/// Extraction ekranında gösterilmek üzere Esmanur'un HUD sistemi dinler.
/// </summary>
public struct CorpseAbandonedEvent
{
    public ulong AbandonedClientId;
    public float PenaltyPercent; // max(0.25f, playerCount / 100f)

    public CorpseAbandonedEvent(ulong clientId, float penalty)
    {
        AbandonedClientId = clientId;
        PenaltyPercent    = penalty;
    }
}