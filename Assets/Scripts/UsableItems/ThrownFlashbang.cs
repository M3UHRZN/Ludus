using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Ludus.UsableItems.Core;

/// <summary>
/// Flashbang as a self-contained networked usable. Spawned by PlayerInventory and
/// activated server-side: applies throw impulse, runs a fuse with collision linecast,
/// then blinds players in radius (via PlayerFlashEffect), stuns enemies, and plays a
/// world-positioned explosion sound on everyone before despawning.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
public class ThrownFlashbang : NetworkUsableItem
{
    [Header("Throw")]
    [SerializeField] private float throwSpeed = 16f;
    [SerializeField] private float upwardBoost = 1.5f;
    [SerializeField] private float fuseTime = 1.6f;

    [Header("Blast")]
    [SerializeField] private float blastRadius = 5f;
    [SerializeField] private float blindDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float peakAlpha = 1f;
    [SerializeField] private float enemyStunDuration = 3f;

    [Header("Explosion Audio")]
    [SerializeField] private AudioClip explosionClip;
    [SerializeField] [Range(0f, 1f)] private float explosionVolume = 1f;
    [SerializeField] private float explosionAudibleRange = 18f;

    private readonly List<int> _affectedIndices = new();

    protected override void OnServerActivate(in UsableActivationContext context)
    {
        Vector3 direction = context.Direction.sqrMagnitude > 0.0001f
            ? context.Direction.normalized
            : transform.forward;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce((direction * throwSpeed) + (Vector3.up * upwardBoost), ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
        }

        StartCoroutine(ServerFuseRoutine(direction));
    }

    private IEnumerator ServerFuseRoutine(Vector3 direction)
    {
        Vector3 lastPosition = transform.position;
        Vector3 explosionPoint = lastPosition;
        float timer = 0f;

        while (timer < fuseTime)
        {
            timer += Time.deltaTime;
            Vector3 currentPosition = transform.position;
            if (Physics.Linecast(lastPosition, currentPosition, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            {
                explosionPoint = hit.point;
                break;
            }
            explosionPoint = currentPosition;
            lastPosition = currentPosition;
            yield return null;
        }

        ServerApplyBlast(explosionPoint);
        PlayExplosionAudioRpc(explosionPoint, blindDuration);
        ServerDespawnSelf();
    }

    private void ServerApplyBlast(Vector3 explosionPoint)
    {
        // Players: pure selection via FlashbangMath against the server registry.
        var players = PlayerStateMachine.ServerPlayers;
        var positions = new List<Vector3>(players.Count);
        for (int i = 0; i < players.Count; i++)
            positions.Add(players[i] != null ? players[i].transform.position : new Vector3(99999f, 99999f, 99999f));

        FlashbangMath.SelectAffectedIndices(positions, explosionPoint, blastRadius, _affectedIndices);
        foreach (int idx in _affectedIndices)
        {
            PlayerStateMachine player = players[idx];
            if (player == null) continue;
            PlayerFlashEffect effect = player.GetComponent<PlayerFlashEffect>();
            if (effect != null)
                effect.ServerBlind(blindDuration, peakAlpha);
        }

        // Enemies: physics overlap (they are not in the player registry).
        Collider[] hits = Physics.OverlapSphere(explosionPoint, blastRadius);
        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponent<EnemyController>() ?? hit.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.IsAlive) continue;
            enemy.SetStunned(true, enemyStunDuration);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayExplosionAudioRpc(Vector3 explosionPoint, float duration)
    {
        if (explosionClip == null) return;
        if (explosionClip.loadState == AudioDataLoadState.Unloaded)
            explosionClip.LoadAudioData();

        GameObject audioObject = new GameObject("FlashbangExplosionAudio");
        audioObject.transform.position = explosionPoint;
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = explosionClip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = Mathf.Max(1f, explosionAudibleRange);
        source.volume = explosionVolume;
        source.Play();
        Object.Destroy(audioObject, Mathf.Max(0.5f, duration) + 1f);
    }
}
