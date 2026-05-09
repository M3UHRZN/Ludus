// Assets/Scripts/UI/WeightUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays current carry weight and speed penalty on HUD.
/// </summary>
public class WeightUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _weightText;
    [SerializeField] private Slider          _weightBar;
    [SerializeField] private PlayerInventory _inventory;
    [SerializeField] private WeightSystem    _weightSystem;
    [SerializeField] private float           _maxWeight = 10f;

    private void Update()
    {
        if (_inventory == null || _weightSystem == null) return;

        int total = _weightSystem.CalculateTotalWeight(_inventory);

        if (_weightText != null)
            _weightText.text = $"Weight: {total} / {(int)_maxWeight}";

        if (_weightBar != null)
            _weightBar.value = Mathf.Clamp01((float)total / _maxWeight);
    }
}