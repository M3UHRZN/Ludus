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
        if (corpse != null && !_corpsesInZone.Contains(corpse)) _corpsesInZone.Add(corpse);
    }

    private void OnTriggerExit(Collider other)
    {
        var machine = other.GetComponent<PlayerStateMachine>() ?? other.GetComponentInParent<PlayerStateMachine>();
        if (machine != null) { _playersInZone.Remove(machine); return; }
        var corpse = other.GetComponent<CorpseItem>() ?? other.GetComponentInParent<CorpseItem>();
        if (corpse != null) _corpsesInZone.Remove(corpse);
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
