using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MarketTestPlayerSetup
{
    [MenuItem("VoidHaul/Market/Add Test Player to Scene")]
    public static void AddTestPlayer()
    {
        // Remove old TestPlayer if exists
        GameObject existing = GameObject.Find("TestPlayer");
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        // Disable all existing cameras so Camera.main returns ours
        foreach (Camera oldCam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(oldCam.gameObject, "Disable Old Camera");
            oldCam.gameObject.SetActive(false);
        }

        // Create player root
        GameObject player = new GameObject("TestPlayer");
        Undo.RegisterCreatedObjectUndo(player, "Create TestPlayer");
        player.transform.position = GetSpawnPosition();

        // CharacterController
        CharacterController cc = Undo.AddComponent<CharacterController>(player);
        cc.height  = 1.8f;
        cc.radius  = 0.4f;
        cc.center  = new Vector3(0f, 0.9f, 0f);

        // TestPlayer movement script
        Undo.AddComponent<TestPlayer>(player);

        // Market interact script
        MarketTestInteract interact = Undo.AddComponent<MarketTestInteract>(player);
        MarketTransactionService svc = Object.FindFirstObjectByType<MarketTransactionService>();
        MarketUIController ui = Object.FindFirstObjectByType<MarketUIController>();
        if (svc != null) interact.transactionService = svc;
        if (ui  != null) interact.marketUI = ui;

        // Market item pickup script
        Undo.AddComponent<MarketItemPickup>(player);

        // Camera
        GameObject camObj = new GameObject("Main Camera");
        Undo.RegisterCreatedObjectUndo(camObj, "Create Main Camera");
        camObj.tag = "MainCamera";
        camObj.transform.SetParent(player.transform, false);
        camObj.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        Camera cam = Undo.AddComponent<Camera>(camObj);
        cam.fieldOfView = 80f;
        cam.nearClipPlane = 0.05f;

        Undo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(camObj);
        AudioListener listener = Undo.AddComponent<AudioListener>(camObj);

        // Remove AudioListener from other cameras if any
        foreach (AudioListener al in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
        {
            if (al != listener)
            {
                Undo.DestroyObjectImmediate(al);
                break;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = player;

        Debug.Log("[MarketTestPlayerSetup] TestPlayer added. Press Play and use WASD to move.");
    }

    private static Vector3 GetSpawnPosition()
    {
        // Try to find LobbySpawnPoint
        LobbySpawnPoint spawn = Object.FindFirstObjectByType<LobbySpawnPoint>();
        if (spawn != null)
            return spawn.transform.position + Vector3.up * 0.1f;

        return new Vector3(0f, 1f, 0f);
    }
}
