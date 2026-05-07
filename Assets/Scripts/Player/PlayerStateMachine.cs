using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class PlayerStateMachine : NetworkBehaviour, IDamageable, ISpectatable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    public PlayerMovement Movement { get; private set; }
    public PlayerLook Look { get; private set; }
    public PlayerInteraction Interaction { get; private set; }
    public PlayerInventory Inventory { get; private set; }
    public SpectatorController Spectator { get; private set; }
    public PlayerInput PlayerInput { get; private set; }

    public readonly NetworkVariable<byte> NetState = new(
        (byte)PlayerStateEnum.Alive,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public readonly NetworkVariable<float> NetHealth = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private IPlayerState _currentState;

    public bool IsAlive => NetHealth.Value > 0f;
    public float CurrentHealth => NetHealth.Value;
    // Owner tarafinda input/interaction kararlari icin "anlik" state.
    // NetState server'dan roundtrip ile gelir; owner local state ise aninda guncellenir.
    public PlayerStateEnum LocalState => _currentState != null ? StateToEnum(_currentState) : (PlayerStateEnum)NetState.Value;

    public Transform SpectateTarget => Look != null ? Look.CameraTarget : transform;
    public string DisplayName => $"Player-{OwnerClientId}";
    public bool CanBeSpectated => IsAlive;

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        Look = GetComponent<PlayerLook>();
        Interaction = GetComponent<PlayerInteraction>();
        Inventory = GetComponent<PlayerInventory>();
        Spectator = GetComponent<SpectatorController>();
        PlayerInput = GetComponent<PlayerInput>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetHealth.Value = maxHealth;
            NetState.Value = (byte)PlayerStateEnum.Alive;
        }

        if (!IsOwner)
        {
            if (PlayerInput != null) PlayerInput.enabled = false;
            enabled = false;
            return;
        }

        NetState.OnValueChanged += OnNetStateChanged;
        ApplyState((PlayerStateEnum)NetState.Value);
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            NetState.OnValueChanged -= OnNetStateChanged;
    }

    private void Update()
    {
        _currentState?.Tick(this);
    }

    public void ChangeState(IPlayerState newState)
    {
        if (!IsOwner || newState == null) return;

        // Owner side: lokal hemen geçer (responsive feel), server'a bildirir.
        _currentState?.Exit(this);
        _currentState = newState;
        _currentState.Enter(this);

        byte stateByte = (byte)StateToEnum(newState);
        if (IsServer)
            NetState.Value = stateByte;
        else
            RequestStateServerRpc(stateByte);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestStateServerRpc(byte newState, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Extra belt-and-suspenders: Owner permission should already enforce this.
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        // Reject invalid enum bytes (corrupt / modded / future version mismatch).
        if (newState > (byte)PlayerStateEnum.Dead) return;

        var requested = (PlayerStateEnum)newState;
        var current = (PlayerStateEnum)NetState.Value;

        // Dead/stun gibi state'ler client'in "set" etmesine kapali; server olayi tetikler.
        if (current == PlayerStateEnum.Dead) return;
        if (requested == PlayerStateEnum.Dead || requested == PlayerStateEnum.Stunned) return;

        // Co-op icin basit ve deterministic bir transition seti.
        bool allowed = (current, requested) switch
        {
            (PlayerStateEnum.Alive, PlayerStateEnum.Carrying) => true,
            (PlayerStateEnum.Alive, PlayerStateEnum.Interacting) => true,
            (PlayerStateEnum.Carrying, PlayerStateEnum.Alive) => true,
            (PlayerStateEnum.Interacting, PlayerStateEnum.Alive) => true,
            _ => false
        };

        if (!allowed) return;

        NetState.Value = newState;
    }

    // Server'dan tetiklenen state geçişi (örn. damage → DeadState).
    private void OnNetStateChanged(byte previous, byte current)
    {
        if (!IsOwner) return;
        var newEnum = (PlayerStateEnum)current;
        if (StateToEnum(_currentState) == newEnum) return;
        ApplyState(newEnum);
    }

    private void ApplyState(PlayerStateEnum stateEnum)
    {
        var newState = NewStateInstance(stateEnum);
        if (newState == null) return;
        _currentState?.Exit(this);
        _currentState = newState;
        _currentState.Enter(this);
    }

    public void SwitchActionMap(string mapName)
    {
        if (PlayerInput == null) return;
        PlayerInput.SwitchCurrentActionMap(mapName);
    }

    public void SetComponentsEnabled(bool movement, bool look, bool interaction, bool inventory, bool spectator)
    {
        if (Movement != null)    Movement.enabled = movement;
        if (Look != null)        Look.enabled = look;
        if (Interaction != null) Interaction.enabled = interaction;
        if (Inventory != null)   Inventory.enabled = inventory;
        if (Spectator != null)   Spectator.enabled = spectator;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (IsServer)
        {
            ApplyDamage(amount, hitPoint, attackerClientId);
            return;
        }
        TakeDamageServerRpc(amount, hitPoint, attackerClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TakeDamageServerRpc(float amount, Vector3 hitPoint, ulong attackerClientId, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Co-op model: client'lar sadece "kendi oyunculari" icin damage isteyebilir.
        // (Server-authoritative kaynaklar dogrudan ApplyDamage cagirir.)
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        // Client-supplied attackerClientId'yi guvenilir kabul etmiyoruz.
        ApplyDamage(amount, hitPoint, rpcParams.Receive.SenderClientId);
    }

    private void ApplyDamage(float amount, Vector3 hitPoint, ulong attackerClientId)
    {
        if (!IsServer || !IsAlive) return;

        // Sanity checks: accidental negative/NaN/infinite values should never mutate health.
        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f) return;

        // TODO: anti-cheat — attackerClientId'nin bu mesafede olduğunu doğrula (Sprint 2).
        amount = Mathf.Min(amount, maxHealth);
        NetHealth.Value = Mathf.Max(0f, NetHealth.Value - amount);
        GameEventBus.Publish(new PlayerDamagedEvent(
            (int)OwnerClientId, amount, NetHealth.Value));

        if (NetHealth.Value <= 0f)
        {
            GameEventBus.Publish(new PlayerDiedEvent(
                (int)OwnerClientId, transform.position));
            NetState.Value = (byte)PlayerStateEnum.Dead;
        }
    }

    private static PlayerStateEnum StateToEnum(IPlayerState state) => state switch
    {
        AliveState => PlayerStateEnum.Alive,
        CarryingState => PlayerStateEnum.Carrying,
        DeadState => PlayerStateEnum.Dead,
        StunnedState => PlayerStateEnum.Stunned,
        InteractState => PlayerStateEnum.Interacting,
        _ => PlayerStateEnum.Alive
    };

    private static IPlayerState NewStateInstance(PlayerStateEnum stateEnum) => stateEnum switch
    {
        PlayerStateEnum.Alive => new AliveState(),
        PlayerStateEnum.Carrying => new CarryingState(),
        PlayerStateEnum.Dead => new DeadState(),
        PlayerStateEnum.Stunned => new StunnedState(),
        PlayerStateEnum.Interacting => new InteractState(),
        _ => null
    };
}
