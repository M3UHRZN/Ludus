using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Ludus.UsableItems.Core;

/// <summary>
/// Fırlatılan flashbang: patlamada yarıçaptaki oyuncuları kör eder (PlayerFlashEffect),
/// düşmanları sersemletir ve herkese dünya-konumlu patlama sesi çalar.
/// </summary>
public class Flashbang : ThrowableItem
{
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

    protected override void OnDetonate(Vector3 point)
    {
        ServerApplyBlast(point);
        PlayExplosionAudioRpc(point, blindDuration);
    }

    private void ServerApplyBlast(Vector3 explosionPoint)
    {
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
            if (effect != null) effect.ServerBlind(blindDuration, peakAlpha);
        }

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
        if (explosionClip.loadState == AudioDataLoadState.Unloaded) explosionClip.LoadAudioData();

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
