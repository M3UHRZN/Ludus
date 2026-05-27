using TMPro;
using UnityEngine;

public class CameraNameTestBootstrap : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string fallbackDisplayName = "Test Player";
    [SerializeField] private Vector3 playerStartPosition = new Vector3(0f, 0.05f, 0f);

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(PlayerPrefs.GetString("DisplayName", string.Empty)))
        {
            PlayerPrefs.SetString("DisplayName", fallbackDisplayName);
            PlayerPrefs.Save();
        }

        CreateLighting();
        CreateGround();
        CreateCameraTestObstacles();
        CreatePlayerSetup();
    }

    private static void CreateLighting()
    {
        if (FindFirstObjectByType<Light>() != null)
            return;

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    private static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.05f, 0f);
        ground.transform.localScale = new Vector3(18f, 0.1f, 18f);
        ApplyColor(ground, new Color(0.18f, 0.2f, 0.22f));
    }

    private static void CreateCameraTestObstacles()
    {
        CreateWall("Back Wall Camera Test", new Vector3(0f, 1.5f, -3.2f), new Vector3(7f, 3f, 0.35f));
        CreateWall("Side Wall", new Vector3(4f, 1.5f, 0f), new Vector3(0.35f, 3f, 7f));
        CreateWall("Front Marker Wall", new Vector3(-3f, 1.5f, 4f), new Vector3(3f, 3f, 0.35f));
    }

    private static void CreateWall(string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.localScale = scale;
        ApplyColor(wall, new Color(0.32f, 0.34f, 0.38f));
    }

    private void CreatePlayerSetup()
    {
        GameObject playerObject = new GameObject("CameraNicknameTestPlayer");
        playerObject.transform.position = playerStartPosition;
        playerObject.transform.rotation = Quaternion.identity;

        CharacterController controller = playerObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        CameraNameTestPlayer player = playerObject.AddComponent<CameraNameTestPlayer>();

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Player Body Visual";
        body.transform.SetParent(playerObject.transform);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
        Destroy(body.GetComponent<Collider>());
        ApplyColor(body, new Color(0.1f, 0.45f, 0.95f));
        player.SetThirdPersonVisual(body);

        Camera camera = CreateCamera();
        SmoothCameraModeController cameraController = playerObject.AddComponent<SmoothCameraModeController>();
        cameraController.SetPlayer(player);
        cameraController.SetCamera(camera);

        CreateNameplate(playerObject.transform);
    }

    private static Camera CreateCamera()
    {
        GameObject cameraObject = new GameObject("CameraNameTest Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.03f;
        camera.fieldOfView = 70f;
        cameraObject.AddComponent<AudioListener>();
        return camera;
    }

    private static void CreateNameplate(Transform target)
    {
        GameObject nameplateObject = new GameObject("Player Nameplate");
        PlayerNameplate nameplate = nameplateObject.AddComponent<PlayerNameplate>();
        nameplate.SetFollowTarget(target);

        TextMeshPro text = nameplateObject.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3.2f;
        text.color = Color.white;
        text.outlineColor = Color.black;
        text.outlineWidth = 0.25f;
        text.rectTransform.sizeDelta = new Vector2(5f, 1f);
    }

    private static void ApplyColor(GameObject target, Color color)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        renderer.material = new Material(shader);
        renderer.material.color = color;
    }
}
