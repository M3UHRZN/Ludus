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
        NetworkVariableWritePermission.Owner);

    public readonly NetworkVariable<float> NetHealth = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private IPlayerState _currentState;

    public bool IsAlive => NetHealth.Value > 0f;
    public float CurrentHealth => NetHealth.Value;

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
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        NetHealth.Value = maxHealth;
        ChangeState(new AliveState());
    }

    private void Update()
    {
        _currentState?.Tick(this);
    }

    public void ChangeState(IPlayerState newState)
    {
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
        if (!IsOwner || !IsAlive) return;

        NetHealth.Value = Mathf.Max(0f, NetHealth.Value - amount);
        GameEventBus.Publish(new PlayerDamagedEvent(
            (int)OwnerClientId, amount, NetHealth.Value));

        if (NetHealth.Value <= 0f)
        {
            GameEventBus.Publish(new PlayerDiedEvent(
                (int)OwnerClientId, transform.position));
            ChangeState(new DeadState());
        }
    }
}
