using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class FlashbangSceneAuthoring
{
    private const string SceneName = "FlashbangTest";

    static FlashbangSceneAuthoring()
    {
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += TrySetupActiveScene;
        EditorApplication.delayCall += TrySetupActiveScene;
    }

    private static void TrySetupActiveScene()
    {
        if (Application.isPlaying)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != SceneName)
            return;

        bool changed = false;

        changed |= DisableLegacyObject("NetworkManager");
        changed |= DisableLegacyObject("EnemySpawner");
        changed |= DisableLegacyObject("EnemySpawnPoint_1");
        changed |= DisableLegacyObject("PatrolGroup_Room1");
        changed |= DisableLegacyObject("TestController");
        changed |= DisableLegacyObject("PlayerV0.3");
        changed |= DisableLegacyObject("Main Camera");

        GameObject player = FindOrCreatePlayer(ref changed);
        FlashbangTestPlayer playerComponent = EnsureComponent<FlashbangTestPlayer>(player, ref changed);
        FlashbangEffect flashbangEffect = EnsureComponent<FlashbangEffect>(player, ref changed);
        FlashbangTestController flashbangController = EnsureComponent<FlashbangTestController>(player, ref changed);
        AudioSource ringingSource = EnsureComponent<AudioSource>(player, ref changed);
        flashbangEffect.SetRingingSource(ringingSource);
        flashbangEffect.SetTargetPlayer(playerComponent);

        EnsurePlayerCamera(player, ref changed);
        Image overlay = EnsureOverlay(ref changed);
        flashbangEffect.SetOverlay(overlay);

        GameObject enemy = FindOrCreateEnemy(ref changed);
        FlashbangLocalEnemy enemyComponent = EnsureComponent<FlashbangLocalEnemy>(enemy, ref changed);
        enemyComponent.SetPlayerTarget(player.transform);
        enemyComponent.SetPlayerEffect(flashbangEffect);

        flashbangController.SetFlashbangEffect(flashbangEffect);
        flashbangController.SetTargetEnemy(enemyComponent);

        if (changed)
            EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool DisableLegacyObject(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj == null || !obj.activeSelf)
            return false;

        obj.SetActive(false);
        return true;
    }

    private static GameObject FindOrCreatePlayer(ref bool changed)
    {
        GameObject player = GameObject.Find("FlashbangPlayer");
        if (player != null)
            return player;

        player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "FlashbangPlayer";
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);

        CapsuleCollider primitiveCollider = player.GetComponent<CapsuleCollider>();
        if (primitiveCollider != null)
            Object.DestroyImmediate(primitiveCollider);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);

        changed = true;
        return player;
    }

    private static void EnsurePlayerCamera(GameObject player, ref bool changed)
    {
        Transform existing = player.transform.Find("FlashbangCamera");
        if (existing != null)
            return;

        GameObject cameraObj = new GameObject("FlashbangCamera");
        cameraObj.tag = "MainCamera";
        cameraObj.transform.SetParent(player.transform, false);
        cameraObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObj.AddComponent<Camera>();
        cameraObj.AddComponent<AudioListener>();
        changed = true;
    }

    private static Image EnsureOverlay(ref bool changed)
    {
        GameObject canvasObj = GameObject.Find("FlashbangCanvas");
        Canvas canvas;

        if (canvasObj == null)
        {
            canvas = new GameObject("FlashbangCanvas").AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.gameObject.AddComponent<CanvasScaler>();
            canvas.gameObject.AddComponent<GraphicRaycaster>();
            changed = true;
        }
        else
        {
            canvas = canvasObj.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                changed = true;
            }

            EnsureComponent<CanvasScaler>(canvas.gameObject, ref changed);
            EnsureComponent<GraphicRaycaster>(canvas.gameObject, ref changed);
        }

        Transform overlayTransform = canvas.transform.Find("FlashOverlay");
        Image overlay;
        if (overlayTransform == null)
        {
            GameObject overlayObj = new GameObject("FlashOverlay");
            overlayObj.transform.SetParent(canvas.transform, false);
            overlay = overlayObj.AddComponent<Image>();
            changed = true;
        }
        else
        {
            overlay = overlayTransform.GetComponent<Image>();
            if (overlay == null)
            {
                overlay = overlayTransform.gameObject.AddComponent<Image>();
                changed = true;
            }
        }

        overlay.color = new Color(1f, 1f, 1f, 0f);
        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        return overlay;
    }

    private static GameObject FindOrCreateEnemy(ref bool changed)
    {
        GameObject enemy = GameObject.Find("FlashbangEnemy");
        if (enemy != null)
            return enemy;

        enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "FlashbangEnemy";
        enemy.transform.position = new Vector3(8f, 1f, 0f);
        changed = true;
        return enemy;
    }

    private static T EnsureComponent<T>(GameObject gameObject, ref bool changed) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component != null)
            return component;

        changed = true;
        return gameObject.AddComponent<T>();
    }
}
