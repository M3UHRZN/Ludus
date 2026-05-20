using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Visualizes the fear system state and provides test buttons.
/// Attach to a panel under Canvas.
/// Wire up test buttons via OnClick: AddFear10(), AddFear30(), ResetFear()
/// </summary>
public class FearSystemUI : MonoBehaviour
{
    [Header("Reference")]
    public FearSystem fearSystem;

    [Header("UI Elements")]
    [Tooltip("Fear bar image — set Fill Method to Horizontal")]
    public Image fearBar;

    [Tooltip("Displays fear percentage (TextMeshPro)")]
    public TextMeshProUGUI fearText;

    [Tooltip("Shown only during panic")]
    public GameObject panicLabel;

    [Header("Bar Colors")]
    public Color safeColor  = Color.green;
    public Color midColor   = Color.yellow;
    public Color panicColor = Color.red;

    private void Update()
    {
        if (fearSystem == null) return;

        float t = fearSystem.FearLevel / 100f;

        // Fill bar
        if (fearBar != null)
            fearBar.fillAmount = t;

        // Bar color gradient
        if (fearBar != null)
        {
            Color barColor = t < 0.5f
                ? Color.Lerp(safeColor, midColor, t * 2f)
                : Color.Lerp(midColor, panicColor, (t - 0.5f) * 2f);
            fearBar.color = barColor;
        }

        // Percentage text
        if (fearText != null)
            fearText.text = $"Fear: {fearSystem.FearLevel:F0}%";

        // Panic label visibility
        if (panicLabel != null)
            panicLabel.SetActive(fearSystem.IsInPanic);
    }

    // Button callbacks
    public void AddFear10() => fearSystem?.AddFear(10f);
    public void AddFear30() => fearSystem?.AddFear(30f);
    public void ResetFear() => fearSystem?.ResetFear();

    /// <summary>Simulates a nearby player death — triggers FearSystem death witness reaction.</summary>
    public void SimulateNearbyDeath()
    {
        if (fearSystem == null) return;
        GameEventBus.Publish(new PlayerDiedEvent(99, fearSystem.transform.position));
    }

    /// <summary>Instantly triggers panic by maxing out fear.</summary>
    public void SimulatePanic()
    {
        fearSystem?.AddFear(100f);
    }
}
