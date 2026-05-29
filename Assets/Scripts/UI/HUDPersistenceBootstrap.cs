using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Kalici HUD bootstrap. NetworkManager GameObject'ine (zaten DontDestroyOnLoad)
/// veya benzeri persistent bir objeye eklenir. Her sahne yuklendiginde HUD
/// prefab'inin mevcut olup olmadigini kontrol eder ve yoksa instantiate eder.
///
/// Sahne degisiminde HUD prefab'i taze instantiate edilir (eski HUD scene
/// unload sirasinda zaten yok olur). Boylece her sahne kendi HUD'una sahip
/// olur ama designer her sahneye prefab eklemek zorunda kalmaz.
///
/// _excludedScenes listesindeki sahnelerde HUD spawn edilmez (orn. MainMenu).
/// _hudPrefab atanmamissa hicbir sey yapmaz; log'da bir kerelik uyari.
/// </summary>
public class HUDPersistenceBootstrap : MonoBehaviour
{
    [Tooltip("Sahne yuklendiginde instantiate edilecek HUD prefab'i " +
             "(orn. Esmanur_HUD_Root 1.prefab).")]
    [SerializeField] private GameObject _hudPrefab;

    [Tooltip("Bu sahnelerde HUD spawn EDILMEZ (orn. MainMenu, intro).")]
    [SerializeField] private string[] _excludedScenes = new[] { "MainMenu" };

    [Tooltip("True ise sahne icinde halihazirda HUD varsa (manual placed) " +
             "yenisini olusturmaz, mevcudunu kullanir.")]
    [SerializeField] private bool _respectManualPlacement = true;

    [Tooltip("Spawn / skip loglarini Console'a yaz.")]
    [SerializeField] private bool _verbose = false;

    private bool _warnedNullPrefab;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        // Bu bootstrap geç eklendiyse mevcut sahne icin de check yap.
        TrySpawnFor(SceneManager.GetActiveScene());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return; // additive loadlari atla
        TrySpawnFor(scene);
    }

    private void TrySpawnFor(Scene scene)
    {
        if (_hudPrefab == null)
        {
            if (!_warnedNullPrefab)
            {
                Debug.LogWarning("[HUDPersistenceBootstrap] _hudPrefab atanmadi, HUD spawn edilmiyor.");
                _warnedNullPrefab = true;
            }
            return;
        }

        // Excluded sahne mi?
        string sceneName = scene.name;
        if (_excludedScenes != null)
        {
            for (int i = 0; i < _excludedScenes.Length; i++)
            {
                if (string.Equals(_excludedScenes[i], sceneName, System.StringComparison.Ordinal))
                {
                    if (_verbose)
                        Debug.Log($"[HUDPersistenceBootstrap] '{sceneName}' excluded, atlandi.");
                    return;
                }
            }
        }

        // Manuel placed HUD'u koru?
        if (_respectManualPlacement && InventoryUIControllerExistsInScene(scene))
        {
            if (_verbose)
                Debug.Log($"[HUDPersistenceBootstrap] '{sceneName}' sahnesinde HUD zaten var, ek instantiate yapilmadi.");
            return;
        }

        // Instantiate et ve aktif sahneye tasi (scene unload olunca otomatik yok olsun).
        var hud = Instantiate(_hudPrefab);
        if (scene.IsValid())
            SceneManager.MoveGameObjectToScene(hud, scene);

        if (_verbose)
            Debug.Log($"[HUDPersistenceBootstrap] HUD instantiate edildi: scene='{sceneName}'.");
    }

    private static bool InventoryUIControllerExistsInScene(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].GetComponentInChildren<InventoryUIController>(true) != null)
                return true;
        }
        return false;
    }
}
