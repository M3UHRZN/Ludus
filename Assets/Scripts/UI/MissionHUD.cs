using TMPro;
using UnityEngine;
using Ludus.Extraction.Core;

/// <summary>
/// Mevcut mission / kota / run sayacini gosteren minimal HUD. MissionStateEvent ile guncellenir.
/// label bos birakilirsa kendi uzerindeki TMP_Text'i kullanir. (Run-sonu broadcast'ine dayanir;
/// oturumun ilk run'indan once placeholder gosterir.)
/// </summary>
public class MissionHUD : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        if (label != null) label.text = "Mission 1 — Kota: ? — Run 0/3";
    }

    private void OnEnable()  => GameEventBus.Subscribe<MissionStateEvent>(OnMissionState);
    private void OnDisable() => GameEventBus.Unsubscribe<MissionStateEvent>(OnMissionState);

    private void OnMissionState(MissionStateEvent e)
    {
        if (label == null) return;

        string suffix = e.Result switch
        {
            MissionResult.Cleared => "  <color=#5f5>KOTA DOLDU!</color>",
            MissionResult.Failed  => "  <color=#f55>BASARISIZ — SIFIRLANDI</color>",
            _ => string.Empty
        };

        label.text = $"Mission {e.Mission} — Kota: {e.Quota} — Banka: {e.BankCredits} — Run {e.RunsInMission}/{e.RunsPerMission}{suffix}";
    }
}
