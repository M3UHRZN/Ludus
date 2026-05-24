using System;
using UnityEngine;

/// <summary>
/// Lightweight runtime projectile for local flashbang testing.
/// Uses initial velocity and gravity instead of a fake scripted arc.
/// </summary>
public class FlashbangProjectile : MonoBehaviour
{
    private Vector3 _velocity;
    private float _gravity;
    private float _maxLifetime;
    private float _elapsed;
    private Action<Vector3> _onExplode;

    public static FlashbangProjectile Create(
        string objectName,
        Vector3 start,
        Vector3 initialVelocity,
        float gravity,
        float maxLifetime,
        Action<Vector3> onExplode,
        Color color)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = objectName;
        obj.transform.position = start;
        obj.transform.localScale = Vector3.one * 0.3f;

        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
            UnityEngine.Object.Destroy(collider);

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(renderer.sharedMaterial);
            material.color = color;
            renderer.material = material;
        }

        FlashbangProjectile projectile = obj.AddComponent<FlashbangProjectile>();
        projectile.Initialize(initialVelocity, gravity, maxLifetime, onExplode);
        return projectile;
    }

    private void Initialize(
        Vector3 initialVelocity,
        float gravity,
        float maxLifetime,
        Action<Vector3> onExplode)
    {
        _velocity = initialVelocity;
        _gravity = Mathf.Max(0f, gravity);
        _maxLifetime = Mathf.Max(0.1f, maxLifetime);
        _onExplode = onExplode;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector3 previousPosition = transform.position;

        _elapsed += dt;
        _velocity += Vector3.down * _gravity * dt;
        Vector3 nextPosition = previousPosition + _velocity * dt;

        if (Physics.Linecast(previousPosition, nextPosition, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point;
            Explode(hit.point);
            return;
        }

        transform.position = nextPosition;

        if (_velocity.sqrMagnitude > 0.0001f)
            transform.forward = _velocity.normalized;

        if (_elapsed >= _maxLifetime || transform.position.y <= -10f)
            Explode(transform.position);
    }

    private void Explode(Vector3 point)
    {
        _onExplode?.Invoke(point);
        Destroy(gameObject);
    }
}
