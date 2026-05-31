using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Extraction zone içindeki satış alt-bölgesi. Trigger içinde duran (held olmayan)
/// BaseItem'ları takip eder. Yalnızca "şu an içeride ne var" bilir; satış extraction
/// anında ExtractionService tarafından yapılır. Item buraya atılınca kaybolmaz/satılmaz.
/// </summary>
public class LootSellZone : MonoBehaviour
{
    public static LootSellZone Instance { get; private set; }

    private readonly List<PhysicsObject> _itemsInZone = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnTriggerEnter(Collider other)
    {
        var po = other.GetComponent<PhysicsObject>();
        if (po == null) po = other.GetComponentInParent<PhysicsObject>();
        if (po == null) return;
        if (po.GetComponent<BaseItem>() == null) return; // sadece satılabilir eşya
        if (!_itemsInZone.Contains(po)) _itemsInZone.Add(po);
    }

    private void OnTriggerExit(Collider other)
    {
        var po = other.GetComponent<PhysicsObject>();
        if (po == null) po = other.GetComponentInParent<PhysicsObject>();
        if (po != null) _itemsInZone.Remove(po);
    }

    /// <summary>Şu an içeride duran, held OLMAYAN, geçerli BaseItem'lar.</summary>
    public List<PhysicsObject> GetSellableItems()
    {
        var result = new List<PhysicsObject>();
        for (int i = _itemsInZone.Count - 1; i >= 0; i--)
        {
            var po = _itemsInZone[i];
            if (po == null) { _itemsInZone.RemoveAt(i); continue; }
            if (po.IsHeld) continue;                       // elde tutulan satılmaz
            if (po.GetComponent<BaseItem>() == null) continue;
            result.Add(po);
        }
        return result;
    }

    /// <summary>Satılabilir item'ların toplam CreditValue'su (yuvarlanmış).</summary>
    public int ComputeGross()
    {
        int total = 0;
        foreach (var po in GetSellableItems())
        {
            if (po.TryGetComponent<BaseItem>(out var item))
                total += Mathf.RoundToInt(item.CreditValue);
        }
        return total;
    }

    /// <summary>Server-only: satılan item'ları sahneden despawn et.</summary>
    public void ConsumeAndDespawn()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        foreach (var po in GetSellableItems())
        {
            var no = po.NetworkObject;
            if (no != null && no.IsSpawned) no.Despawn(true);
            else if (po != null) Destroy(po.gameObject);
        }
        _itemsInZone.Clear();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        var col = GetComponent<Collider>();
        if (col is BoxCollider box) Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider s) Gizmos.DrawSphere(s.center, s.radius);
    }
}
