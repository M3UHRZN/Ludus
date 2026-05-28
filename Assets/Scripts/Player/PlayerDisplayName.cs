using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class PlayerDisplayName : NetworkBehaviour
{
    private const string DisplayNamePrefsKey = "DisplayName";
    private const int MaxDisplayNameLength = 32;

    public event Action<string> DisplayNameChanged;

    public readonly NetworkVariable<FixedString64Bytes> NetDisplayName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public string DisplayName { get; private set; } = string.Empty;

    public override void OnNetworkSpawn()
    {
        NetDisplayName.OnValueChanged += OnNetDisplayNameChanged;

        if (IsServer && NetDisplayName.Value.Length == 0)
            NetDisplayName.Value = SanitizeDisplayName(string.Empty, OwnerClientId);

        RefreshDisplayName();

        if (IsOwner)
            SubmitDisplayNameRpc(GetLocalDisplayName());
    }

    public override void OnNetworkDespawn()
    {
        NetDisplayName.OnValueChanged -= OnNetDisplayNameChanged;
    }

    private void OnNetDisplayNameChanged(FixedString64Bytes previousValue, FixedString64Bytes newValue)
    {
        RefreshDisplayName();
    }

    private void RefreshDisplayName()
    {
        string newName = NetDisplayName.Value.ToString();
        if (string.IsNullOrWhiteSpace(newName))
            newName = $"Player-{OwnerClientId}";

        DisplayName = newName;
        DisplayNameChanged?.Invoke(DisplayName);
    }

    private FixedString64Bytes GetLocalDisplayName()
    {
        string savedName = PlayerPrefs.GetString(DisplayNamePrefsKey, string.Empty);
        if (string.IsNullOrWhiteSpace(savedName))
        {
            ConnectionManager connectionManager = FindFirstObjectByType<ConnectionManager>();
            if (connectionManager != null)
                savedName = connectionManager.DisplayName;
        }

        return SanitizeDisplayName(savedName, OwnerClientId);
    }

    private static FixedString64Bytes SanitizeDisplayName(string rawName, ulong fallbackId)
    {
        string cleanName = string.IsNullOrWhiteSpace(rawName)
            ? $"Player-{fallbackId}"
            : rawName.Trim();

        if (cleanName.Length > MaxDisplayNameLength)
            cleanName = cleanName.Substring(0, MaxDisplayNameLength);

        while (cleanName.Length > 0)
        {
            try
            {
                return new FixedString64Bytes(cleanName);
            }
            catch (ArgumentException)
            {
                cleanName = cleanName.Substring(0, cleanName.Length - 1);
            }
        }

        return new FixedString64Bytes($"Player-{fallbackId}");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void SubmitDisplayNameRpc(FixedString64Bytes requestedName, RpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != OwnerClientId) return;

        NetDisplayName.Value = SanitizeDisplayName(requestedName.ToString(), senderId);
    }
}
