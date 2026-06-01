using Unity.Netcode;
using UnityEngine;

// Enemy GameObject'ine ek component. EnemyController MonoBehaviour kalir;
// NetworkVariable'lar bu component uzerinden tasinir.
// Prefab gereksinimi: NetworkObject + EnemyNetState (Sprint 2'de EnemyController'in kendisi NetworkBehaviour'a cevrilince burasi temizlenir).
[RequireComponent(typeof(NetworkObject))]
public class EnemyNetState : NetworkBehaviour
{
    public readonly NetworkVariable<bool> NetIsBlinded = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<float> NetBlindUntilServerTime = new(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Lazer nisan telegraph (RangedAimBehavior). Server yazimi, herkes okur;
    // gorsel cizimi her client kendi tarafinda yapar boylece host + tum istemciler
    // ayni lazeri gorur. Detayli aciklama icin bkz. RangedAimBehavior.
    public readonly NetworkVariable<bool> NetIsAiming = new(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<Vector3> NetAimTarget = new(
        Vector3.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Client-side lazer cizimi icin runtime kaynaklar (her instance icin ayri).
    private LineRenderer _aimLine;
    private GameObject _aimLineObj;
    private Material _aimLineMaterial;

    public void ServerSetBlinded(float duration)
    {
        if (!IsServer) return;
        if (duration <= 0f) return;

        NetIsBlinded.Value = true;
        NetBlindUntilServerTime.Value = (float)NetworkManager.Singleton.ServerTime.Time + duration;
    }

    // Lazer nisani baslat + ilk hedefi yaz
    public void ServerStartAim(Vector3 initialTarget)
    {
        if (!IsServer) return;
        NetAimTarget.Value = initialTarget;
        NetIsAiming.Value = true;
    }

    // Lazer hedefini guncelle (her Tick'te cagrilir)
    public void ServerUpdateAimTarget(Vector3 target)
    {
        if (!IsServer) return;
        if (!NetIsAiming.Value) return;
        NetAimTarget.Value = target;
    }

    // Lazer nisani kapat
    public void ServerStopAim()
    {
        if (!IsServer) return;
        NetIsAiming.Value = false;
    }

    [Header("Sound")]
    [SerializeField] private AudioClip _shootSfx;
    [SerializeField] private AudioClip _punchSfx;
    [Range(0f, 1f)]
    [SerializeField] private float _sfxVolume = 0.85f;
    [SerializeField] private float _sfxMinDistance = 4f;
    [SerializeField] private float _sfxMaxDistance = 25f;

    // Type A robot ates ettiginde server bunu cagirir, tum client'lar 3D ses duyar
    public void ServerPlayShootSfx()
    {
        if (!IsServer) return;
        PlayShootSfxRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void PlayShootSfxRpc()
    {
        PlayOneShotAt(_shootSfx);
    }

    // Type B priest yumruk attiginda server bunu cagirir
    public void ServerPlayPunchSfx()
    {
        if (!IsServer) return;
        PlayPunchSfxRpc();
    }

    [Rpc(SendTo.Everyone)]
    private void PlayPunchSfxRpc()
    {
        PlayOneShotAt(_punchSfx);
    }

    private void PlayOneShotAt(AudioClip clip)
    {
        if (clip == null) return;
        // Gecici GameObject'te 3D AudioSource - oyuncuya gore ses uzakliga gore solar
        var go = new GameObject($"SFX_{clip.name}");
        go.transform.position = transform.position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.spatialBlend = 1f;
        src.minDistance = _sfxMinDistance;
        src.maxDistance = _sfxMaxDistance;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.volume = _sfxVolume;
        src.dopplerLevel = 0f;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }

    public override void OnNetworkDespawn()
    {
        // Runtime olusturulan kaynaklari sizdirma; objeyi kapat + materyali yok et.
        if (_aimLineObj != null)
        {
            Destroy(_aimLineObj);
            _aimLineObj = null;
            _aimLine = null;
        }
        if (_aimLineMaterial != null)
        {
            Destroy(_aimLineMaterial);
            _aimLineMaterial = null;
        }

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (IsServer) UpdateServer();
        UpdateAimVisual();   // host + tum client'larda calismali
    }

    private void UpdateServer()
    {
        if (!NetIsBlinded.Value) return;
        if ((float)NetworkManager.Singleton.ServerTime.Time >= NetBlindUntilServerTime.Value)
            NetIsBlinded.Value = false;
    }

    // Tum client'larda calisir, FirePoint -> NetAimTarget arasinda kirmizi cizgi cizer
    private void UpdateAimVisual()
    {
        if (!NetIsAiming.Value)
        {
            if (_aimLineObj != null && _aimLineObj.activeSelf)
                _aimLineObj.SetActive(false);
            return;
        }

        if (_aimLineObj == null)
            CreateAimLine();

        if (!_aimLineObj.activeSelf)
            _aimLineObj.SetActive(true);

        // Baslangic noktasi: prefabdaki FirePoint (EnemyController disabled olsa bile
        // serialized referans kullanilabilir durumda).
        Vector3 origin;
        var controller = GetComponent<EnemyController>();
        if (controller != null && controller.FirePoint != null)
            origin = controller.FirePoint.position;
        else
            origin = transform.position + Vector3.up * 1.5f;

        _aimLine.SetPosition(0, origin);
        _aimLine.SetPosition(1, NetAimTarget.Value);
    }

    private void CreateAimLine()
    {
        _aimLineObj = new GameObject("EnemyAimLine");
        _aimLineObj.transform.SetParent(transform, false);

        _aimLine = _aimLineObj.AddComponent<LineRenderer>();
        _aimLine.positionCount = 2;
        _aimLine.useWorldSpace = true;
        _aimLine.startWidth = 0.025f;
        _aimLine.endWidth = 0.025f;
        _aimLine.numCapVertices = 2;
        _aimLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _aimLine.receiveShadows = false;
        _aimLine.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _aimLine.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // URP + Built-in calisan sprite shader, bulamazsa fallback
        Shader shader = Shader.Find("Sprites/Default");
        _aimLineMaterial = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
        _aimLineMaterial.color = Color.red;
        _aimLine.material = _aimLineMaterial;
        _aimLine.startColor = Color.red;
        _aimLine.endColor   = Color.red;
    }
}
