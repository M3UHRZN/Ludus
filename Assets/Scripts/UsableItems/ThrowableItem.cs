using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Elde tutulup "use" ile arm edilen (pini çekilen), sonra normal fizik fırlatmayla
/// (sağtık) atılan usable. Arm anında fünye HEMEN başlar (cooking): vaktinde atmazsan
/// elinde patlar. Atıldıktan sonra yüzeye çarpınca da patlar. Alt sınıf OnDetonate yazar.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhysicsObject))]
public abstract class ThrowableItem : UsableItem
{
    [Header("Fuse")]
    [SerializeField] protected float fuseTime = 2.5f;
    [Tooltip("Fırlatıldıktan sonra yüzey temasıyla patlayabilmesi için geçen kısa süre (anında patlamayı önler).")]
    [SerializeField] protected float armTime = 0.2f;

    private Transform _throwerRoot;
    private PhysicsObject _physObj;

    // "use" = arm (pin çek). Base ServerActivate idempotent olduğu için bir kez çalışır.
    protected override void OnServerActivate(in UsableActivationContext context)
    {
        _physObj = GetComponent<PhysicsObject>();
        if (context.UserObject.TryGet(out NetworkObject userObject))
            _throwerRoot = userObject.transform;
        IgnoreThrowerCollisions();
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
        Vector3 point = lastPosition;
        float timer = 0f;
        float timeSinceThrow = -1f;   // <0 iken hâlâ elde

        while (timer < fuseTime)
        {
            timer += Time.deltaTime;
            Vector3 current = transform.position;

            bool held = _physObj != null && _physObj.NetIsHeld.Value;
            if (!held)
            {
                timeSinceThrow = timeSinceThrow < 0f ? 0f : timeSinceThrow + Time.deltaTime;
                if (timeSinceThrow >= armTime &&
                    Physics.Linecast(lastPosition, current, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore) &&
                    !IsSelfOrThrower(hit.collider))
                {
                    point = hit.point;
                    break;
                }
            }

            point = current;
            lastPosition = current;
            yield return null;
        }

        // Elde patlıyorsa tutan oyuncuyu temiz bırak.
        if (_physObj != null && _physObj.NetIsHeld.Value)
            _physObj.ServerStopHold();

        OnDetonate(point);
        ServerDespawnSelf();
    }

    private bool IsSelfOrThrower(Collider col)
    {
        if (col == null) return false;
        if (col.transform == transform || col.transform.IsChildOf(transform)) return true;
        if (_throwerRoot != null && (col.transform == _throwerRoot || col.transform.IsChildOf(_throwerRoot))) return true;
        return false;
    }

    protected abstract void OnDetonate(Vector3 point);
}
