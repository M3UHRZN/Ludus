using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple standalone trigger for the flashbang prototype.
/// Press G in play mode to trigger the local flashbang feedback.
/// </summary>
public class FlashbangTestController : MonoBehaviour
{
    [SerializeField] private FlashbangEffect flashbangEffect;
    [SerializeField] private FlashbangLocalEnemy targetEnemy;
    [SerializeField] private float testBlindDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float testPeakAlpha = 1f;
    [SerializeField] private float throwSpeed = 14f;
    [SerializeField] private float throwGravity = 18f;
    [SerializeField] private float maxThrowLifetime = 1.5f;
    [SerializeField] private float blastRadius = 3f;

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.gKey.wasPressedThisFrame)
            ThrowFlashbangAtEnemy();
    }

    public void TriggerTestFlashbang()
    {
        if (flashbangEffect == null)
        {
            Debug.LogWarning("[FlashbangTestController] FlashbangEffect reference is missing.");
            return;
        }

        flashbangEffect.TriggerFlashbang(testBlindDuration, testPeakAlpha);
    }

    public void SetFlashbangEffect(FlashbangEffect effect)
    {
        flashbangEffect = effect;
    }

    public void SetTargetEnemy(FlashbangLocalEnemy enemy)
    {
        targetEnemy = enemy;
    }

    public void ThrowFlashbangAtEnemy()
    {
        Vector3 start = transform.position + Vector3.up * 1.2f;
        Transform aimTransform = Camera.main != null ? Camera.main.transform : transform;
        Vector3 throwDirection = aimTransform.forward.normalized;
        if (throwDirection.sqrMagnitude < 0.0001f)
            throwDirection = transform.forward;
        Vector3 initialVelocity = throwDirection * throwSpeed;

        FlashbangProjectile.Create(
            "PlayerFlashbang",
            start,
            initialVelocity,
            throwGravity,
            maxThrowLifetime,
            ExplodePlayerFlashbang,
            new Color(0.5f, 0.85f, 1f, 1f));
    }

    private void ExplodePlayerFlashbang(Vector3 explosionPoint)
    {
        if (targetEnemy != null)
        {
            Vector3 enemyPoint = targetEnemy.transform.position + Vector3.up;
            if (Vector3.Distance(explosionPoint, enemyPoint) <= blastRadius)
                targetEnemy.ApplyBlind(testBlindDuration);
        }

        Vector3 playerPoint = transform.position + Vector3.up;
        if (Vector3.Distance(explosionPoint, playerPoint) <= blastRadius)
            TriggerTestFlashbang();
    }
}
