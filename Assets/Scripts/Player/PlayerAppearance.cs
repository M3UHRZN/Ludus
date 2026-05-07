using Unity.Netcode;
using UnityEngine;

public class PlayerAppearance : NetworkBehaviour
{
    [SerializeField] private GameObject[] modelPrefabs;

    public readonly NetworkVariable<byte> NetModelIndex = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner);

    public override void OnNetworkSpawn()
    {
        if (IsOwner && modelPrefabs != null && modelPrefabs.Length > 0)
        {
            NetModelIndex.Value = (byte)Random.Range(0, modelPrefabs.Length);
        }
        else if (!IsOwner)
        {
            NetModelIndex.OnValueChanged += OnModelIndexChanged;
        }

        SpawnModel();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            NetModelIndex.OnValueChanged -= OnModelIndexChanged;
        }
    }

    private void OnModelIndexChanged(byte previousValue, byte newValue)
    {
        SpawnModel();
    }

    private void SpawnModel()
    {
        Transform modelSlot = transform.Find("ModelSlot");
        if (modelSlot == null)
        {
            Debug.LogWarning($"[{nameof(PlayerAppearance)}] ModelSlot child was not found.", this);
            return;
        }

        for (int i = modelSlot.childCount - 1; i >= 0; i--)
        {
            Destroy(modelSlot.GetChild(i).gameObject);
        }

        if (modelPrefabs == null || modelPrefabs.Length == 0)
        {
            Debug.LogWarning($"[{nameof(PlayerAppearance)}] No model prefabs assigned.", this);
            return;
        }

        int modelIndex = NetModelIndex.Value;
        if (modelIndex < 0 || modelIndex >= modelPrefabs.Length)
        {
            Debug.LogWarning($"[{nameof(PlayerAppearance)}] Model index {modelIndex} is out of range.", this);
            return;
        }

        GameObject prefab = modelPrefabs[modelIndex];
        if (prefab == null)
        {
            Debug.LogWarning($"[{nameof(PlayerAppearance)}] Model prefab at index {modelIndex} is null.", this);
            return;
        }

        GameObject instance = Instantiate(prefab, modelSlot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale    = Vector3.one;

        var anim           = instance.GetComponent<Animator>();
        var playerAnimator = GetComponent<PlayerAnimator>();
        if (anim != null && playerAnimator != null)
            playerAnimator.SetAnimator(anim);
    }
}
