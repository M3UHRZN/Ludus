using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MarketWallet : NetworkBehaviour
{
    [SerializeField] private int startingCredits = 100;
    [SerializeField] private int currentCredits;

    public readonly NetworkVariable<int> NetCredits = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int CurrentCredits => IsSpawned ? NetCredits.Value : currentCredits;
    public event Action<int> CreditsChanged;

    private void Awake()
    {
        // Network'e baglanmadan (local/test) once: kasadan oku. Kasa bos ise startingCredits ile kur.
        if (!IsSpawned)
        {
            MarketCreditBank.EnsureInitialized(startingCredits);
            currentCredits = MarketCreditBank.Credits;
        }
    }

    public override void OnNetworkSpawn()
    {
        NetCredits.OnValueChanged += OnNetCreditsChanged;

        // Para kasada (MarketCreditBank) durur; wallet sahneyle dogup olse de bakiye korunur.
        // Ilk acilista startingCredits ile kur, run'dan donuste mevcut bakiyeyi oku.
        // (Eskiden burada 100'e resetleniyordu — yasanan bug'in kaynagi buydu.)
        if (IsServer)
        {
            MarketCreditBank.EnsureInitialized(startingCredits);
            NetCredits.Value = MarketCreditBank.Credits;
        }

        CreditsChanged?.Invoke(CurrentCredits);
    }

    public override void OnNetworkDespawn()
    {
        NetCredits.OnValueChanged -= OnNetCreditsChanged;
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && CurrentCredits >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount))
            return false;

        // CurrentCredits property (NetCredits.Value when spawned) okumamiz sart;
        // `currentCredits` field NGO callback'i fire olana kadar stale kalir, ardarda
        // satin almalarda hep eski deger kullanilir ve para hic dusmez ("sadece bir
        // defa alindi" yanilgisinin sebebi buydu).
        SetCredits(CurrentCredits - amount);
        return true;
    }

    public void AddCredits(int amount)
    {
        if (amount <= 0)
            return;

        SetCredits(CurrentCredits + amount);
    }

    public void SetCredits(int amount)
    {
        int safeAmount = Mathf.Max(0, amount);

        if (IsSpawned)
        {
            if (!IsServer)
                return;

            NetCredits.Value = safeAmount;
            MarketCreditBank.Set(safeAmount); // kasayi senkron tut — donuste bu bakiye okunur
        }
        else
        {
            currentCredits = safeAmount;
            MarketCreditBank.Set(safeAmount);
            CreditsChanged?.Invoke(currentCredits);
        }
    }

    public void ResetToStartingCredits()
    {
        SetCredits(startingCredits);
    }

    private void OnNetCreditsChanged(int previous, int current)
    {
        currentCredits = current;
        CreditsChanged?.Invoke(current);
    }
}
