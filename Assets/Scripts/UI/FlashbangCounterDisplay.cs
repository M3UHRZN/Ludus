using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// HUD slot UI yokken yedek olarak calisan sag alt "Flashbangs: N" gostergesi.
// Runtime'da kendi Canvas + TMP widget'ini olusturur, DontDestroyOnLoad ile sahneler arasi yasar.
public class FlashbangCounterDisplay : MonoBehaviour
{
    public static FlashbangCounterDisplay Instance { get; private set; }

    [SerializeField] private ushort _flashbangItemId = 100;
    [SerializeField] private string _format = "Flashbangs: {0}";
    [SerializeField] private bool _hideWhenZero = true;
    [SerializeField] private float _fontSize = 28f;
    [SerializeField] private Color _color = Color.white;
    [SerializeField] private Vector2 _bottomRightOffset = new Vector2(-30f, 30f);
    [SerializeField] private int _sortingOrder = 50;

    private GameObject _canvasGo;
    private TextMeshProUGUI _text;
    private int _lastCount = -1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildRuntimeWidget();
    }

    private void OnEnable()
    {
        GameEventBus.Subscribe<LocalInventoryUpdatedEvent>(OnInventoryUpdated);
        TryRefreshFromLocalInventory();
    }

    private void OnDisable()
    {
        GameEventBus.Unsubscribe<LocalInventoryUpdatedEvent>(OnInventoryUpdated);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_canvasGo != null) Destroy(_canvasGo);
    }

    private void BuildRuntimeWidget()
    {
        _canvasGo = new GameObject("FlashbangCounterCanvas");
        DontDestroyOnLoad(_canvasGo);

        var canvas = _canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;

        var scaler = _canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _canvasGo.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("FlashbangText");
        textGo.transform.SetParent(_canvasGo.transform, false);

        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.text = string.Empty;
        _text.fontSize = _fontSize;
        _text.color = _color;
        _text.alignment = TextAlignmentOptions.BottomRight;
        _text.raycastTarget = false;

        var rect = _text.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = _bottomRightOffset;
        rect.sizeDelta = new Vector2(400f, 60f);
    }

    private void OnInventoryUpdated(LocalInventoryUpdatedEvent evt)
    {
        if (_text == null) return;

        int count = 0;
        if (evt.ItemIds != null)
        {
            for (int i = 0; i < evt.ItemIds.Length; i++)
            {
                if (evt.ItemIds[i] == _flashbangItemId) count++;
            }
        }

        if (count == _lastCount) return;
        _lastCount = count;

        if (count <= 0 && _hideWhenZero)
        {
            _text.text = string.Empty;
        }
        else
        {
            _text.text = string.Format(_format, count);
        }
    }

    // Sahne ilk yuklendiginde event'leri kacirmamak icin local oyuncudan mevcut envanteri oku.
    private void TryRefreshFromLocalInventory()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;

        var inv = nm.LocalClient.PlayerObject.GetComponent<PlayerInventory>();
        if (inv == null) return;

        int count = 0;
        for (int i = 0; i < inv.Slots.Count; i++)
        {
            if (inv.Slots[i] == _flashbangItemId) count++;
        }

        _lastCount = count;
        if (_text == null) return;
        _text.text = (count <= 0 && _hideWhenZero) ? string.Empty : string.Format(_format, count);
    }
}
