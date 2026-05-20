using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Oyuncunun hareketine gore ayak sesi (NoiseEmittedEvent) yayinlar.
/// Enemy AI (EnemyController.OnNoiseHeard) bu sesi dinleyip Chase'e gecer.
///
/// Gerceklik mekanigi:
///   - Comelme (crouch)  : ses YOK -> dusman fark etmez
///   - Normal yurume     : orta menzil ses
///   - Kosma (sprint)    : genis menzil + daha sik ses -> dusman uzaktan duyar
///   - Havada / dururken : ses yok
///
/// Network: Owner'da calisir. Host ise dogrudan GameEventBus'a yayinlar;
/// client ise ServerRpc ile server'a bildirir (enemy AI server-only kosuyor,
/// olay server tarafinda yayinlanmali).
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerNoiseEmitter : NetworkBehaviour
{
    [Header("Ses Menzilleri (birim)")]
    [Tooltip("Normal yurume ayak sesi menzili")]
    [SerializeField] private float walkNoiseRange = 12f;

    [Tooltip("Kosma ayak sesi menzili — daha genis, daha cok dusman duyar")]
    [SerializeField] private float runNoiseRange = 28f;

    [Header("Adim Araligi (saniye)")]
    [Tooltip("Yururken iki ayak sesi arasi")]
    [SerializeField] private float walkStepInterval = 0.5f;

    [Tooltip("Kosarken iki ayak sesi arasi — daha sik = daha cok ses")]
    [SerializeField] private float runStepInterval = 0.3f;

    private PlayerMovement _movement;
    private float _stepTimer;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }
        _movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (_movement == null) return;

        // Havada (ziplama/dusme) ayak sesi olmaz
        if (!_movement.IsGrounded)
        {
            _stepTimer = 0f;
            return;
        }

        float speed = _movement.CurrentSpeed;

        // Comelme VEYA hareketsiz -> ses yok (dusman fark etmez)
        if (_movement.NetCrouching.Value || speed < 0.1f)
        {
            _stepTimer = 0f;
            return;
        }

        // Kosuyor mu? Hiz runSpeed'e yakinsa kosma kabul edilir
        bool running = speed >= _movement.RunSpeed - 0.5f;
        float interval = running ? runStepInterval : walkStepInterval;
        float range    = running ? runNoiseRange  : walkNoiseRange;

        _stepTimer -= Time.deltaTime;
        if (_stepTimer <= 0f)
        {
            EmitFootstep(range);
            _stepTimer = interval;
        }
    }

    private void EmitFootstep(float range)
    {
        // Host (server+owner): dogrudan yayinla. Client: server'a bildir.
        if (IsServer)
            GameEventBus.Publish(new NoiseEmittedEvent(transform.position, range, "Footstep"));
        else
            EmitFootstepRpc(transform.position, range);
    }

    [Rpc(SendTo.Server)]
    private void EmitFootstepRpc(Vector3 position, float range)
    {
        // Server'da yayinla -> server-only kosan enemy'ler bu olayi duyar
        GameEventBus.Publish(new NoiseEmittedEvent(position, range, "Footstep"));
    }
}
