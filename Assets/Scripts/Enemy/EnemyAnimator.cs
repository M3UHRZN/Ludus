using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Enemy'nin NavMeshAgent hizini Animator'in "Speed" parametresine aktarir.
/// Boylece dusman hareket ederken yurume/kosma, dururken idle animasyonu oynar.
///
/// Animator Controller'da bir "Speed" (float) parametresi ve buna bagli
/// Idle/Walk/Run gecisleri (veya Blend Tree) olmali. Mevcut PlayerAnimController
/// de "Speed" parametresi kullaniyor, gecici olarak o da atanabilir.
///
/// Not: Enemy su an server-only kostugu icin (EnemyController IsServer guard),
/// animasyon host tarafinda dogru oynar. Tam client sync, NetworkBehaviour
/// migrasyonunda NetworkAnimator ile cozulecek.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAnimator : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    [Tooltip("Bos birakilirsa child'larda otomatik Animator aranir.")]
    [SerializeField] private Animator _animator;

    [Tooltip("Speed parametresinin yumusatma suresi (ani gecisleri onler).")]
    [SerializeField] private float _damping = 0.12f;

    [Tooltip("NavMeshAgent hizini Animator'a aktarirken carpan. Animator'in " +
             "walk/run esikleriyle uyumlu olacak sekilde ayarla.")]
    [SerializeField] private float _speedMultiplier = 1f;

    private NavMeshAgent _agent;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (_animator == null || _agent == null) return;

        // Agent disable ise (client tarafi) hareket okunamaz, idle'da kal
        float speed = _agent.enabled ? _agent.velocity.magnitude * _speedMultiplier : 0f;

        _animator.SetFloat(SpeedHash, speed, _damping, Time.deltaTime);
    }
}
