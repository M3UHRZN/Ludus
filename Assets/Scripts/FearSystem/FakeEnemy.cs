using UnityEngine;

/// <summary>
/// Fake enemy for FearSystem testing.
/// Walks toward the target player. No real AI required.
/// </summary>
public class FakeEnemy : MonoBehaviour
{
    [Tooltip("Target player transform")]
    public Transform target;

    [Tooltip("Movement speed")]
    public float speed = 2f;

    [Tooltip("Stop at this distance from target")]
    public float stopDistance = 2f;

    [Tooltip("Enable/disable movement")]
    public bool moveEnabled = true;

    private void Update()
    {
        if (!moveEnabled || target == null) return;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= stopDistance) return;

        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0f;
        transform.position += dir * speed * Time.deltaTime;
        transform.LookAt(new Vector3(target.position.x, transform.position.y, target.position.z));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
    }
}
