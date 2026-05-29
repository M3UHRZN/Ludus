// CorpseItem.cs
// Assets/Scripts/Items/CorpseItem.cs

using TMPro;
using Unity.Netcode;
using UnityEngine;


[RequireComponent(typeof(PhysicsObject))]
public class CorpseItem : NetworkBehaviour
{
    // ------------------------------------------------------------------ Kimlik

    [Header("Corpse Identity")]
    [SerializeField] private string _ownerName     = "Unknown";
    [SerializeField] private ulong  _ownerClientId = 0;

    // Cesedin uzerine runtime'da yazilacak isim etiketi icin agir sync
    // gerekmedigi icin server'da owner name belirlendikten sonra herkese
    // Rpc ile gondeririz; her client lokal olarak floating text yaratir.
    public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> NetOwnerName = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Nameplate Ayarlari")]
    [SerializeField] private float _nameplateHeight = 1.2f;
    [SerializeField] private float _nameplateFontSize = 4f;
    [SerializeField] private Color _nameplateColor = new Color(1f, 0.4f, 0.4f, 0.95f);

    private GameObject _nameplateGo;
    private TextMeshPro _nameplateText;

    public string OwnerName     => _ownerName;
    public ulong  CorpseOwnerClientId => _ownerClientId;

    // ------------------------------------------------------------------ Durum

    public bool IsRevived { get; private set; } = false;

    private PhysicsObject   _physObj;
    private ulong           _lastCarrierId = ulong.MaxValue;

    // ------------------------------------------------------------------ Spawn

    public override void OnNetworkSpawn()
    {
        _physObj = GetComponent<PhysicsObject>();
        _physObj.NetIsHeld.OnValueChanged += OnHeldStateChanged;

        BuildNameplate();
        NetOwnerName.OnValueChanged += OnNameChanged;
        // Network'ten gelen ad varsa hemen yansit
        if (!string.IsNullOrEmpty(NetOwnerName.Value.ToString()))
            UpdateNameplateText(NetOwnerName.Value.ToString());
    }

    public override void OnNetworkDespawn()
    {
        if (_physObj != null)
            _physObj.NetIsHeld.OnValueChanged -= OnHeldStateChanged;
        NetOwnerName.OnValueChanged -= OnNameChanged;

        if (_nameplateGo != null) Destroy(_nameplateGo);
    }

    private void LateUpdate()
    {
        if (_nameplateGo == null) return;
        // Nameplate her zaman ana kameraya bakar (billboard)
        var cam = Camera.main;
        if (cam == null) return;
        _nameplateGo.transform.position = transform.position + Vector3.up * _nameplateHeight;
        _nameplateGo.transform.rotation = Quaternion.LookRotation(_nameplateGo.transform.position - cam.transform.position);
    }

    private void BuildNameplate()
    {
        _nameplateGo = new GameObject("CorpseNameplate");
        _nameplateGo.transform.SetParent(null, false);
        _nameplateGo.transform.position = transform.position + Vector3.up * _nameplateHeight;

        _nameplateText = _nameplateGo.AddComponent<TextMeshPro>();
        _nameplateText.text = _ownerName;
        _nameplateText.fontSize = _nameplateFontSize;
        _nameplateText.color = _nameplateColor;
        _nameplateText.alignment = TextAlignmentOptions.Center;
        _nameplateText.fontStyle = FontStyles.Bold;
        _nameplateText.raycastTarget = false;
    }

    private void UpdateNameplateText(string newName)
    {
        _ownerName = newName;
        if (_nameplateText != null)
            _nameplateText.text = string.IsNullOrEmpty(newName) ? "?" : newName;
    }

    private void OnNameChanged(Unity.Collections.FixedString64Bytes prev, Unity.Collections.FixedString64Bytes current)
    {
        UpdateNameplateText(current.ToString());
    }

    // ------------------------------------------------------------------ NetIsHeld hook

    private void OnHeldStateChanged(bool previous, bool current)
    {
        if (current) OnCorpsePickedUp();
        else         OnCorpseDropped();
    }

    private void OnCorpsePickedUp()
    {
        if (IsRevived) return;

        ulong carrierClientId = _physObj.NetGrabberClientId.Value;
        PlayerInventory inventory = GetInventory(carrierClientId);

        if (inventory != null && inventory.IsFull())
        {
            // Carry slot dolu — grab'i iptal et (sadece server)
            if (IsServer)
                _physObj.ServerStopHold();

            Debug.Log("[CorpseItem] Carry slot dolu — ceset alınamadı.");
            return;
        }

        _lastCarrierId = carrierClientId;

        // Owner client'ının inventory'sine ServerRpc üzerinden yaz
        if (inventory != null)
            inventory.SetCarryingCorpse(true);

        GameEventBus.Publish(new CorpsePickedUpEvent(_ownerClientId, carrierClientId));
        Debug.Log($"[CorpseItem] {_ownerName}'in cesedi alındı (carrier: {carrierClientId})");
    }

    private void OnCorpseDropped()
    {
        PlayerInventory inventory = GetInventory(_lastCarrierId);

        if (inventory != null)
            inventory.SetCarryingCorpse(false);

        GameEventBus.Publish(new CorpseDroppedEvent(_ownerClientId));
        Debug.Log($"[CorpseItem] {_ownerName}'in cesedi bırakıldı.");

        _lastCarrierId = ulong.MaxValue;
    }

    // ------------------------------------------------------------------ Yardımcı

    private static PlayerInventory GetInventory(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || clientId == ulong.MaxValue) return null;
        if (!nm.ConnectedClients.TryGetValue(clientId, out var client)) return null;
        
        if (client.PlayerObject != null)
        {
            var inv = client.PlayerObject.GetComponent<PlayerInventory>();
            if (inv != null) return inv;
        }

        // Fallback for manually placed or custom spawned players where PlayerObject is null
        var allInventories = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var inv in allInventories)
        {
            if (inv.OwnerClientId == clientId)
                return inv;
        }

        return null;
    }

    // ------------------------------------------------------------------ Revival

    /// <summary>
    /// InfirmaryPod trigger'ı, ceset pod'a bırakılınca bunu çağırır.
    /// </summary>
    public void OnRevived()
    {
        if (IsRevived) return;
        IsRevived = true;

        GameEventBus.Publish(new PlayerRevivedEvent((int)_ownerClientId));
        Debug.Log($"[CorpseItem] {_ownerName} canlandı!");
        Destroy(gameObject, 0.5f);
    }

    // ------------------------------------------------------------------ Init

    /// <summary>
    /// PlayerDiedEvent handler'ı ceset prefab'ı spawn ettikten sonra bunu çağırır.
    /// </summary>
    public void Initialize(string ownerName, ulong ownerClientId)
    {
        _ownerName     = ownerName;
        _ownerClientId = ownerClientId;

        // Network sync — clientlar OnNameChanged'de nameplate'i guncellesin
        if (IsServer)
            NetOwnerName.Value = new Unity.Collections.FixedString64Bytes(ownerName ?? "?");
    }
}