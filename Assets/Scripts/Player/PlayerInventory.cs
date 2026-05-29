using Unity.Netcode;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerInventory : NetworkBehaviour
{
    public const int MaxSlots = 4;
    private const int DefaultMarketStartingCredits = 100;
    private static int s_ServerMarketCredits = DefaultMarketStartingCredits;

    public readonly NetworkList<ushort> Slots = new();

    public readonly NetworkVariable<byte> ActiveSlot = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── Corpse Carry (Sprint 2 — Yasin) ──────────────────────────────────────
    public readonly NetworkVariable<bool> IsCarryingCorpse = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    // ─────────────────────────────────────────────────────────────────────────

    [Header("Flashbang")]
    [SerializeField] private ushort flashbangItemId = 100;
    [SerializeField] private GameObject flashbangWorldPrefab;
    [SerializeField] private float flashbangThrowSpeed = 16f;
    [SerializeField] private float flashbangUpwardBoost = 1.5f;
    [SerializeField] private float flashbangFuseTime = 1.6f;
    [SerializeField] private float flashbangBlastRadius = 5f;
    [SerializeField] private float flashbangBlindDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float flashbangPeakAlpha = 1f;
    [SerializeField] private float flashbangEnemyStunDuration = 3f;
    [SerializeField] [Range(0f, 1f)] private float blindedMoveMultiplier = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float blindedLookMultiplier = 0.25f;
    [SerializeField] private int flashbangMarketPrice = 40;

    [Header("Flashbang Audio")]
    [SerializeField] private AudioClip flashbangExplosionClip;
    [SerializeField] [Range(0f, 1f)] private float flashbangExplosionVolume = 1f;
    [SerializeField] private float flashbangExplosionAudibleRange = 18f;
    [SerializeField] private AudioClip flashbangRingingClip;
    [SerializeField] [Range(0f, 1f)] private float flashbangRingingVolume = 0.7f;
    [SerializeField] private float flashbangAudioExtraFadeTime = 1f;

    private InputAction _scrollAction;
    private InputAction _useAction;
    private InputAction _dropAction;
    private Image _flashOverlay;
    private Coroutine _flashRoutine;
    private PlayerMovement _movement;
    private PlayerLook _look;
    private AudioSource _flashbangAudioSource;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        var input = GetComponent<PlayerInput>();
        _scrollAction = input.actions["Gameplay/Scroll"];
        _useAction    = input.actions["Gameplay/UseItem"];
        _dropAction   = input.actions["Gameplay/Drop"];

        // Mevcut: flashbang / hareket entegrasyonu
        _movement = GetComponent<PlayerMovement>();
        _look = GetComponent<PlayerLook>();
        EnsureFlashOverlay();
        EnsureFlashbangAudio();

        // Esmanur UI kopru: Sunucu cantaya esya koydugunda / slot degisiminde
        // GameEventBus.Publish(LocalInventoryUpdatedEvent) otomatik tetikle.
        // Anonim lambda ile abone olursak unsubscribe edemeyiz; named handler kullaniyoruz.
        Slots.OnListChanged += OnSlotsChanged;
        ActiveSlot.OnValueChanged += OnActiveSlotChanged;

        // Ilk frame'de UI bir kere sifir state'le cizilsin
        TriggerUIUpdate();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        Slots.OnListChanged -= OnSlotsChanged;
        ActiveSlot.OnValueChanged -= OnActiveSlotChanged;
    }

    private void OnSlotsChanged(Unity.Netcode.NetworkListEvent<ushort> changeEvent) => TriggerUIUpdate();
    private void OnActiveSlotChanged(byte prev, byte current) => TriggerUIUpdate();

    /// <summary>UI'a "cantam degisti, kendini yeniden ciz" sinyali fırlatır.</summary>
    private void TriggerUIUpdate()
    {
        ushort[] currentItems = new ushort[Slots.Count];
        for (int i = 0; i < Slots.Count; i++)
        {
            currentItems[i] = Slots[i];
        }

        GameEventBus.Publish(new LocalInventoryUpdatedEvent(currentItems, ActiveSlot.Value));
    }

    private void Update()
    {
        if (_scrollAction == null) return;
        HandleScroll();

        bool usePressed = _useAction.WasPressedThisFrame() ||
                          (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame);
        if (usePressed)
            UseActiveItem();

        // Çantadan seçili eşyayı yere atma tuşuna basılırsa
        if (_dropAction != null && _dropAction.WasPressedThisFrame())
        {
            DropActiveItemFromInventory();
        }
    }

    // ÇANTADAN YERE ATMA OPERASYONU
    private void DropActiveItemFromInventory()
    {
        // Çanta boşsa hiçbir şey yapma
        if (Slots.Count == 0) return;

        // 1. Atılacak eşyanın ID'sini al
        ushort itemIdToDrop = Slots[ActiveSlot.Value];

        // 2. Eşyayı çantadan (Listeden) sil
        RemoveAtSlot(ActiveSlot.Value);

        // 3. Sunucuya "Bu eşyayı önüme fiziksel olarak geri yarat" de
        Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 1f;
        SpawnItemServerRpc(itemIdToDrop, spawnPos);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SpawnItemServerRpc(ushort itemId, Vector3 spawnPosition)
    {
        // Veritabanından gerçek Prefab'ı çek ve yere fırlat!
        GameObject itemPrefab = ItemDatabase.Instance.GetPrefab(itemId);
        if (itemPrefab != null)
        {
            GameObject spawned = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);
            spawned.GetComponent<NetworkObject>().Spawn();
        }
    }

    private void HandleScroll()
    {
        float scroll = _scrollAction.ReadValue<float>();
        if (Mathf.Abs(scroll) < 0.01f) return;
        if (Slots.Count == 0) return;

        int dir  = scroll > 0 ? -1 : 1;
        int next = (ActiveSlot.Value + dir + Slots.Count) % Slots.Count;
        RequestActiveSlotServerRpc((byte)next);
    }

    // ── Item Yönetimi ─────────────────────────────────────────────────────────

    public bool TryAddItem(ushort itemId)
    {
        if (Slots.Count >= MaxSlots)  return false;
        if (IsCarryingCorpse.Value)   return false; // ceset taşırken item alınamaz
        AddItemServerRpc(itemId);
        return true;
    }

    public bool ServerTryAddItem(ushort itemId)
    {
        if (!IsServer) return false;
        if (Slots.Count >= MaxSlots) return false;
        if (IsCarryingCorpse.Value) return false;

        Slots.Add(itemId);
        return true;
    }

    public void RemoveAtSlot(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        RemoveAtSlotServerRpc(index);
    }

    public bool ServerTryRemoveAtSlot(int index, out ushort itemId)
    {
        itemId = 0;
        if (!IsServer) return false;
        if (index < 0 || index >= Slots.Count) return false;

        itemId = Slots[index];
        Slots.RemoveAt(index);
        if (ActiveSlot.Value >= Slots.Count && Slots.Count > 0)
            ActiveSlot.Value = (byte)(Slots.Count - 1);
        else if (Slots.Count == 0)
            ActiveSlot.Value = 0;

        return true;
    }

    public void RequestMarketFlashbangPurchase(Vector3 deliveryPosition, Vector3 deliveryForward)
    {
        if (!IsOwner) return;
        RequestMarketFlashbangPurchaseServerRpc(deliveryPosition, deliveryForward);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestMarketFlashbangPurchaseServerRpc(Vector3 deliveryPosition, Vector3 deliveryForward, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (float.IsNaN(deliveryPosition.x) || float.IsNaN(deliveryPosition.y) || float.IsNaN(deliveryPosition.z)) return;
        if (float.IsNaN(deliveryForward.x) || float.IsNaN(deliveryForward.y) || float.IsNaN(deliveryForward.z)) return;

        if (Vector3.Distance(transform.position, deliveryPosition) > 8f)
        {
            SendInventoryMarketMessageRpc("Delivery point is too far away.");
            return;
        }

        if (s_ServerMarketCredits < flashbangMarketPrice)
        {
            SendInventoryMarketMessageRpc("Not enough team credits.");
            return;
        }

        s_ServerMarketCredits -= flashbangMarketPrice;
        ServerSpawnFlashbangPickup(deliveryPosition, deliveryForward);
        SendInventoryMarketMessageRpc($"Bought Flashbang. Team Credits: {s_ServerMarketCredits}");
    }

    // ── Corpse Carry (Sprint 2 — Yasin) ──────────────────────────────────────

    /// <summary>
    /// Ceset alınabilir mi? Slot dolu VEYA zaten ceset taşınıyorsa false.
    /// CorpseItem.OnCorpsePickedUp() çağırır.
    /// </summary>
    public bool IsFull()
    {
        return Slots.Count >= MaxSlots || IsCarryingCorpse.Value;
    }

    /// <summary>
    /// CorpseItem, ceset alındığında/bırakıldığında çağırır.
    /// Sadece Owner çağırabilir.
    /// </summary>
    public void SetCarryingCorpse(bool carrying)
    {
        if (!IsOwner) return;
        SetCarryingCorpseServerRpc(carrying);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SetCarryingCorpseServerRpc(bool carrying)
    {
        IsCarryingCorpse.Value = carrying;
    }

    // ─────────────────────────────────────────────────────────────────────────

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void AddItemServerRpc(ushort itemId)
    {
        if (Slots.Count >= MaxSlots) return;
        Slots.Add(itemId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RemoveAtSlotServerRpc(int index)
    {
        if (index < 0 || index >= Slots.Count) return;
        Slots.RemoveAt(index);
        if (ActiveSlot.Value >= Slots.Count && Slots.Count > 0)
            ActiveSlot.Value = (byte)(Slots.Count - 1);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestActiveSlotServerRpc(byte newSlot)
    {
        if (newSlot < Slots.Count)
            ActiveSlot.Value = newSlot;
    }

    public void RequestFlashbang(Vector3 origin, float radius, float duration)
    {
        if (!IsOwner) return;
        FlashbangServerRpc(origin, radius, duration);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void FlashbangServerRpc(Vector3 origin, float radius, float duration)
    {
        if (!IsServer) return;
        if (radius <= 0f || radius > 50f) return;
        if (duration <= 0f || duration > 30f) return;
        if (float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z)) return;

        if (Vector3.Distance(transform.position, origin) > radius + 2f) return;

        Collider[] hits = Physics.OverlapSphere(origin, radius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<EnemyController>();
            if (enemy == null) continue;
            enemy.SetBlinded(true, duration);
        }
    }

    private void UseActiveItem()
    {
        if (Slots.Count == 0) return;
        if (ActiveSlot.Value >= Slots.Count) return;

        ushort itemId = Slots[ActiveSlot.Value];
        if (itemId == flashbangItemId)
        {
            Transform aimTransform = ResolveAimTransform();
            Vector3 direction = aimTransform != null ? aimTransform.forward : transform.forward;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;

            Vector3 origin = transform.position + Vector3.up * 1.45f + direction.normalized * 0.45f;
            ThrowFlashbangServerRpc(ActiveSlot.Value, origin, direction);
            return;
        }

        Debug.Log($"[Inventory] Use item at slot {ActiveSlot.Value}: ID={itemId}");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ThrowFlashbangServerRpc(int slotIndex, Vector3 origin, Vector3 direction, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (slotIndex < 0 || slotIndex >= Slots.Count)
        {
            SendInventoryMarketMessageRpc("No flashbang selected.");
            return;
        }
        if (Slots[slotIndex] != flashbangItemId)
        {
            SendInventoryMarketMessageRpc("Selected item is not a flashbang.");
            return;
        }
        if (float.IsNaN(origin.x) || float.IsNaN(origin.y) || float.IsNaN(origin.z)) return;
        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z)) return;
        if (direction.sqrMagnitude < 0.0001f) return;
        if (Vector3.Distance(transform.position + Vector3.up, origin) > 4f)
        {
            SendInventoryMarketMessageRpc("Flashbang throw origin was rejected.");
            return;
        }

        ServerTryRemoveAtSlot(slotIndex, out _);
        StartCoroutine(ServerFlashbangProjectileRoutine(origin, direction.normalized));
    }

    private Transform ResolveAimTransform()
    {
        if (_look != null && _look.CameraTarget != null)
            return _look.CameraTarget;

        Camera localCamera = GetComponentInChildren<Camera>();
        if (localCamera != null)
            return localCamera.transform;

        return transform;
    }

    private IEnumerator ServerFlashbangProjectileRoutine(Vector3 origin, Vector3 direction)
    {
        GameObject projectile = null;
        Rigidbody rb = null;
        NetworkObject netObject = null;

        if (flashbangWorldPrefab != null)
        {
            projectile = Instantiate(flashbangWorldPrefab, origin, Quaternion.LookRotation(direction));
            projectile.transform.localScale = new Vector3(0.28f, 0.18f, 0.45f);

            netObject = projectile.GetComponent<NetworkObject>();
            rb = projectile.GetComponent<Rigidbody>();

            if (netObject != null)
            {
                netObject.Spawn(true);
                if (projectile.TryGetComponent(out PhysicsObject physicsObject))
                    physicsObject.ServerConfigureInventoryPickup(false, flashbangItemId);
            }
            else
            {
                Debug.LogWarning("[Flashbang] Flashbang world prefab is missing NetworkObject.");
            }

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.AddForce((direction * flashbangThrowSpeed) + (Vector3.up * flashbangUpwardBoost), ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 4f, ForceMode.Impulse);
            }
        }

        Vector3 lastPosition = projectile != null ? projectile.transform.position : origin;
        Vector3 explosionPoint = lastPosition;
        float timer = 0f;

        while (timer < flashbangFuseTime)
        {
            timer += Time.deltaTime;

            if (projectile != null)
            {
                Vector3 currentPosition = projectile.transform.position;
                if (Physics.Linecast(lastPosition, currentPosition, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    explosionPoint = hit.point;
                    break;
                }

                explosionPoint = currentPosition;
                lastPosition = currentPosition;
            }
            else
            {
                explosionPoint += direction * flashbangThrowSpeed * Time.deltaTime;
            }

            yield return null;
        }

        ServerApplyFlashbang(explosionPoint);

        if (netObject != null && netObject.IsSpawned)
            netObject.Despawn(true);
        else if (projectile != null)
            Destroy(projectile);
    }

    private void ServerSpawnFlashbangPickup(Vector3 position, Vector3 forward)
    {
        if (flashbangWorldPrefab == null)
        {
            SendInventoryMarketMessageRpc("Flashbang world prefab is missing on PlayerInventory.");
            return;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;

        GameObject pickup = Instantiate(flashbangWorldPrefab, position, Quaternion.LookRotation(forward.normalized));
        pickup.transform.localScale = new Vector3(0.28f, 0.18f, 0.45f);

        NetworkObject netObject = pickup.GetComponent<NetworkObject>();
        if (netObject != null)
        {
            netObject.Spawn(true);
            if (pickup.TryGetComponent(out PhysicsObject physicsObject))
                physicsObject.ServerConfigureInventoryPickup(true, flashbangItemId);
        }
        else
        {
            Debug.LogWarning("[Flashbang] Flashbang pickup prefab is missing NetworkObject.");
        }

        Rigidbody rb = pickup.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.AddForce((Vector3.up + forward.normalized * 0.4f) * 1.5f, ForceMode.Impulse);
        }
    }

    private void ServerApplyFlashbang(Vector3 explosionPoint)
    {
        PlayFlashbangExplosionRpc(explosionPoint, flashbangBlindDuration + flashbangAudioExtraFadeTime);

        float sqrRadius = flashbangBlastRadius * flashbangBlastRadius;

        foreach (PlayerStateMachine player in PlayerStateMachine.ServerPlayers)
        {
            if (player == null) continue;
            if ((player.transform.position + Vector3.up - explosionPoint).sqrMagnitude > sqrRadius) continue;

            PlayerInventory inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
                inventory.PlayLocalFlashbangRpc(flashbangBlindDuration, flashbangPeakAlpha);
        }

        Collider[] hits = Physics.OverlapSphere(explosionPoint, flashbangBlastRadius);
        foreach (Collider hit in hits)
        {
            EnemyController enemy = hit.GetComponent<EnemyController>() ?? hit.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.IsAlive) continue;
            enemy.SetStunned(true, flashbangEnemyStunDuration);
        }
    }

    [Rpc(SendTo.Everyone)]
    private void PlayFlashbangExplosionRpc(Vector3 explosionPoint, float duration)
    {
        AudioClip clip = flashbangExplosionClip != null ? flashbangExplosionClip : flashbangRingingClip;
        if (clip == null)
            return;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        GameObject audioObject = new GameObject("FlashbangExplosionAudio");
        audioObject.transform.position = explosionPoint;

        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = Mathf.Max(1f, flashbangExplosionAudibleRange);
        source.volume = flashbangExplosionVolume;
        source.Play();

        StartCoroutine(FadeAndDestroyFlashbangAudio(source, audioObject, duration, flashbangExplosionVolume));
    }

    private IEnumerator FadeAndDestroyFlashbangAudio(AudioSource source, GameObject audioObject, float duration, float startVolume)
    {
        float safeDuration = Mathf.Max(0.05f, duration);
        float timer = 0f;

        while (timer < safeDuration && source != null)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / safeDuration);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, t);
            source.volume = startVolume * fade;
            yield return null;
        }

        if (source != null)
            source.Stop();
        if (audioObject != null)
            Destroy(audioObject);
    }

    [Rpc(SendTo.Everyone)]
    private void PlayLocalFlashbangRpc(float duration, float alpha)
    {
        if (!IsOwner) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);

        _flashRoutine = StartCoroutine(LocalFlashbangRoutine(duration, alpha));
    }

    [Rpc(SendTo.Owner)]
    private void SendInventoryMarketMessageRpc(string message)
    {
        MarketUIController ui = FindFirstObjectByType<MarketUIController>();
        if (ui != null)
            ui.SetExternalStatus(message);
        else
            Debug.Log($"[Market] {message}");
    }

    private IEnumerator LocalFlashbangRoutine(float duration, float alpha)
    {
        EnsureFlashOverlay();
        EnsureFlashbangAudio();

        float safeDuration = Mathf.Max(0.05f, duration);
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (_movement != null)
            _movement.SetSpeedMultiplier(blindedMoveMultiplier);
        if (_look != null)
            _look.SetLookMultiplier(blindedLookMultiplier);

        if (_flashOverlay != null)
        {
            _flashOverlay.enabled = true;
            _flashOverlay.color = new Color(1f, 1f, 1f, clampedAlpha);
        }

        if (_flashbangAudioSource != null && flashbangRingingClip != null)
        {
            if (flashbangRingingClip.loadState == AudioDataLoadState.Unloaded)
                flashbangRingingClip.LoadAudioData();

            _flashbangAudioSource.Stop();
            _flashbangAudioSource.clip = flashbangRingingClip;
            _flashbangAudioSource.time = 0f;
            _flashbangAudioSource.volume = flashbangRingingVolume * clampedAlpha;
            _flashbangAudioSource.Play();
        }

        float audioDuration = safeDuration + Mathf.Max(0f, flashbangAudioExtraFadeTime);
        float timer = 0f;
        while (timer < audioDuration)
        {
            timer += Time.deltaTime;
            float visualT = Mathf.Clamp01(timer / safeDuration);
            float visualFade = 1f - Mathf.SmoothStep(0f, 1f, visualT);

            if (_flashOverlay != null)
                _flashOverlay.color = new Color(1f, 1f, 1f, clampedAlpha * visualFade);

            if (_flashbangAudioSource != null && _flashbangAudioSource.isPlaying)
            {
                float audioT = Mathf.Clamp01(timer / audioDuration);
                float audioFade = 1f - Mathf.SmoothStep(0f, 1f, audioT);

                _flashbangAudioSource.volume = flashbangRingingVolume * clampedAlpha * audioFade;
            }

            if (_movement != null)
                _movement.SetSpeedMultiplier(Mathf.Lerp(blindedMoveMultiplier, 1f, visualT));
            if (_look != null)
                _look.SetLookMultiplier(Mathf.Lerp(blindedLookMultiplier, 1f, visualT));

            yield return null;
        }

        if (_flashOverlay != null)
        {
            _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            _flashOverlay.enabled = false;
        }

        if (_movement != null)
            _movement.SetSpeedMultiplier(1f);
        if (_look != null)
            _look.SetLookMultiplier(1f);

        if (_flashbangAudioSource != null)
        {
            _flashbangAudioSource.Stop();
            _flashbangAudioSource.volume = 0f;
        }

        _flashRoutine = null;
    }

    private void EnsureFlashOverlay()
    {
        if (_flashOverlay != null)
            return;

        GameObject canvasObject = new GameObject("FlashbangRuntimeCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObject = new GameObject("FlashOverlay");
        overlayObject.transform.SetParent(canvasObject.transform, false);
        _flashOverlay = overlayObject.AddComponent<Image>();
        _flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        _flashOverlay.raycastTarget = false;
        _flashOverlay.enabled = false;

        RectTransform rect = _flashOverlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void EnsureFlashbangAudio()
    {
        if (_flashbangAudioSource != null)
            return;

        Transform existing = transform.Find("FlashbangAudioSource");
        GameObject audioObject = existing != null
            ? existing.gameObject
            : new GameObject("FlashbangAudioSource");

        audioObject.transform.SetParent(transform, false);

        _flashbangAudioSource = audioObject.GetComponent<AudioSource>();
        if (_flashbangAudioSource == null)
            _flashbangAudioSource = audioObject.AddComponent<AudioSource>();

        _flashbangAudioSource.playOnAwake = false;
        _flashbangAudioSource.loop = false;
        _flashbangAudioSource.spatialBlend = 0f;
        _flashbangAudioSource.volume = 0f;

        if (flashbangRingingClip != null && flashbangRingingClip.loadState == AudioDataLoadState.Unloaded)
            flashbangRingingClip.LoadAudioData();
        if (flashbangExplosionClip != null && flashbangExplosionClip.loadState == AudioDataLoadState.Unloaded)
            flashbangExplosionClip.LoadAudioData();
    }

    /// <summary>Cantadaki aktif esyayi siler ve ID'sini doner. UI extraction akisinda kullanilir.</summary>
    public bool TryTakeActiveItem(out ushort itemId)
    {
        itemId = 0;
        if (Slots.Count == 0) return false;

        itemId = Slots[ActiveSlot.Value];
        RemoveAtSlot(ActiveSlot.Value);
        return true;
    }
}
