using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] private float triggerRange = 3f;

    private Animator _animator;
    private static readonly int NearbyHash = Animator.StringToHash("character_nearby");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
            _animator = GetComponentInParent<Animator>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        var col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(triggerRange, 3f, triggerRange);
        col.center = new Vector3(0f, 1.5f, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_animator != null && other.GetComponent<CharacterController>() != null)
            _animator.SetBool(NearbyHash, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (_animator != null && other.GetComponent<CharacterController>() != null)
            _animator.SetBool(NearbyHash, false);
    }
}
