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
        if (!IsSpawned && currentCredits <= 0 && startingCredits > 0)
            SetCredits(startingCredits);
    }

    public override void OnNetworkSpawn()
    {
        NetCredits.OnValueChanged += OnNetCreditsChanged;

        if (IsServer && NetCredits.Value <= 0 && startingCredits > 0)
            SetCredits(startingCredits);

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
        }
        else
        {
            currentCredits = safeAmount;
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
