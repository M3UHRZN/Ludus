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
    [SerializeField] private float _speed = 30f;
    [SerializeField] private float _lifetime = 3f;

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
        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
        else
            Destroy(gameObject);
    }
}
