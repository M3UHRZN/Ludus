using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInventory : NetworkBehaviour
{
    public const int MaxSlots = 4;

    public readonly NetworkList<ushort> Slots = new();

    public readonly NetworkVariable<byte> ActiveSlot = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    private InputAction _scrollAction;
    private InputAction _useAction;
    private InputAction _dropAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        var input = GetComponent<PlayerInput>();
        _scrollAction = input.actions["Gameplay/Scroll"];
        _useAction = input.actions["Gameplay/UseItem"];
        _dropAction = input.actions["Gameplay/Drop"];
    }

    private void Update()
    {
        if (_scrollAction == null) return;
        HandleScroll();

        if (_useAction.WasPressedThisFrame())
            UseActiveItem();
    }

    private void HandleScroll()
    {
        float scroll = _scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) < 0.01f) return;
        if (Slots.Count == 0) return;

        int dir = scroll > 0 ? -1 : 1;
        int next = (ActiveSlot.Value + dir + Slots.Count) % Slots.Count;
        ActiveSlot.Value = (byte)next;
    }

    public bool TryAddItem(ushort itemId)
    {
        if (Slots.Count >= MaxSlots) return false;
        Slots.Add(itemId);
        return true;
    }

    public void RemoveAtSlot(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        Slots.RemoveAt(index);
        if (ActiveSlot.Value >= Slots.Count && Slots.Count > 0)
            ActiveSlot.Value = (byte)(Slots.Count - 1);
    }

    private void UseActiveItem()
    {
        if (Slots.Count == 0) return;
        Debug.Log($"[Inventory] Use item at slot {ActiveSlot.Value}: ID={Slots[ActiveSlot.Value]}");
    }
}
