using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runtime-only setup for the FlashbangTest scene.
/// Keeps the branch isolated and avoids hand-editing the copied scene for each iteration.
/// </summary>
public static class FlashbangTestBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "FlashbangTest")
            return;

        DisableLegacySceneObjects();
        BuildLocalTestRig();
    }

    private static void DisableLegacySceneObjects()
    {
        string[] names =
        {
            "NetworkManager",
            "EnemySpawner",
            "EnemySpawnPoint_1",
            "PatrolGroup_Room1",
            "TestController",
            "PlayerV0.3",
            "Main Camera"
        };

        foreach (string objectName in names)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null)
                obj.SetActive(false);
        }
    }

    private static void BuildLocalTestRig()
    {
        if (GameObject.Find("FlashbangPlayer") != null)
            return;

        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "FlashbangPlayer";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.tag = "Player";

        Collider playerPrimitiveCollider = player.GetComponent<Collider>();
        if (playerPrimitiveCollider != null)
            Object.Destroy(playerPrimitiveCollider);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = new Vector3(0f, 1f, 0f);

        FlashbangTestPlayer testPlayer = player.AddComponent<FlashbangTestPlayer>();
        FlashbangEffect flashEffect = player.AddComponent<FlashbangEffect>();
        FlashbangTestController flashController = player.AddComponent<FlashbangTestController>();
        AudioSource audioSource = player.AddComponent<AudioSource>();
        flashEffect.SetRingingSource(audioSource);
        flashEffect.SetTargetPlayer(testPlayer);

        GameObject cameraObj = new GameObject("FlashbangCamera");
        cameraObj.tag = "MainCamera";
        cameraObj.transform.SetParent(player.transform, false);
        cameraObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        cameraObj.AddComponent<Camera>();
        cameraObj.AddComponent<AudioListener>();

        Canvas canvas = new GameObject("FlashbangCanvas").AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.gameObject.AddComponent<CanvasScaler>();
        canvas.gameObject.AddComponent<GraphicRaycaster>();

        GameObject overlayObj = new GameObject("FlashOverlay");
        overlayObj.transform.SetParent(canvas.transform, false);
        Image overlay = overlayObj.AddComponent<Image>();
        overlay.color = new Color(1f, 1f, 1f, 0f);
        RectTransform overlayRect = overlay.rectTransform;
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        flashEffect.SetOverlay(overlay);

        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = "FlashbangEnemy";
        enemy.transform.position = new Vector3(8f, 1f, 0f);
        FlashbangLocalEnemy localEnemy = enemy.AddComponent<FlashbangLocalEnemy>();
        localEnemy.SetPlayerTarget(player.transform);
        localEnemy.SetPlayerEffect(flashEffect);

        flashController.SetFlashbangEffect(flashEffect);
        flashController.SetTargetEnemy(localEnemy);
    }
}
