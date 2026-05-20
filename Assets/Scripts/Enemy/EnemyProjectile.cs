using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Type A dusmanin attigi gorsel mermi. Server'da ileri hareket eder,
/// NetworkTransform ile tum client'lara senkronlanir. Bir IDamageable'a
/// veya engele carpinca hasar verip despawn olur.
///
/// Prefab gereksinimleri:
///   - NetworkObject + NetworkTransform (pozisyon sync)
///   - Collider (Is Trigger = true)
///   - Bu script (EnemyProjectile)
///   - Gorsel: kucuk Sphere mesh + emissive materyal (parlasin)
/// DefaultNetworkPrefabs listesine eklenmeli.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class EnemyProjectile : NetworkBehaviour
{
    [Tooltip("Mermi hizi — dusuk deger gorunur ve kacinilabilir mermi (gerceklik icin 15-20)")]
    [SerializeField] private float _speed = 18f;

    [Tooltip("Merminin maks. omru (saniye)")]
    [SerializeField] private float _lifetime = 4f;

    [Tooltip("Carpma aninda spawn olan efekt prefab'i (opsiyonel — kivilcim/patlama)")]
    [SerializeField] private GameObject _impactEffectPrefab;

    private Vector3 _direction;
    private float _damage;
    private float _age;
    private bool _launched;

    /// <summary>Server, spawn'dan hemen sonra cagirir: yon ve hasar verir.</summary>
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
            Debug.Log($"[EnemyProjectile] Oyuncuya isabet: {_damage} hasar.");
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

        // Carpma efekti (opsiyonel) — herkeste gorunmesi icin tum client'lara bildir
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
