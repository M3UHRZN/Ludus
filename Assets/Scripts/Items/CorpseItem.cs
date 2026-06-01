using TMPro;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PhysicsObject))]
public class CorpseItem : NetworkBehaviour
{
    [Header("Corpse Identity")]
    [SerializeField] private string _ownerName = "Unknown";
    [SerializeField] private ulong _ownerClientId = 0;

    // Ceset uzerindeki isim icin agir sync gerek yok, server'da yazip herkese yayariz.
    public readonly NetworkVariable<Unity.Collections.FixedString64Bytes> NetOwnerName = new(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Nameplate Ayarlari")]
    [SerializeField] private float _nameplateHeight = 1.2f;
    [SerializeField] private float _nameplateFontSize = 4f;
    [SerializeField] private Color _nameplateColor = new Color(1f, 0.4f, 0.4f, 0.95f);

    private GameObject _nameplateGo;
    private TextMeshPro _nameplateText;

    public string OwnerName => _ownerName;
    public ulong CorpseOwnerClientId => _ownerClientId;

    public bool IsRevived { get; private set; } = false;

    private PhysicsObject _physObj;
    private ulong _lastCarrierId = ulong.MaxValue;

    public override void OnNetworkSpawn()
    {
        _physObj = GetComponent<PhysicsObject>();
        _physObj.NetIsHeld.OnValueChanged += OnHeldStateChanged;

        // Network snapshot'tan gelen ismi BuildNameplate'ten ONCE okuyup _ownerName'e
        // yazariz; boylece nameplate dogru isimle olusturulur, "Unknown" flash olmaz.
        string snapshotName = NetOwnerName.Value.ToString();
        if (!string.IsNullOrEmpty(snapshotName))
            _ownerName = snapshotName;

        BuildNameplate();
        NetOwnerName.OnValueChanged += OnNameChanged;
        // Late join veya gec gelen update'leri yakala
        if (!string.IsNullOrEmpty(snapshotName))
            UpdateNameplateText(snapshotName);
    }

    public override void OnNetworkDespawn()
    {
        if (_physObj != null)
            _physObj.NetIsHeld.OnValueChanged -= OnHeldStateChanged;
        NetOwnerName.OnValueChanged -= OnNameChanged;

        if (_nameplateGo != null) Destroy(_nameplateGo);
    }

    private Camera _cam;

    private void LateUpdate()
    {
        if (_nameplateGo == null) return;
        // Nameplate her zaman ana kameraya bakar (billboard).
        // Camera.main her frame FindGameObjectWithTag yapar, cache'le, sadece null'sa tazele.
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        _nameplateGo.transform.position = transform.position + Vector3.up * _nameplateHeight;
        _nameplateGo.transform.rotation = Quaternion.LookRotation(_nameplateGo.transform.position - _cam.transform.position);
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

    private void OnHeldStateChanged(bool previous, bool current)
    {
        if (current) OnCorpsePickedUp();
        else OnCorpseDropped();
    }

    private void OnCorpsePickedUp()
    {
        if (IsRevived) return;

        ulong carrierClientId = _physObj.NetGrabberClientId.Value;
        PlayerInventory inventory = GetInventory(carrierClientId);

        if (inventory != null && inventory.IsFull())
        {
            if (IsServer)
                _physObj.ServerStopHold();

            Debug.Log("[CorpseItem] Carry slot dolu, ceset alinamadi.");
            return;
        }

        _lastCarrierId = carrierClientId;

        if (inventory != null)
            inventory.SetCarryingCorpse(true);

        GameEventBus.Publish(new CorpsePickedUpEvent(_ownerClientId, carrierClientId));
        Debug.Log($"[CorpseItem] {_ownerName} cesedi alindi (carrier: {carrierClientId})");
    }

    private void OnCorpseDropped()
    {
        PlayerInventory inventory = GetInventory(_lastCarrierId);

        if (inventory != null)
            inventory.SetCarryingCorpse(false);

        GameEventBus.Publish(new CorpseDroppedEvent(_ownerClientId));
        Debug.Log($"[CorpseItem] {_ownerName} cesedi birakildi.");

        _lastCarrierId = ulong.MaxValue;
    }

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

        // PlayerObject null ise (custom spawn) tum PlayerInventory'leri tara
        var allInventories = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var inv in allInventories)
        {
            if (inv.OwnerClientId == clientId)
                return inv;
        }

        return null;
    }

    // InfirmaryPod trigger cesedi pod'a birakinca bunu cagirir.
    public void OnRevived()
    {
        if (IsRevived) return;
        IsRevived = true;

        GameEventBus.Publish(new PlayerRevivedEvent((int)_ownerClientId));
        Debug.Log($"[CorpseItem] {_ownerName} canlandi!");
        Destroy(gameObject, 0.5f);
    }

    // Spawn ONCESI cagrilir. NetworkVariable'in initial snapshot'i bu degerle gider,
    // tum client'lar ilk gordukleri an dogru nickname ile gorur.
    public void Initialize(string ownerName, ulong ownerClientId)
    {
        _ownerName = ownerName;
        _ownerClientId = ownerClientId;

        // IsServer NetworkObject Spawn olmadan once de dogru deger doner (NetworkManager.IsServer)
        // NGO 2.x'te NetworkVariable.Value Spawn'dan once yazilabilir, ilk snapshot'a dahil edilir.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetOwnerName.Value = new Unity.Collections.FixedString64Bytes(SanitizeName(ownerName));
    }

    // Spawn SONRASI ek guvenlik cagrisi. Gecikmis client'lara veya late join'lere
    // ismi tekrar publish eder; degisiklik yoksa NGO no-op yapar.
    public void RefreshOwnerName(string ownerName)
    {
        if (!IsServer) return;
        var safe = new Unity.Collections.FixedString64Bytes(SanitizeName(ownerName));
        if (!NetOwnerName.Value.Equals(safe))
            NetOwnerName.Value = safe;
        _ownerName = ownerName;
    }

    private static string SanitizeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "?";
        // FixedString64Bytes 61 byte UTF-8 + null icin guvenli sinir
        if (raw.Length > 32) raw = raw.Substring(0, 32);
        return raw;
    }
}
