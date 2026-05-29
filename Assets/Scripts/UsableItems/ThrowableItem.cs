using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Aktivasyonda fırlatılan usable: impuls uygular, çarpışma linecast'li bir fünye
/// çalıştırır, çarpma noktasında OnDetonate'i çağırıp despawn olur. Alt sınıflar
/// (Flashbang, ...) sadece OnDetonate'i uygular.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class ThrowableItem : UsableItem
{
    [Header("Throw")]
    [SerializeField] protected float throwSpeed = 16f;
    [SerializeField] protected float upwardBoost = 1.5f;
    [SerializeField] protected float fuseTime = 1.6f;
    [Tooltip("Yüzey temasında patlayabilmesi için geçmesi gereken süre — atan kişiyi geçsin diye.")]
    [SerializeField] protected float armTime = 0.2f;

    private Transform _throwerRoot;

    protected override void OnServerActivate(in UsableActivationContext context)
    {
        Vector3 direction = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : transform.forward;

        if (context.UserObject.TryGet(out NetworkObject userObject))
            _throwerRoot = userObject.transform;
        IgnoreThrowerCollisions();

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce((direction * throwSpeed) + (Vector3.up * upwardBoost), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
        }

        StartCoroutine(ServerFuseRoutine());
    }

    private void IgnoreThrowerCollisions()
    {
        if (_throwerRoot == null) return;
        Collider own = GetComponent<Collider>();
        if (own == null) return;
        foreach (Collider c in _throwerRoot.GetComponentsInChildren<Collider>())
            if (c != null) Physics.IgnoreCollision(own, c, true);
    }

    private IEnumerator ServerFuseRoutine()
    {
        Vector3 lastPosition = transform.position;
        Vector3 explosionPoint = lastPosition;
        float timer = 0f;

        while (timer < fuseTime)
        {
            timer += Time.deltaTime;
            Vector3 currentPosition = transform.position;

            if (timer >= armTime &&
                Physics.Linecast(lastPosition, currentPosition, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                !IsSelfOrThrower(hit.collider))
            {
                explosionPoint = hit.point;
                break;
            }

            explosionPoint = currentPosition;
            lastPosition = currentPosition;
            yield return null;
        }

        OnDetonate(explosionPoint);
        ServerDespawnSelf();
    }

    private bool IsSelfOrThrower(Collider col)
    {
        if (col == null) return false;
        if (col.transform == transform || col.transform.IsChildOf(transform)) return true;
        if (_throwerRoot != null && (col.transform == _throwerRoot || col.transform.IsChildOf(_throwerRoot))) return true;
        return false;
    }

    /// <summary>Server-only. Çarpma noktasında bir kez çağrılır. Etkini burada uygula.</summary>
    protected abstract void OnDetonate(Vector3 point);
}
