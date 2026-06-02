using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Local oyuncunun can barini gosterir. `PlayerStateMachine.NetHealth`
/// NetworkVariable'a abone olur; damage / heal / revive durumlarinda otomatik
/// guncellenir. Stamina UI pattern'ini takip eder: Image.fillAmount + renk
/// degisimi (dusuk HP'de kirmizi).
///
/// Local player spawn olmadan once aktif olabilir (HUD prefab'i sahnede sabit).
/// Bu yuzden Lazy binding kullanir: Update'te local player'i poll eder,
/// bulunca abone olur. Player despawn olunca abonelikten cikar.
/// </summary>
public class HealthBarUIController : MonoBehaviour
{
    [Header("Can Bari Ayarlari")]
    [Tooltip("Doldurulan/bosalan kirmizi bar (Image.Type = Filled).")]
    [SerializeField] private Image healthFillImage;

    [Header("Renkler")]
    [Tooltip("Normal can rengi.")]
    [SerializeField] private Color healthyColor = new Color(0.85f, 0.2f, 0.2f, 1f);
    [Tooltip("Dusuk HP rengi (lowHealthThreshold altinda).")]
    [SerializeField] private Color lowHealthColor = new Color(0.5f, 0.0f, 0.0f, 1f);
    [Tooltip("Bu orandan dusuk fillAmount low renge gecirir (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float lowHealthThreshold = 0.3f;

    [Header("Bind")]
    [Tooltip("Local oyuncuyu bulamazsak bu kadar saniyede bir tekrar dene.")]
    [SerializeField] private float rebindInterval = 0.5f;

    private PlayerStateMachine _boundPlayer;
    private float _nextRebindTime;

    private void OnEnable()
    {
        TryBindLocalPlayer();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void Update()
    {
        // Local player henuz spawn olmadiysa veya despawn oldu ise yeniden bind dene.
        if (_boundPlayer == null && Time.time >= _nextRebindTime)
        {
            _nextRebindTime = Time.time + rebindInterval;
            TryBindLocalPlayer();
        }
    }

    private void TryBindLocalPlayer()
    {
        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null)
            return;

        PlayerStateMachine player = nm.LocalClient.PlayerObject.GetComponent<PlayerStateMachine>();
        if (player == null) return;

        Bind(player);
    }

    private void Bind(PlayerStateMachine player)
    {
        if (_boundPlayer == player) return;
        Unbind();

        _boundPlayer = player;
        _boundPlayer.NetHealth.OnValueChanged += OnHealthChanged;

        // Mevcut deger ile bar'i sifirdan cizdir.
        Refresh(_boundPlayer.NetHealth.Value, _boundPlayer.MaxHealth);
    }

    private void Unbind()
    {
        if (_boundPlayer == null) return;
        _boundPlayer.NetHealth.OnValueChanged -= OnHealthChanged;
        _boundPlayer = null;
    }

    private void OnHealthChanged(float previous, float current)
    {
        float max = _boundPlayer != null ? _boundPlayer.MaxHealth : 100f;
        Refresh(current, max);
    }

    private void Refresh(float current, float max)
    {
        if (healthFillImage == null) return;

        float ratio = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        healthFillImage.fillAmount = ratio;
        healthFillImage.color = ratio <= lowHealthThreshold ? lowHealthColor : healthyColor;
    }
}
