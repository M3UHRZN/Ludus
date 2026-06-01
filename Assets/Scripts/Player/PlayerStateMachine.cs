using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class PlayerStateMachine : NetworkBehaviour, IDamageable, ISpectatable, IKnockbackable, ISlowable
{
    /// <summary>
    /// Server-only registry of every spawned PlayerStateMachine. Populated in
    /// OnNetworkSpawn (IsServer gate) and drained in OnNetworkDespawn. Consumers
    /// MUST filter by IsAlive — dead players stay in the list until despawn so
    /// that revive flows do not need extra plumbing. Empty on non-server peers.
    /// </summary>
    public static readonly List<PlayerStateMachine> ServerPlayers = new();

    /// <summary>
    /// Server-side: clientId -> PlayerStateMachine arama. Sadece CANLI eslesmeyi
    /// doner; oyuncu olmus / despawn olmussa null. EnemyController bunu hasar
    /// kaynagi ve carrier-id resolve etmek icin kullanir.
    /// </summary>
    public static PlayerStateMachine GetServerPlayer(ulong clientId)
    {
        for (int i = 0; i < ServerPlayers.Count; i++)
        {
            var p = ServerPlayers[i];
            if (p == null) continue;
            if (p.OwnerClientId != clientId) continue;
            if (!p.IsAlive) continue;
            return p;
        }
        return null;
    }

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Death Drop")]
    [Tooltip("Olunce envanterdeki esyalar yere dokulsun mu? Co-op kurtarma akisi icin true.")]
    [SerializeField] private bool _dropItemsOnDeath = true;

    [Tooltip("Olunce ceset prefab'i spawn edilsin mi? Diger oyuncular tasiyabilir, infirmary'ye getirebilir.")]
    [SerializeField] private bool _spawnCorpseOnDeath = true;

    [Tooltip("Ceset prefab'i (CorpseItem + PhysicsObject + NetworkObject). Bos ise ceset spawn atlanir.")]
    [SerializeField] private GameObject _corpsePrefab;

    [Tooltip("Eldeki/envanterdeki esyalar bu yaricap icinde rastgele yerlerde dokulur (yiginda kalmasin diye).")]
    [SerializeField] private float _itemDropScatterRadius = 1.5f;

    [Tooltip("Esya drop yerden bu yukseklikte spawn edilir (zemine yerlessin diye).")]
    [SerializeField] private float _itemDropHeightOffset = 0.6f;

    public PlayerMovement Movement { get; private set; }
    public PlayerLook Look { get; private set; }
    public PlayerInteraction Interaction { get; private set; }
    public PlayerInventory Inventory { get; private set; }
    public SpectatorController Spectator { get; private set; }
    public PlayerInput PlayerInput { get; private set; }
    public PlayerDisplayName DisplayNameSource { get; private set; }

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

    public Transform SpectateTarget => transform;
    public string DisplayName => DisplayNameSource != null && !string.IsNullOrWhiteSpace(DisplayNameSource.DisplayName)
        ? DisplayNameSource.DisplayName
        : $"Player-{OwnerClientId}";
    public bool CanBeSpectated => IsAlive;

    private void Awake()
    {
        Movement = GetComponent<PlayerMovement>();
        Look = GetComponent<PlayerLook>();
        Interaction = GetComponent<PlayerInteraction>();
        Inventory = GetComponent<PlayerInventory>();
        Spectator = GetComponent<SpectatorController>();
        PlayerInput = GetComponent<PlayerInput>();
        DisplayNameSource = GetComponent<PlayerDisplayName>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetHealth.Value = maxHealth;
            NetState.Value = (byte)PlayerStateEnum.Alive;

            // Server registry: enemy AI nearest-player lookup uses this.
            // Duplicate guard for safety against double-spawn callbacks.
            if (!ServerPlayers.Contains(this))
                ServerPlayers.Add(this);
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
        if (IsServer)
            ServerPlayers.Remove(this);

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

    /// <summary>Server tarafindan zorla stun uygula (FearSystem panik, vb.)</summary>
    public void ForceStun(float duration)
    {
        if (!IsServer) return;
        if (!IsAlive) return;
        StartCoroutine(StunCoroutine(duration));
    }

    private System.Collections.IEnumerator StunCoroutine(float duration)
    {
        byte prev = NetState.Value;
        NetState.Value = (byte)PlayerStateEnum.Stunned;
        yield return new WaitForSeconds(duration);
        // Stun bittikten sonra Alive'a don (Dead degillerse)
        if (IsAlive && (PlayerStateEnum)NetState.Value == PlayerStateEnum.Stunned)
            NetState.Value = prev;
    }

    private const float KnockbackSlideDuration  = 0.3f;
    private const float KnockbackShakeAmplitude = 0.25f;  // kamera sarsilma siddeti
    private const float KnockbackShakeFrequency = 25f;     // sarsilma hizi
    private const float KnockbackShakeDuration  = 0.4f;    // sarsilma suresi (sn)

    /// <summary>
    /// IKnockbackable — dusman yakin vurusu: oyuncuyu kaynaktan uzaga firlatir +
    /// kisa sure stun eder. Server-only tetiklenir (enemy AI server'da kosar).
    /// Stun NetState ile sync olur; knockback hareketi owner-authoritative oldugu
    /// icin owner'a Rpc ile gonderilir.
    /// </summary>
    public void ApplyKnockback(Vector3 sourcePosition, float force, float stunDuration)
    {
        if (!IsServer || !IsAlive) return;

        Vector3 dir = transform.position - sourcePosition;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : -transform.forward;

        ForceStun(stunDuration);
        ApplyKnockbackRpc(dir * force);
    }

    /// <summary>
    /// ISlowable — dusman mermisi vb. dis kaynak oyuncuyu gecici olarak yavaslatir.
    /// Server'da tetiklenir, owner'a RPC ile gonderilir. Owner'in PlayerMovement
    /// scripti slow'u uygular (FearSystem'in hiz cezasiyla carpilarak compose).
    /// </summary>
    public void ApplySlow(float multiplier, float duration)
    {
        if (!IsServer || !IsAlive) return;
        if (duration <= 0f) return;
        ApplySlowRpc(Mathf.Clamp(multiplier, 0.05f, 1f), duration);
    }

    [Rpc(SendTo.Owner)]
    private void ApplySlowRpc(float multiplier, float duration)
    {
        if (Movement != null)
            Movement.ApplyTemporarySlow(multiplier, duration);
    }

    [Rpc(SendTo.Owner)]
    private void ApplyKnockbackRpc(Vector3 horizontalVelocity)
    {
        if (Movement != null)
            StartCoroutine(KnockbackRoutine(horizontalVelocity));

        // Sert vurus hissi: kamerayi sars. Stun PlayerLook'u kapatsa bile bu coroutine
        // PlayerStateMachine'de (hep acik) kostugu icin calisir. cameraTarget'i sarsmak
        // vcam->brain->Main Camera zinciriyle ekrana yansir, ekstra kurulum gerekmez.
        Transform camT = Look != null ? Look.CameraTarget : null;
        if (camT != null)
            StartCoroutine(CameraShakeRoutine(camT));
    }

    private System.Collections.IEnumerator KnockbackRoutine(Vector3 horizontalVelocity)
    {
        float t = 0f;
        while (t < KnockbackSlideDuration)
        {
            float frac = 1f - (t / KnockbackSlideDuration);   // dogrusal sonumlenme
            Vector3 step = horizontalVelocity * (frac * Time.deltaTime);
            step.y = -9.81f * Time.deltaTime;                  // zemine yapis (egimde havalanmasin)
            Movement.ApplyExternalMove(step);
            t += Time.deltaTime;
            yield return null;
        }
    }

    private System.Collections.IEnumerator CameraShakeRoutine(Transform camT)
    {
        Vector3 origin = camT.localPosition;
        float elapsed = 0f;
        while (elapsed < KnockbackShakeDuration)
        {
            float fade = 1f - (elapsed / KnockbackShakeDuration);   // sonumlenme
            float ox = (Mathf.PerlinNoise(elapsed * KnockbackShakeFrequency, 0f) - 0.5f) * 2f * KnockbackShakeAmplitude * fade;
            float oy = (Mathf.PerlinNoise(0f, elapsed * KnockbackShakeFrequency) - 0.5f) * 2f * KnockbackShakeAmplitude * fade;
            camT.localPosition = origin + new Vector3(ox, oy, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        camT.localPosition = origin;   // dinlenme pozisyonuna don
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
            // Ceset olen anin TAM AYNI frame'inde spawn olsun, gozle gorulur gecikme olmasin.
            // Sira: 1) ceset spawn et (en goz onunde) 2) elindekini birak 3) envanteri sac
            //       4) visual hide RPC 5) event + state
            if (_spawnCorpseOnDeath)
                ServerSpawnCorpse();

            if (_dropItemsOnDeath)
            {
                ServerDropHeldObjectOnDeath();
                ServerDropInventoryItemsOnDeath();
            }

            // Canli mesh + nameplate'i tum client'larda gizle, sahnede iki "Alp" gozukmesin.
            HidePlayerVisualOnAllClientsRpc();

            GameEventBus.Publish(new PlayerDiedEvent(
                (int)OwnerClientId, transform.position));
            NetState.Value = (byte)PlayerStateEnum.Dead;
        }
    }

    // Elinde tuttugu nesneyi birakir, NetIsHeld false olur.
    private void ServerDropHeldObjectOnDeath()
    {
        if (Interaction == null) return;
        var held = Interaction.HeldObject;
        if (held == null) return;
        if (!held.IsHeld) return;
        held.ServerStopHold();
    }

    // Envanterdeki item'lari olum noktasi cevresine sacar, slotlari temizler.
    private void ServerDropInventoryItemsOnDeath()
    {
        if (Inventory == null) return;
        if (Inventory.Slots == null || Inventory.Slots.Count == 0) return;

        var catalog = ItemCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning("[PlayerStateMachine] ItemCatalog yok, envanter drop atlandi.");
            return;
        }

        Vector3 origin = transform.position + Vector3.up * _itemDropHeightOffset;
        int dropped = 0;
        for (int i = 0; i < Inventory.Slots.Count; i++)
        {
            ushort itemId = Inventory.Slots[i];
            if (itemId == 0) continue;
            var prefab = catalog.GetPrefab(itemId);
            if (prefab == null) continue;

            // Yatay sapma, ust uste yigilmasin
            Vector2 planar = Random.insideUnitCircle * _itemDropScatterRadius;
            Vector3 pos = origin + new Vector3(planar.x, 0f, planar.y);

            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning($"[PlayerStateMachine] '{prefab.name}' uzerinde NetworkObject yok, drop iptal.");
                Destroy(go);
                continue;
            }
            netObj.Spawn(true);
            dropped++;
        }

        // Envanteri temizle (server-side NetworkList yazma)
        Inventory.ClearInventory();

        if (dropped > 0)
            Debug.Log($"[PlayerStateMachine] Olum sirasinda {dropped} esya yere dokuldu.");
    }

    // Ceset prefab'ini spawn eder, CorpseItem.Initialize ile owner ismini yazar.
    private void ServerSpawnCorpse()
    {
        if (_corpsePrefab == null) return;

        // Olen oyuncunun yonune gore cesedi yere yatir. Prefab kendi 90 X rotasyonuyla geliyor.
        float yaw = transform.eulerAngles.y;
        Quaternion lieDown = Quaternion.Euler(0f, yaw, 0f);

        // Ceset DOGRUDAN player'in oldugu yere dussun, yukseklik offset'i koymuyoruz.
        // Boylece olunce ANINDA o pozisyonda gozukur, "havadan dusme" gecikmesi olmaz.
        var corpseGo = Instantiate(
            _corpsePrefab,
            transform.position,
            lieDown);

        // Owner ismini Spawn ONCESI yaz, boylece initial network snapshot dogru gider
        // ve tum client'lar ceseti gordugu an dogru nickname ile gorur.
        string ownerName = DisplayName;
        var corpseItem = corpseGo.GetComponent<CorpseItem>();
        if (corpseItem != null)
            corpseItem.Initialize(ownerName, OwnerClientId);

        var netObj = corpseGo.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError($"[PlayerStateMachine] Corpse prefab '{_corpsePrefab.name}' uzerinde NetworkObject yok.");
            Destroy(corpseGo);
            return;
        }
        netObj.Spawn(true);

        // Spawn sonrasi guvenlik: yeniden yaz ki gecikmis client'lar da kesin almis olsun
        if (corpseItem != null)
            corpseItem.RefreshOwnerName(ownerName);

        Debug.Log($"[PlayerStateMachine] Ceset spawn edildi: {ownerName} (clientId={OwnerClientId}, yaw={yaw:F0}).");
    }

    [Rpc(SendTo.Everyone)]
    private void HidePlayerVisualOnAllClientsRpc()
    {
        // Olu oyuncunun mesh + nameplate'ini gizle. Sahnede sadece ceset gozuksun.
        HideVisualChildren(transform);
    }

    private static void HideVisualChildren(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;

        var tmp = root.GetComponentsInChildren<TMPro.TextMeshPro>(true);
        for (int i = 0; i < tmp.Length; i++) tmp[i].enabled = false;

        var tmpUi = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
        for (int i = 0; i < tmpUi.Length; i++) tmpUi[i].enabled = false;
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
