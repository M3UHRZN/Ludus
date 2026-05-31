using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tahliye alanı. İçindeki canlı oyuncuları ve getirilmiş cesetleri takip eder.
/// ExtractionService rescue/abandon kararında bunu sorgular. Etkileşim ExtractionLever'da.
/// </summary>
public class ExtractionZone : MonoBehaviour
{
    public static ExtractionZone Instance { get; private set; }

    private readonly List<PlayerStateMachine> _playersInZone = new();
    private readonly List<CorpseItem> _corpsesInZone = new();
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
        var machine = other.GetComponent<PlayerStateMachine>() ?? other.GetComponentInParent<PlayerStateMachine>();
        if (machine != null)
        {
            if (!_playersInZone.Contains(machine)) _playersInZone.Add(machine);
            return;
        }
        var corpse = other.GetComponent<CorpseItem>() ?? other.GetComponentInParent<CorpseItem>();
        if (corpse != null)
        {
            if (!_corpsesInZone.Contains(corpse)) _corpsesInZone.Add(corpse);
            return;
        }

        // Satilabilir/tasinabilir esya (BaseItem) — extraction'da satilmayanlar lobide geri donecek.
        var po = other.GetComponent<PhysicsObject>() ?? other.GetComponentInParent<PhysicsObject>();
        if (po != null && po.GetComponent<BaseItem>() != null && !_itemsInZone.Contains(po))
            _itemsInZone.Add(po);
    }

    private void OnTriggerExit(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>() ?? other.GetComponentInParent<PlayerStateMachine>();
        if (machine != null) { _playersInZone.Remove(machine); return; }
        var corpse = other.GetComponent<CorpseItem>() ?? other.GetComponentInParent<CorpseItem>();
        if (corpse != null) { _corpsesInZone.Remove(corpse); return; }
        var po = other.GetComponent<PhysicsObject>() ?? other.GetComponentInParent<PhysicsObject>();
        if (po != null) _itemsInZone.Remove(po);
    }

    /// <summary>
    /// Zone icindeki gecerli BaseItem PhysicsObject'leri (held dahil). Extraction'da
    /// satilmayanlar bu listeden lobide geri dondurulur.
    /// </summary>
    public List<PhysicsObject> GetItemsInside()
    {
        var result = new List<PhysicsObject>();
        for (int i = _itemsInZone.Count - 1; i >= 0; i--)
        {
            var po = _itemsInZone[i];
            if (po == null) { _itemsInZone.RemoveAt(i); continue; }
            if (po.GetComponent<BaseItem>() == null) continue;
            result.Add(po);
        }
        return result;
    }

    public bool ContainsPlayer(PlayerStateMachine machine)
    {
        return machine != null && _playersInZone.Contains(machine);
    }

    public CorpseItem FindCorpseForClient(ulong clientId)
    {
        for (int i = 0; i < _corpsesInZone.Count; i++)
        {
            var c = _corpsesInZone[i];
            if (c != null && c.CorpseOwnerClientId == clientId) return c;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.6f, 1f, 0.15f);
        Gizmos.matrix = transform.localToWorldMatrix;
        var col = GetComponent<Collider>();
        if (col is BoxCollider box) Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider s) Gizmos.DrawSphere(s.center, s.radius);
    }
}
