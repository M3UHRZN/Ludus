using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerInteraction : NetworkBehaviour
{
    [Header("Referanslar")]
    [SerializeField] private Camera playerCamera;

    [Header("Tutma Ayarlari")]
    [SerializeField] private float holdDistance = 2.5f;
    [SerializeField] private float minHoldDistance = 1f;
    [SerializeField] private float maxHoldDistance = 5f;

    [Header("Firlatma")]
    [SerializeField] private float maxChargeTime = 1.5f;

    [Header("Raycast")]
    [SerializeField] private LayerMask interactLayers = ~0;
    [SerializeField] private float interactRange = 4f;
    [SerializeField] private bool debugInteraction;

    [Header("UI")]
    [SerializeField] private GameObject interactPromptUI;

    public readonly NetworkVariable<bool> IsHolding = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PhysicsObject _heldObject;
    public PhysicsObject HeldObject => _heldObject;
    private IInteractable _lookedInteractable;
    private bool _isChargingThrow;
    private float _chargeStartTime;

    private InputAction _interactAction;
    private InputAction _throwAction;
    private InputAction _scrollAction;
    private InputAction _flashlightAction;
    private InputAction _dropAction;

    private PlayerStateMachine _machine;
    private bool _loggedMissingCamera;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        playerCamera = ResolveInteractionCamera();

        _machine = GetComponent<PlayerStateMachine>();

        var input = GetComponent<PlayerInput>();
        _interactAction = input.actions["Gameplay/Interact"];
        _throwAction = input.actions["Gameplay/Throw"];
        _scrollAction = input.actions["Gameplay/Scroll"];
        _flashlightAction = input.actions["Gameplay/Flashlight"];
        _dropAction = input.actions["Gameplay/Drop"];

        // === RADAR KODU ===
        if (interactPromptUI == null)
        {
            // Sahnede "InteractionContainer" adındaki objeyi otomatik bulup kabloyu bağlar
            GameObject uiObj = GameObject.Find("InteractionContainer");
            if (uiObj != null)
            {
                interactPromptUI = uiObj;
                interactPromptUI.SetActive(false); // Başlangıçta yazıyı gizle!
            }
        }
        // ===========================================
    }

    private void Update()
    {
        if (_interactAction == null) return;
        if (playerCamera == null)
            playerCamera = ResolveInteractionCamera();
        if (playerCamera == null)
        {
            if (debugInteraction && !_loggedMissingCamera)
            {
                Debug.LogWarning("[PlayerInteraction] No camera resolved for interaction raycast.", this);
                _loggedMissingCamera = true;
            }
            return;
        }
        _loggedMissingCamera = false;
        HandleLook();
        HandleInput();
        UpdateHoldDistance();
    }

    private void FixedUpdate()
    {
        if (playerCamera == null) return;
        if (_heldObject == null) return;
        Vector3 targetPos = playerCamera.transform.position
                          + playerCamera.transform.forward * holdDistance;
        _heldObject.MoveTowards(targetPos);
    }

    private void HandleLook()
    {
        if (_lookedInteractable != null && _lookedInteractable != _heldObject as IInteractable)
        {
            var ph = _lookedInteractable as Component;
            if (ph != null && TryGetPhysicsObject(ph, out var po))
                po.SetHighlight(false);
            _lookedInteractable = null;
        }

        if (_heldObject != null)
        {
            ShowPrompt(false);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (debugInteraction)
            Debug.DrawRay(ray.origin, ray.direction * interactRange, Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactLayers))
        {
            var interactable = FindInteractable(hit.collider);
            bool canInteract = interactable != null && interactable.CanInteract(_machine);
            if (debugInteraction)
            {
                string interactableType = interactable?.GetType().Name ?? "null";
                string interactableName = interactable is Component interactableComponent
                    ? interactableComponent.name
                    : interactable?.GetType().Name ?? "null";
                string physicsState = string.Empty;
                if (interactable is PhysicsObject physicsObject)
                    physicsState = $" netIsHeld={physicsObject.NetIsHeld.Value} isSpawned={physicsObject.IsSpawned}";
                Debug.Log($"[PlayerInteraction] Hit '{hit.collider.name}' interactable={interactableName} type={interactableType} canInteract={canInteract} distance={hit.distance:F2}{physicsState}", this);
            }
            if (canInteract)
            {
                _lookedInteractable = interactable;

                if (TryGetPhysicsObject(hit.collider, out var po))
                    po.SetHighlight(true);

                ShowPrompt(true);
                return;
            }

            if (debugInteraction)
                Debug.Log($"[PlayerInteraction] Hit '{hit.collider.name}' but interactable was null or CanInteract returned false.", this);
        }
        else if (debugInteraction)
        {
            Debug.Log("[PlayerInteraction] Raycast hit nothing.", this);
        }

        ShowPrompt(false);
    }

    private void HandleInput()
    {
        if (_interactAction.WasPressedThisFrame())
        {
            if (_heldObject == null)
                TryGrab();
            else
                DropObject();
        }

        if (_dropAction.WasPressedThisFrame() && _heldObject != null)
            DropObject();

        if (_throwAction.WasPressedThisFrame() && _heldObject != null)
        {
            _isChargingThrow = true;
            _chargeStartTime = Time.time;
        }

        if (_throwAction.WasReleasedThisFrame() && _isChargingThrow && _heldObject != null)
            ThrowObject();

        // YENİ AKILLI 'F' TUŞU: Çantaya At VEYA Çantadan Çıkar!
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (_heldObject != null)
            {
                TryStashObject(); // Elim doluysa çantaya at
            }
            else
            {
                TryEquipFromStash(); // Elim boşsa çantadan çıkarıp elime al!
            }
        }
    }

    // ÇANTAYA ATMA (STASH) OPERASYONU
    private void TryStashObject()
    {
        // Tuttuğumuz obje bir BaseItem mi? (Yani çantaya atılabilir bir şey mi?)
        if (_heldObject.TryGetComponent<BaseItem>(out var baseItem))
        {
            var inventory = GetComponent<PlayerInventory>();

            // 1. Çantada yer varsa (TryAddItem true dönerse)
            if (inventory.TryAddItem(baseItem.ItemId))
            {
                // HATA ÇÖZÜMÜ: Eşyanın ağ kimliğini, ağa bağlı olan _heldObject üzerinden alıyoruz!
                var networkObj = _heldObject.NetworkObject;

                // 2. Fiziksel tutma işlemini bitir (Eşya elimizden düşsün)
                DropObject();

                // 3. Eşyayı ağ (Network) üzerinden dünyadan tamamen sil/gizle!
                if (networkObj != null)
                {
                    RequestDespawnServerRpc(new NetworkObjectReference(networkObj));
                }
            }
            else
            {
                Debug.Log("Çanta dolu, eşya alınamadı!");
            }
        }
    }

    // AĞ ÜZERİNDEN EŞYAYI YOK ETME BİLDİRİSİ
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDespawnServerRpc(NetworkObjectReference targetRef)
    {
        if (targetRef.TryGet(out var targetNob))
        {
            targetNob.Despawn(); // Eşyayı sahneden tamamen siler
        }
    }

    private void UpdateHoldDistance()
    {
        if (_heldObject == null) return;
        float scroll = _scrollAction.ReadValue<float>();
        holdDistance = Mathf.Clamp(holdDistance + scroll * 0.01f, minHoldDistance, maxHoldDistance);
    }

    private void TryGrab()
    {
        if (_lookedInteractable == null)
        {
            if (debugInteraction)
                Debug.Log("[PlayerInteraction] TryGrab aborted: no looked interactable.", this);
            return;
        }
        var component = _lookedInteractable as Component;
        if (component == null)
        {
            if (debugInteraction)
                Debug.LogWarning("[PlayerInteraction] TryGrab aborted: looked interactable is not a Component.", this);
            return;
        }

        if (TryGetPhysicsObject(component, out var physObj))
        {
            if (physObj.CanPickupToInventory)
            {
                if (physObj.NetworkObject == null)
                    return;

                RequestInventoryPickupServerRpc(new NetworkObjectReference(physObj.NetworkObject));
                return;
            }

            if (physObj.NetIsHeld.Value)
            {
                if (debugInteraction)
                    Debug.Log($"[PlayerInteraction] TryGrab aborted: '{physObj.name}' already held.", this);
                return;
            }
            if (physObj.NetworkObject == null)
            {
                if (debugInteraction)
                    Debug.LogWarning($"[PlayerInteraction] TryGrab aborted: '{physObj.name}' has no NetworkObject.", this);
                return;
            }

            if (debugInteraction)
                Debug.Log($"[PlayerInteraction] Requesting grab for '{physObj.name}'.", this);
            RequestGrabServerRpc(new NetworkObjectReference(physObj.NetworkObject));
            return;
        }

        // PhysicsObject olmayan IInteractable'lar (kapilar, switchler) — Phase 5.5 (Sprint 2).
        _lookedInteractable.Interact(_machine);
    }

    public void DropObject()
    {
        if (_heldObject == null) return;
        if (_heldObject.NetworkObject == null) return;
        RequestDropServerRpc(new NetworkObjectReference(_heldObject.NetworkObject));
        // _heldObject NotifyReleased ile temizlenir (server-driven).
    }

    private void ThrowObject()
    {
        if (_heldObject == null) return;
        if (_heldObject.NetworkObject == null) return;

        float chargeRatio = Mathf.Clamp01((Time.time - _chargeStartTime) / maxChargeTime);
        Vector3 throwDir = playerCamera.transform.forward;

        RequestThrowServerRpc(new NetworkObjectReference(_heldObject.NetworkObject), throwDir, chargeRatio);
        _isChargingThrow = false;
        // _heldObject NotifyReleased ile temizlenir.
    }

    // PhysicsObject.NetGrabberClientId.OnValueChanged'dan gelen callback (owner-side).
    public void NotifyGrabbed(PhysicsObject physObj)
    {
        if (!IsOwner || physObj == null) return;
        _heldObject = physObj;

        if (physObj.Weight >= 6f && _machine != null)
            _machine.ChangeState(new CarryingState());
    }

    public void NotifyReleased(PhysicsObject physObj)
    {
        if (!IsOwner) return;
        if (_heldObject != physObj) return;

        _heldObject = null;
        _isChargingThrow = false;

        if (_machine != null && _machine.LocalState == PlayerStateEnum.Carrying)
            _machine.ChangeState(new AliveState());
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestInventoryPickupServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!targetRef.TryGet(out var targetNob)) return;
        if (!targetNob.TryGetComponent<PhysicsObject>(out var physObj)) return;

        float dist = Vector3.Distance(transform.position, physObj.transform.position);
        if (dist > interactRange + 1.5f)
            return;

        PlayerInventory inventory = _machine != null ? _machine.Inventory : GetComponent<PlayerInventory>();
        if (inventory == null)
            return;

        physObj.ServerTryPickupInto(inventory);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestGrabServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!targetRef.TryGet(out var targetNob))
        {
            if (debugInteraction)
                Debug.LogWarning("[PlayerInteraction] Server grab rejected: targetRef could not resolve.", this);
            return;
        }
        if (!targetNob.TryGetComponent<PhysicsObject>(out var physObj))
        {
            if (debugInteraction)
                Debug.LogWarning($"[PlayerInteraction] Server grab rejected: '{targetNob.name}' has no PhysicsObject.", this);
            return;
        }
        if (physObj.NetIsHeld.Value)
        {
            if (debugInteraction)
                Debug.Log($"[PlayerInteraction] Server grab rejected: '{physObj.name}' already held.", this);
            return;
        }

        // Mesafe kontrolu (anti-cheat): grabber bu mesafede mi?
        float dist = Vector3.Distance(transform.position, physObj.transform.position);
        if (dist > interactRange + 1.5f)
        {
            if (debugInteraction)
                Debug.Log($"[PlayerInteraction] Server grab rejected: distance {dist:F2} > {interactRange + 1.5f:F2}.", this);
            return;
        }

        physObj.ServerStartHold(rpcParams.Receive.SenderClientId);
        IsHolding.Value = true;
        if (debugInteraction)
            Debug.Log($"[PlayerInteraction] Server grab accepted for '{physObj.name}' by client {rpcParams.Receive.SenderClientId}.", this);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestDropServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!targetRef.TryGet(out var targetNob)) return;
        if (!targetNob.TryGetComponent<PhysicsObject>(out var physObj)) return;
        if (physObj.NetGrabberClientId.Value != rpcParams.Receive.SenderClientId) return;

        physObj.ServerStopHold();
        IsHolding.Value = false;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestThrowServerRpc(NetworkObjectReference targetRef, Vector3 direction, float chargeRatio, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        if (!targetRef.TryGet(out var targetNob)) return;
        if (!targetNob.TryGetComponent<PhysicsObject>(out var physObj)) return;
        if (physObj.NetGrabberClientId.Value != rpcParams.Receive.SenderClientId) return;

        // Sanity check
        if (float.IsNaN(direction.x) || float.IsNaN(direction.y) || float.IsNaN(direction.z)) return;
        if (direction.sqrMagnitude < 0.0001f) return;

        physObj.ServerThrow(direction, chargeRatio);
        IsHolding.Value = false;
    }

    private void ShowPrompt(bool active)
    {
        if (interactPromptUI != null)
            interactPromptUI.SetActive(active);
    }

    private static IInteractable FindInteractable(Component source)
    {
        if (source == null) return null;

        var interactable = source.GetComponent<IInteractable>();
        if (interactable != null) return interactable;

        return source.GetComponentInParent<IInteractable>();
    }

    private static bool TryGetPhysicsObject(Component source, out PhysicsObject physicsObject)
    {
        physicsObject = null;
        if (source == null) return false;

        if (source.TryGetComponent(out physicsObject))
            return true;

        physicsObject = source.GetComponentInParent<PhysicsObject>();
        return physicsObject != null;
    }

    private Camera ResolveInteractionCamera()
    {
        if (playerCamera != null)
            return playerCamera;

        var main = Camera.main;
        if (main != null && main.isActiveAndEnabled)
            return main;

        var brains = FindObjectsByType<CinemachineBrain>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var brain in brains)
        {
            var cam = brain.GetComponent<Camera>();
            if (cam != null && cam.isActiveAndEnabled)
                return cam;
        }

        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            if (cam.isActiveAndEnabled)
                return cam;
        }

        return null;
    }

    // ÇANTADAN ÇIKARIP ELİNE ALMA OPERASYONU
    private void TryEquipFromStash()
    {
        var inventory = GetComponent<PlayerInventory>();

        // Eğer çantadan eşyayı başarıyla çekebildiysek (TryTakeActiveItem true dönerse)
        if (inventory.TryTakeActiveItem(out ushort itemId))
        {
            // ID'li eşyayı hemen önümde yarat ve direkt elime ver!"
            Vector3 spawnPos = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
            RequestSpawnAndGrabServerRpc(itemId, spawnPos);
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void RequestSpawnAndGrabServerRpc(ushort itemId, Vector3 spawnPosition, RpcParams rpcParams = default)
    {
        // 1. Veritabanından gerçek Prefab'ı çek!
        GameObject itemPrefab = ItemDatabase.Instance.GetPrefab(itemId);

        if (itemPrefab != null)
        {
            // 2. Dünyada yarat
            GameObject spawned = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);

            // 3. Ağ (Network) objesi olarak herkese duyur
            var networkObj = spawned.GetComponent<NetworkObject>();
            networkObj.Spawn();

            // 4. Fiziksel olarak F'ye basan oyuncunun eline ver!
            if (spawned.TryGetComponent<PhysicsObject>(out var physObj))
            {
                physObj.ServerStartHold(rpcParams.Receive.SenderClientId);
            }
        }
    }
}
