using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bagimsiz, kucuk bir "Flashbangs: N" gostergesi. Esmanur'un HUD slot
/// sistemine paralel calisir; oyuncu lobby veya gameplay sahnesinde ne
/// kadar flashbang tasidigini ekranin sag alt kosesinde net olarak gorur.
///
/// Kullanim:
/// - Bu component'i sahnedeki herhangi bir GameObject'e ekle. Ilk Awake'te
///   kendi Canvas'ini + TextMeshPro widget'ini runtime'da olusturur.
/// - Singleton: birden fazla sahnede zaten varsa duplicate eden kendini yok eder.
/// - Sahneler arasi yasayabilmesi icin DontDestroyOnLoad ile isaretlenir;
///   boylece tek bir Lobby placement bile lobby + RNGmap'te calisir.
/// - LocalInventoryUpdatedEvent'i dinler, slotlardaki itemId == _flashbangItemId
///   sayisini sayar ve "Flashbangs: N" formatinda yazar. Sayi sifirsa yaziyi gizler.
/// </summary>
public class FlashbangCounterDisplay : MonoBehaviour
{
    public static FlashbangCounterDisplay Instance { get; private set; }

    [Header("Item")]
    [Tooltip("Sayilacak item ID. PlayerInventory.flashbangItemId ile ayni olmali (default 100).")]
    [SerializeField] private ushort _flashbangItemId = 100;

    [Header("Gorunum")]
    [Tooltip("Bicim string'i — {0} flashbang sayisi ile dolusur.")]
    [SerializeField] private string _format = "Flashbangs: {0}";

    [Tooltip("Yazi sayi 0 oldugunda gizlensin mi?")]
    [SerializeField] private bool _hideWhenZero = true;

    [Tooltip("Yazi font boyutu")]
    [SerializeField] private float _fontSize = 28f;

    [Tooltip("Yazi rengi")]
    [SerializeField] private Color _color = Color.white;

    [Tooltip("Sag alt kosege offset (pixel)")]
    [SerializeField] private Vector2 _bottomRightOffset = new Vector2(-30f, 30f);

    [Tooltip("Canvas sorting order — yuksek ise diger UI'larin ustunde gozukur.")]
    [SerializeField] private int _sortingOrder = 50;

    private GameObject _canvasGo;
    private TextMeshProUGUI _text;
    private int _lastCount = -1;

    private void Awake()
    {
        // Singleton koruma
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
        // Sahne degisiminde yeni PlayerInventory daha event publishlemis olabilir;
        // local oyuncudan mevcut durumu bir kez sorgula.
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

        if (count == _lastCount) return; // gereksiz UI guncellemesi yok
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

    /// <summary>
    /// Sahne yuklendikten hemen sonra PlayerInventory event'i fire etmis olabilir;
    /// local oyuncu objesinden mevcut Slots durumunu hesaplayip uygula.
    /// </summary>
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
