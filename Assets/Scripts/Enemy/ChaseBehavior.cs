using UnityEngine;

/// <summary>
/// Oyuncuyu kovalar. Gorus varsa dogrudan kovalar, menzile girince saldiriya gecer
/// (Type A ranged / Type B melee). Gorus kaybolursa once son gorulen yere (ya da ses
/// kaynagina) gider, orada bir sure etrafa bakar, bulamazsa devriyeye/gezmeye doner.
///
/// "Once ize git, sonra vazgec" yaklasimi hem gercekci stealth (oyuncu gorusu kesip
/// saklanirsa dusman vazgecer) hem de Patrol&lt;-&gt;Chase flapping'ini onler: Chase bir
/// kez basladiginda ize varana + sure dolana kadar surekli gidip-gelmez.
/// </summary>
public class ChaseBehavior : IEnemyBehavior
{
    private const float GiveUpDelay    = 4f;    // ize varip etrafa baktiktan sonra vazgecme suresi
    private const float ReachThreshold = 1.5f;  // ipucuna "varildi" sayilan mesafe

    private readonly Vector3? _noisePosition;
    private float _lostSightTimer;
    private Vector3 _searchPoint;
    private bool _hasSearchPoint;
    private bool _huntingNoise;

    public ChaseBehavior() { }

    /// <summary>Ses duyma sebebiyle Chase'e gecildiyse, aranacak hedef konum verilir.</summary>
    public ChaseBehavior(Vector3 noisePosition)
    {
        _noisePosition = noisePosition;
    }

    public void Enter(EnemyController enemy)
    {
        _lostSightTimer = GiveUpDelay;

        if (_noisePosition.HasValue && !enemy.CanSeePlayer() && enemy.Agent.isOnNavMesh)
        {
            _searchPoint = _noisePosition.Value;
            _hasSearchPoint = true;
            _huntingNoise = true;
            enemy.Agent.SetDestination(_searchPoint);
            Debug.Log("[ChaseBehavior] Ses kaynagina dogru gidiliyor.");
        }
        else
        {
            // Gorerek baslamis olabiliriz; son gorulen yeri ipucu olarak sakla.
            if (enemy.CanSeePlayer())
            {
                _searchPoint = enemy.PlayerTransform.position;
                _hasSearchPoint = true;
            }
            Debug.Log("[ChaseBehavior] Kovalama basladi.");
        }
    }

    public void Tick(EnemyController enemy)
    {
        if (enemy.PlayerTransform == null) return;
        if (!enemy.Agent.isOnNavMesh) return;

        // --- Oyuncuyu goruyor: dogrudan kovala, son konumu hatirla, sayaci sifirla ---
        if (enemy.CanSeePlayer())
        {
            _lostSightTimer = GiveUpDelay;
            _huntingNoise = false;
            _searchPoint = enemy.PlayerTransform.position;
            _hasSearchPoint = true;

            float dist = Vector3.Distance(enemy.transform.position, enemy.PlayerTransform.position);
            if (dist <= enemy.AttackTriggerRange)
            {
                enemy.SwitchBehavior(enemy.CreateAttackBehavior());
                return;
            }

            enemy.Agent.SetDestination(enemy.PlayerTransform.position);
            return;
        }

        // --- Goremiyor: once son ize (ses / son gorulen yer) dogru git ---
        if (_hasSearchPoint)
        {
            enemy.Agent.SetDestination(_searchPoint);
            if (!enemy.Agent.pathPending && enemy.Agent.remainingDistance < ReachThreshold)
            {
                _hasSearchPoint = false;   // ize varildi; artik etrafa bak ve sayaci baslat
                if (_huntingNoise)
                {
                    _huntingNoise = false;
                    Debug.Log("[ChaseBehavior] Ize varildi, etrafa bakiliyor.");
                }
            }
            return;   // ize giderken vazgecme sayaci islemez -> flapping olmaz
        }

        // --- Iz tuketildi: bir sure etrafa bak, sonra devriyeye/gezmeye don ---
        _lostSightTimer -= Time.deltaTime;
        if (_lostSightTimer <= 0f)
            enemy.SwitchBehavior(enemy.CreateDefaultBehavior());
    }

    public void Exit(EnemyController enemy)
    {
        if (enemy.Agent.isOnNavMesh)
            enemy.Agent.ResetPath();
    }
}
