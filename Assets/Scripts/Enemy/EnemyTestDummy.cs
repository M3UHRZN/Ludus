using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// AlpTest.unity sahnesi icin yardimci Player taklidi.
/// Network gerektirmez, sadece IDamageable implement eder ki AttackBehavior
/// hasar verebilsin. Ayrica F tusu ile yakindaki dusmanlara flashbang
/// (SetBlinded) tetikleyerek FleeBehavior'i test eder.
/// </summary>
public class EnemyTestDummy : MonoBehaviour, IDamageable
{
    [Header("Saglik")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Flashbang Test")]
    [Tooltip("F tusuna basinca bu yaricapta dusmanlara SetBlinded cagirilir")]
    [SerializeField] private float flashRadius = 8f;
    [SerializeField] private float blindDuration = 3f;

    [Header("Noise Test")]
    [Tooltip("N tusuna basinca burada belirtilen menzilde NoiseEmittedEvent yayinlanir")]
    [SerializeField] private float noiseRange = 25f;

    public bool IsAlive => currentHealth > 0f;
    public float CurrentHealth => currentHealth;

    public void TakeDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (!IsAlive) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        Debug.Log($"[TestDummy] Hasar: {amount} | Kalan HP: {currentHealth}/{maxHealth}");

        if (!IsAlive)
            Debug.Log("[TestDummy] Oldu.");
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // F = yakindaki enemy'lere flashbang (FleeBehavior testi)
        if (kb.fKey.wasPressedThisFrame)
            TriggerFlashbang();

        // H = kendine 25 hasar (HUD/death testi)
        if (kb.hKey.wasPressedThisFrame)
            TakeDamage(25f, transform.position, 0UL);

        // R = HP geri yukle
        if (kb.rKey.wasPressedThisFrame)
        {
            currentHealth = maxHealth;
            Debug.Log($"[TestDummy] HP geri yuklendi: {currentHealth}");
        }

        // N = ses yayinla (Patrol sound reactivity testi)
        if (kb.nKey.wasPressedThisFrame)
            TriggerNoise();
    }

    private void TriggerFlashbang()
    {
        var hits = Physics.OverlapSphere(transform.position, flashRadius);
        int count = 0;

        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                enemy.SetBlinded(true, blindDuration);
                count++;
            }
        }

        Debug.Log($"[TestDummy] Flashbang tetiklendi, etkilenen dusman: {count}");
    }

    private void TriggerNoise()
    {
        // Ses kaynagi olarak sahnedeki "Player" tag'li objeyi kullan
        // (TestController'in kendi konumu degil, gercek oyuncu konumu mantikli)
        var playerObj = GameObject.FindWithTag("Player");
        Vector3 source = playerObj != null ? playerObj.transform.position : transform.position;

        GameEventBus.Publish(new NoiseEmittedEvent(source, noiseRange, "TestDummy"));
        Debug.Log($"[TestDummy] Ses yayinlandi (kaynak={source}, menzil={noiseRange}).");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawSphere(transform.position, flashRadius);
    }
#endif
}
