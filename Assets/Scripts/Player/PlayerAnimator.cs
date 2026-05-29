using Unity.Netcode;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");

    private Animator _animator;
    private PlayerMovement _movement;
    private Vector3 _prevPos;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
    }

    public override void OnNetworkSpawn()
    {
        _movement.NetCrouching.OnValueChanged += OnCrouchChanged;
    }

    private void Start()
    {
        if (_animator == null)
        {
            Animator childAnimator = GetComponentInChildren<Animator>();
            if (childAnimator != null)
            {
                SetAnimator(childAnimator);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        _movement.NetCrouching.OnValueChanged -= OnCrouchChanged;
    }

    // PlayerAppearance, modeli spawn ettikten sonra bunu çağırır
    public void SetAnimator(Animator animator)
    {
        _animator = animator;
        _prevPos  = transform.position;
        _animator.SetBool(IsGroundedHash, true);
        _animator.SetBool(IsCrouchingHash, _movement.NetCrouching.Value);
    }

    private void OnCrouchChanged(bool prev, bool current)
    {
        if (_animator == null) return;
        _animator.SetBool(IsCrouchingHash, current);
    }

    private void Update()
    {
        if (_animator == null) return;

        _animator.SetBool(IsGroundedHash, _movement.NetGrounded.Value);

        float speed;
        if (IsOwner)
        {
            speed = _movement.CurrentSpeed;
        }
        else
        {
            speed = (transform.position - _prevPos).magnitude / Time.deltaTime;
            _prevPos = transform.position;
        }

        if (speed < 0.05f) speed = 0f;

        if (speed == 0f && _animator.GetFloat(SpeedHash) < 0.05f)
            _animator.SetFloat(SpeedHash, 0f);
        else
            _animator.SetFloat(SpeedHash, speed, 0.1f, Time.deltaTime);
    }
}
