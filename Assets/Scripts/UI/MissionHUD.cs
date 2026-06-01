using TMPro;
using UnityEngine;
using Ludus.Extraction.Core;

/// <summary>
/// Mevcut mission / kota / banka / run sayacini gosteren minimal HUD.
/// Mission durumu: acilista MissionState (host) veya MissionConfig.Default'tan, run bitince
/// MissionStateEvent ile guncellenir. Banka: lobide market'le AYNI canli kaynaktan
/// (MarketWallet.CreditsChanged / CurrentCredits — bkz MarketUIController). Run icinde wallet
/// olmadigindan banka son run-sonu degerinde kalir.
/// </summary>
public class MissionHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    // Mission gosterim durumu (cache).
    private int _mission = 1;
    private int _quota;
    private int _runsInMission;
    private int _runsPerMission = 3;
    private MissionResult _result = MissionResult.Continue;

    // Banka: -1 = bilinmiyor (gizlenir). Lobide MarketWallet'tan canli gelir.
    private int _bank = -1;
    private MarketWallet _wallet;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<MissionStateEvent>(OnMissionState);
        InitMissionFields();
        Render();
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<MissionStateEvent>(OnMissionState);
        if (_wallet != null) _wallet.CreditsChanged -= OnBankChanged;
        _wallet = null;
    }

    private void Update()
    {
        // MarketWallet runtime'da (MarketRuntimeBootstrap) kuruluyor; lobide bulununca bagla.
        // Wallet despawn olunca (sahne degisimi) _wallet fake-null olur ve yeniden aranir.
        if (_wallet == null)
        {
            MarketWallet w = FindFirstObjectByType<MarketWallet>();
            if (w != null) BindWallet(w);
        }
    }

    /// <summary>Acilista mevcut mission durumunu doldur (run beklemeden).</summary>
    private void InitMissionFields()
    {
        if (MissionState.HasValue)
        {
            MissionSnapshot s = MissionState.Snapshot;
            _mission = s.Mission;
            _quota = s.Quota;
            _runsInMission = s.RunsInMission;
            _runsPerMission = MissionConfig.Default.RunsPerMission;
        }
        else
        {
            MissionConfig cfg = MissionConfig.Default;
            _mission = 1;
            _quota = MissionQuota.For(1, cfg);
            _runsInMission = 0;
            _runsPerMission = cfg.RunsPerMission;
        }
        _result = MissionResult.Continue;
    }

    private void BindWallet(MarketWallet w)
    {
        _wallet = w;
        _wallet.CreditsChanged += OnBankChanged;
        OnBankChanged(_wallet.CurrentCredits);
    }

    private void OnBankChanged(int credits)
    {
        _bank = credits;
        Render();
    }

    private void OnMissionState(MissionStateEvent e)
    {
        _mission = e.Mission;
        _quota = e.Quota;
        _runsInMission = e.RunsInMission;
        _runsPerMission = e.RunsPerMission;
        _result = e.Result;
        if (e.BankCredits >= 0) _bank = e.BankCredits; // run-sonu: wallet yokken banka kaynagi
        Render();
    }

    private void Render()
    {
        if (label == null) return;

        string suffix = _result switch
        {
            MissionResult.Cleared => "  <color=#5f5>KOTA DOLDU!</color>",
            MissionResult.Failed  => "  <color=#f55>BASARISIZ — SIFIRLANDI</color>",
            _ => string.Empty
        };

        string bank = string.Empty;
        if (_bank >= 0)
        {
            // Kota dolduysa banka degerini yesil goster (tek bakista "doldurduk mu" okunur).
            string col = _bank >= _quota ? "#5f5" : "#fff";
            bank = $" — Banka: <color={col}>{_bank}</color>";
        }

        label.text = $"Mission {_mission} — Kota: {_quota}{bank} — Run {_runsInMission}/{_runsPerMission}{suffix}";
    }
}
