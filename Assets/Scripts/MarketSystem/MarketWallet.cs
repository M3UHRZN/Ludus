using System;
using UnityEngine;

public class MarketWallet : MonoBehaviour
{
    [SerializeField] private int startingCredits = 100;
    [SerializeField] private int currentCredits;

    public int CurrentCredits => currentCredits;
    public event Action<int> CreditsChanged;

    private void Awake()
    {
        if (currentCredits <= 0 && startingCredits > 0)
            SetCredits(startingCredits);
    }

    public bool CanAfford(int amount)
    {
        return amount >= 0 && currentCredits >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount))
            return false;

        SetCredits(currentCredits - amount);
        return true;
    }

    public void AddCredits(int amount)
    {
        if (amount <= 0)
            return;

        SetCredits(currentCredits + amount);
    }

    public void SetCredits(int amount)
    {
        currentCredits = Mathf.Max(0, amount);
        CreditsChanged?.Invoke(currentCredits);
    }

    public void ResetToStartingCredits()
    {
        SetCredits(startingCredits);
    }
}
