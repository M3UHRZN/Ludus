using UnityEngine;

/// <summary>
/// Local flashbang enemy for isolated testing.
/// Throws a flashbang at the player every few seconds unless blinded.
/// </summary>
public class FlashbangLocalEnemy : MonoBehaviour
{
    [SerializeField] private Transform playerTarget;
    [SerializeField] private FlashbangEffect playerEffect;
    [SerializeField] private float throwInterval = 10f;
    [SerializeField] private float throwSpeed = 12f;
    [SerializeField] private float throwGravity = 18f;
    [SerializeField] private float maxThrowLifetime = 1.8f;
    [SerializeField] private float blastRadius = 3f;
    [SerializeField] private float inflictedBlindDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float inflictedPeakAlpha = 1f;

    private float _nextThrowTime;
    private float _blindUntil;
    private Renderer _renderer;
    private Color _baseColor;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _baseColor = _renderer.material.color;

        _nextThrowTime = Time.time + throwInterval;
    }

    private void Update()
    {
        UpdateVisual();

        if (playerTarget == null || playerEffect == null)
            return;

        if (Time.time < _blindUntil)
            return;

        transform.LookAt(new Vector3(playerTarget.position.x, transform.position.y, playerTarget.position.z));

        if (Time.time >= _nextThrowTime)
        {
            ThrowAtPlayer();
            _nextThrowTime = Time.time + throwInterval;
        }
    }

    public void SetPlayerTarget(Transform target)
    {
        playerTarget = target;
    }

    public void SetPlayerEffect(FlashbangEffect effect)
    {
        playerEffect = effect;
    }

    public void ApplyBlind(float duration)
    {
        _blindUntil = Mathf.Max(_blindUntil, Time.time + Mathf.Max(0.1f, duration));
    }

    private void ThrowAtPlayer()
    {
        Vector3 start = transform.position + Vector3.up * 1.1f;
        Vector3 target = playerTarget.position + Vector3.up * 1f;
        Vector3 initialVelocity = (target - start).normalized * throwSpeed;

        FlashbangProjectile.Create(
            "EnemyFlashbang",
            start,
            initialVelocity,
            throwGravity,
            maxThrowLifetime,
            ExplodeEnemyFlashbang,
            new Color(1f, 0.8f, 0.25f, 1f));
    }

    private void ExplodeEnemyFlashbang(Vector3 explosionPoint)
    {
        if (playerTarget == null || playerEffect == null)
            return;

        Vector3 playerPoint = playerTarget.position + Vector3.up;
        if (Vector3.Distance(explosionPoint, playerPoint) <= blastRadius)
            playerEffect.TriggerFlashbang(inflictedBlindDuration, inflictedPeakAlpha);
    }

    private void UpdateVisual()
    {
        if (_renderer == null)
            return;

        if (Time.time < _blindUntil)
            _renderer.material.color = Color.Lerp(_baseColor, Color.white, 0.65f);
        else
            _renderer.material.color = _baseColor;
    }
}
