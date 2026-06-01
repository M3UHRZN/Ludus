using Unity.Netcode;
using UnityEngine;

// Type A dusman mermisi. Server hareket, NetworkTransform sync, IDamageable carpinca hasar + slow.
[RequireComponent(typeof(NetworkObject))]
public class EnemyProjectile : NetworkBehaviour
{
    [SerializeField] private float _speed = 18f;
    [SerializeField] private float _lifetime = 4f;
    [SerializeField] private GameObject _impactEffectPrefab;

    [Header("Slow Effect")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _slowMultiplier = 0.55f;
    [SerializeField] private float _slowDuration = 1.5f;

    private Vector3 _direction;
    private float _damage;
    private float _age;
    private bool _launched;

    // Server spawn sonrasi cagirir, yon + hasar verir
    public void Launch(Vector3 direction, float damage)
    {
        _direction = direction.normalized;
        _damage = damage;
        _launched = true;
    }

    private void Update()
    {
        // Hareket ve hasar yalnizca server'da; client'lar NetworkTransform ile gorur
        if (!IsServer || !_launched) return;

        transform.position += _direction * (_speed * Time.deltaTime);

        _age += Time.deltaTime;
        if (_age >= _lifetime)
            Despawn();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !_launched) return;

        // Baska bir enemy'ye veya kendine carparsa yok say
        if (other.GetComponentInParent<EnemyController>() != null) return;

        var dmg = other.GetComponent<IDamageable>() ?? other.GetComponentInParent<IDamageable>();
        if (dmg != null && dmg.IsAlive)
        {
            dmg.TakeDamage(_damage, transform.position, 0UL);

            // Hasarin yaninda kisa slow uygula
            var slow = other.GetComponent<ISlowable>() ?? other.GetComponentInParent<ISlowable>();
            slow?.ApplySlow(_slowMultiplier, _slowDuration);

            Debug.Log($"[EnemyProjectile] Oyuncuya isabet: {_damage} hasar + slow ({_slowMultiplier:F2}x, {_slowDuration:F1}s).");
            Despawn();
            return;
        }

        // Trigger olmayan kati bir yuzeye (duvar/zemin) carptiysa yok ol
        if (!other.isTrigger)
            Despawn();
    }

    private void Despawn()
    {
        _launched = false;

        // Carpma efekti tum client'lara
        if (_impactEffectPrefab != null)
            SpawnImpactRpc(transform.position);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }

    [Rpc(SendTo.Everyone)]
    private void SpawnImpactRpc(Vector3 position)
    {
        if (_impactEffectPrefab == null) return;
        var fx = Instantiate(_impactEffectPrefab, position, Quaternion.identity);
        Destroy(fx, 1.5f);
    }
}
