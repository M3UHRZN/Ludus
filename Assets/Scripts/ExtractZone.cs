using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class ExtractZone : MonoBehaviour
{
    [Header("Item Value")]
    public int fallbackCreditValue = 10;

    [Header("Effects (Optional)")]
    public ParticleSystem extractParticle;
    public AudioSource    extractSound;

    private readonly List<PhysicsObject> _itemsInZone = new();

    private void Update()
    {
        for (int i = _itemsInZone.Count - 1; i >= 0; i--)
        {
            var item = _itemsInZone[i];

            if (item == null)
            {
                _itemsInZone.RemoveAt(i);
                continue;
            }

            if (!item.IsHeld)
            {
                ExtractItem(item);
                _itemsInZone.RemoveAt(i);
            }
        }
    }

    private void ExtractItem(PhysicsObject physicsObject)
    {
        int credits = fallbackCreditValue;

        var item = physicsObject.GetComponent<IItem>();
        if (item != null)
            credits = Mathf.RoundToInt(item.CreditValue);

        if (extractParticle != null)
        {
            extractParticle.transform.position = physicsObject.transform.position;
            extractParticle.Play();
        }
        
        // extractSound?.Play(); // ses atanmadıysa skip
        if (extractSound != null) extractSound.Play();

        ExtractionManager.Instance?.RegisterExtractedItem(0, credits);

        Debug.Log($"[ExtractZone] Extracted: {physicsObject.name} | Credits: {credits}");

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            Destroy(physicsObject.gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ExtractZone] Trigger tetiklendi: {other.name}");

        var po = other.GetComponent<PhysicsObject>();
        if (po == null || _itemsInZone.Contains(po)) return;

        _itemsInZone.Add(po);
        Debug.Log($"[ExtractZone] Entered zone: {po.name}");
    }

    private void OnTriggerExit(Collider other)
    {
        var po = other.GetComponent<PhysicsObject>();
        if (po != null) _itemsInZone.Remove(po);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;

        var col = GetComponent<Collider>();
        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider s)
            Gizmos.DrawSphere(s.center, s.radius);
    }
}