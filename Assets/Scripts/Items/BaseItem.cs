using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Item hiyerarşisinin kökü. Networked (NetworkBehaviour) — böylece usable/equippable
/// alt sınıfları onu IS-A ile miras alır VE RPC kullanabilir. Item verisi
/// ItemDefinition'dan gelir.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class BaseItem : NetworkBehaviour, IItem
{
    [Header("Item")]
    [Tooltip("Item verisi (id, ad, boyut, deger, icon). Envanter/market kimligini belirler.")]
    [SerializeField] private ItemDefinition definition;

    public ItemDefinition Definition => definition;
    public ushort ItemId => definition != null ? definition.Id : (ushort)0;

    public string ItemName  => definition != null ? definition.DisplayName : "Unknown Item";
    public ItemSize Size     => definition != null ? definition.Size : ItemSize.Small;
    public int Weight        => definition != null ? definition.Weight : 1;
    public float CreditValue => definition != null ? definition.CreditValue : 0f;

    public virtual void OnPickup(PlayerInventory inventory)
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        gameObject.SetActive(false);
        GameEventBus.Publish(new ItemPickedUpEvent(ItemName, Weight, CreditValue));
    }

    public virtual void OnDrop()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        gameObject.SetActive(true);
    }
}
