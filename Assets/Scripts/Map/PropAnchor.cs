using UnityEngine;

// Floor prefabının içine konan boş empty'lere eklenir; bir prop slotunu işaretler.
// Transform'un pozisyon+rotasyonu prop'un yerini ve baktığı yönü belirler.
public class PropAnchor : MonoBehaviour
{
    [Tooltip("Bu slota hangi kategorideki modeller gelebilir")]
    public PropCategory category = PropCategory.Floor;

    private void OnDrawGizmos()
    {
        Gizmos.color = category switch
        {
            PropCategory.Floor   => new Color(0.2f, 0.8f, 1f,  0.9f),
            PropCategory.Wall    => new Color(1f,   0.7f, 0.1f, 0.9f),
            PropCategory.Corner  => new Color(0.6f, 0.2f, 1f,  0.9f),
            PropCategory.Ceiling => new Color(1f,   0.2f, 0.5f, 0.9f),
            _                    => Color.white
        };
        Gizmos.DrawSphere(transform.position, 0.2f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.6f);
    }
}
