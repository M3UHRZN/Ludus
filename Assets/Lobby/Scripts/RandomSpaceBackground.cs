using UnityEngine;

/// <summary>
/// Her oyun baslangicinda rastgele bir skybox secer.
/// Kullanim: Hiyerarsideki herhangi bir GameObject'e ekle (orn. LobbyLayout).
/// SkyboxMaterials dizisine Inspector'dan olusturulan Skybox_*.mat materyallerini surukle-birak.
/// </summary>
public class RandomSpaceBackground : MonoBehaviour
{
    [Header("Uzay Skybox Materyalleri")]
    [Tooltip("Dynamic Space Background paketinden uretilen Skybox materyallerini buraya ekle.")]
    [SerializeField] private Material[] skyboxMaterials;

    private void Awake()
    {
        if (skyboxMaterials == null || skyboxMaterials.Length == 0)
        {
            Debug.LogWarning("[RandomSpaceBackground] Hic skybox atanmamis! Inspector'dan materyal ekle.");
            return;
        }

        // Rastgele skybox sec
        int index = Random.Range(0, skyboxMaterials.Length);
        Material selectedSkybox = skyboxMaterials[index];

        // Secilen materyali sahne skybox'ina ata
        if (selectedSkybox != null)
        {
            RenderSettings.skybox = selectedSkybox;
            DynamicGI.UpdateEnvironment(); // Isiklandirmayi guncelle
            Debug.Log($"[RandomSpaceBackground] Secilen Skybox: {selectedSkybox.name}");
        }
    }
}
