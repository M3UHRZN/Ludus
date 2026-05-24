using UnityEngine;
using UnityEditor;

public class LobbyBuilder : EditorWindow
{
    static readonly string PrefabRoot = "Assets/Lobby/Scene/ScenePrefab/Sci-Fi Styled Modular Pack/Prefabs/";

    struct PrefabPlacement
    {
        public string path;
        public Vector3 pos;
        public Quaternion rot;
        public Vector3 scale;

        public PrefabPlacement(string path, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            this.path = path;
            this.pos = pos;
            this.rot = rot;
            this.scale = scale;
        }
    }

    [MenuItem("VoidHaul/Build Lobby Scene")]
    static void BuildLobby()
    {
        if (!EditorUtility.DisplayDialog("Build Lobby",
            "Bu islem aktif sahneye 143 prefab yerlestirecek.\nOutpost with Snow duzeni kullanilacak.\n\nDevam edilsin mi?",
            "Evet, Olustur", "Iptal"))
            return;

        var parent = new GameObject("LobbyLayout");
        Undo.RegisterCreatedObjectUndo(parent, "Build Lobby");

        var placements = GetPlacements();
        int placed = 0;

        foreach (var p in placements)
        {
            string fullPath = PrefabRoot + p.path;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
            if (prefab == null)
            {
                Debug.LogWarning($"Prefab bulunamadi: {fullPath}");
                continue;
            }

            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            obj.transform.localPosition = p.pos;
            obj.transform.localRotation = p.rot;
            obj.transform.localScale = p.scale;
            placed++;
        }

        // Spawn noktasi
        var spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.SetParent(parent.transform);
        spawnPoint.transform.localPosition = new Vector3(0f, 1f, 0f);
        spawnPoint.tag = "Respawn";

        // Seed Terminal (interact noktasi)
        var terminal = new GameObject("SeedTerminal");
        terminal.transform.SetParent(parent.transform);
        terminal.transform.localPosition = new Vector3(12.17f, 0.18f, -0.08f);
        var terminalCollider = terminal.AddComponent<BoxCollider>();
        terminalCollider.isTrigger = true;
        terminalCollider.size = new Vector3(2f, 2f, 2f);

        // Elevator (gecis noktasi)
        var elevator = new GameObject("ElevatorTrigger");
        elevator.transform.SetParent(parent.transform);
        elevator.transform.localPosition = new Vector3(-12f, 0f, -18f);
        var elevatorCollider = elevator.AddComponent<BoxCollider>();
        elevatorCollider.isTrigger = true;
        elevatorCollider.size = new Vector3(3f, 3f, 3f);

        Debug.Log($"Lobby olusturuldu: {placed}/143 prefab yerlestirildi + SpawnPoint + SeedTerminal + ElevatorTrigger");
    }

    static PrefabPlacement[] GetPlacements()
    {
        return new PrefabPlacement[]
        {
            new PrefabPlacement("Decorative elements/floor_corner_ornament.prefab", new Vector3(16.35f,0.18174535f,5.85f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_celing_2.prefab", new Vector3(-11.9f,3.8182592f,-0.5877538f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Doors/glass_panel_1_with_door.prefab", new Vector3(6.0f,0.0f,0.0f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(8.0f,0.0f,6.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Arts/art_6.prefab", new Vector3(3.565167f,1.1903027f,-2.4679544f), new Quaternion(0.0f,-0.6087607f,0.0f,0.793354f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_small.prefab", new Vector3(-6.88f,0.9672138f,-6.93f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf_small.prefab", new Vector3(-15.3259735f,1.6299928f,-1.97f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-6.0f,0.0f,-2.0f), new Quaternion(0.0f,4.5597553e-06f,0.0f,-1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-14.0f,0.0f,-6.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/pilot_seat.prefab", new Vector3(-2.29f,0.19205621f,1.431718f), new Quaternion(0.0f,0.80249137f,0.0f,0.5966638f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(12.0f,4.0f,4.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant_small.prefab", new Vector3(-2.4f,0.10000002f,-4.24f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(12.0f,0.0f,-4.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_3_blue.prefab", new Vector3(7.82f,0.18174535f,5.02f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/floor_corner_ornament.prefab", new Vector3(8.349997f,0.18174535f,-5.81f), new Quaternion(0.0f,4.5597553e-06f,0.0f,-1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_small.prefab", new Vector3(-5.1f,0.9672138f,-6.91f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_1.prefab", new Vector3(-2.487628f,0.10000007f,-4.869519f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(6.0f,0.0f,2.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/pilot_seat.prefab", new Vector3(-8.32f,0.09999994f,-9.7f), new Quaternion(0.0f,-0.98465705f,0.0f,0.174501f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(8.0f,0.0f,-6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(18.0f,0.0f,4.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(8.0f,0.0f,0.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Column/column_end.prefab", new Vector3(-2.6823153f,0.09999992f,-2.5938563f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(16.0f,4.0f,4.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/console_screen.prefab", new Vector3(17.701336f,2.46f,-4.02f), new Quaternion(0.0f,-0.7071068f,0.0f,0.7071067f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_big.prefab", new Vector3(-2.19f,0.0f,-17.806442f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(16.0f,0.0f,6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Corridors/Corridor_L.prefab", new Vector3(0.0f,0.0f,-12.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(18.0f,0.0f,0.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Corridors/Corridor_T.prefab", new Vector3(0.0f,0.0f,0.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/console_celing.prefab", new Vector3(-8.663f,3.9f,-3.13f), new Quaternion(0.0f,-0.38268292f,0.0f,0.9238798f), new Vector3(0.63714623f,0.6371463f,0.63714623f)),
            new PrefabPlacement("Machines/Battery.prefab", new Vector3(2.57f,0.09999995f,-7.37f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(0.546731f,0.546731f,0.546731f)),
            new PrefabPlacement("Decorative elements/Tables/decorative_table_no_glass_only_top.prefab", new Vector3(1.9637697f,0.18174535f,-11.634892f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/hydroponic.prefab", new Vector3(-14.46f,0.09999998f,-4.22f), new Quaternion(0.0f,0.70710593f,0.0f,0.70710766f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(12.0f,0.0f,-6.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant.prefab", new Vector3(-14.37f,0.10000001f,-16.374577f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/pilot_seat.prefab", new Vector3(2.3461847f,0.19205621f,1.431718f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(6.0f,0.0f,4.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(8.0f,0.0f,-6.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Extended Primitives/Cylinder_16_extended.prefab", new Vector3(-14.61f,0.09999994f,-12.59f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(0.54385734f,0.6910685f,0.54385734f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-2.0f,0.0f,-6.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/console.prefab", new Vector3(16.980146f,0.18174535f,1.1527283f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_chair.prefab", new Vector3(-9.682846f,0.099999994f,-4.262928f), new Quaternion(0.0f,-0.48340175f,0.0f,0.8753987f), new Vector3(1.0f,1f,1f)),
            new PrefabPlacement("Machines/rotor.prefab", new Vector3(-11.710843f,-0.0f,5.1861014f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/projector stars.prefab", new Vector3(12.1700735f,0.18174535f,-0.08224347f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(12.0f,0.0f,0.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/console_celing.prefab", new Vector3(-9.81f,3.9f,-15.7f), new Quaternion(0.0f,-0.9238789f,0.0f,0.38268504f), new Vector3(0.5295832f,0.5295833f,0.5295832f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(16.0f,0.0f,6.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_celing_2.prefab", new Vector3(-11.9f,3.8182592f,-11.39f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_sofa.prefab", new Vector3(-14.260384f,0.09999999f,-10.07f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(12.0f,0.0f,6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/console.prefab", new Vector3(16.980146f,0.18174535f,0.15f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/console_screen.prefab", new Vector3(17.701336f,2.46f,3.85f), new Quaternion(0.0f,-0.7071068f,0.0f,0.7071067f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(16.0f,0.0f,4.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(8.0f,0.0f,-4.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_3_blue.prefab", new Vector3(15.824965f,0.18174535f,5.02f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_big.prefab", new Vector3(1.81f,0.0f,-17.806442f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_big.prefab", new Vector3(-6.1858745f,0.0f,-17.806442f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(12.0f,4.0f,-4.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(18.0f,0.0f,-4.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(8.0f,4.0f,4.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Corridors/Corridor_T.prefab", new Vector3(-12.0f,0.0f,-12.0f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_chair.prefab", new Vector3(1.8578064f,0.08f,-4.605772f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_1.prefab", new Vector3(-7.389693f,0.100000076f,-14.546479f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/big_screen.prefab", new Vector3(17.701336f,2.238543f,-0.16538481f), new Quaternion(0.0f,0.7071084f,0.0f,-0.7071052f), new Vector3(0.6974859f,0.6974859f,0.6974859f)),
            new PrefabPlacement("Doors/door_3.prefab", new Vector3(-12.0f,0.0f,-18.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(18.0f,0.0f,4.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/hydroponic.prefab", new Vector3(-8.166097f,0.09999998f,2.2792783f), new Quaternion(0.0f,1.0f,0.0f,2.3245811e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Tables/decorative_table_glass.prefab", new Vector3(3.0166616f,0.099999934f,-3.1292384f), new Quaternion(0.0f,-0.3904209f,0.0f,0.92063653f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf.prefab", new Vector3(-4.05f,0.099999905f,-14.854102f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_3_blue.prefab", new Vector3(7.82f,0.18174535f,-4.984302f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(8.0f,0.0f,4.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(16.0f,0.0f,-6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_1_blue.prefab", new Vector3(5.160542f,0.10000014f,-2.790232f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf.prefab", new Vector3(-7.7406406f,0.100000076f,-2.7457414f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf.prefab", new Vector3(-0.055684783f,0.099999905f,-14.854102f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant_desk.prefab", new Vector3(1.9224995f,1.2720482f,-10.73f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Tables/desk.prefab", new Vector3(2.08f,0.09999999f,3.56f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(16.0f,0.0f,-6.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/pilot_seat.prefab", new Vector3(-9.5100565f,0.09999994f,-8.290352f), new Quaternion(0.0f,-0.835677f,0.0f,0.54922116f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant.prefab", new Vector3(-9.66005f,0.10000001f,-16.374577f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(16.0f,4.0f,0.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(8.0f,4.0f,0.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(12.0f,4.0f,0.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(16.0f,4.0f,-4.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf.prefab", new Vector3(-5.975668f,1.0398889f,-8.718787f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(0.58770204f,0.63815904f,1f)),
            new PrefabPlacement("Decorative elements/console.prefab", new Vector3(16.980146f,0.18174535f,-0.85f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant.prefab", new Vector3(16.67524f,0.18174535f,4.33f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(12.0f,0.0f,-6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(6.0f,0.0f,-4.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf.prefab", new Vector3(-5.975668f,1.0398889f,-15.291f), new Quaternion(0.0f,4.5597553e-06f,0.0f,-1.0f), new Vector3(0.58770204f,0.63815904f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-14.0f,0.0f,-18.0f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Doors/glass_panel_1_with_door.prefab", new Vector3(-6.0f,0.0f,0.0f), new Quaternion(0.0f,-0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-10.0f,0.0f,-18.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant_small.prefab", new Vector3(-4.4011493f,0.10000002f,-2.3817706f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/shelf_small.prefab", new Vector3(-10.01f,1.63f,3.2812357f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant.prefab", new Vector3(16.67524f,0.18174535f,-4.1067376f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Doors/glass_panel_1_with_door.prefab", new Vector3(-12.0f,0.0f,-6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_small.prefab", new Vector3(-5.1780977f,0.9672138f,-5.1636543f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_chair.prefab", new Vector3(5.18f,0.08f,-1.87f), new Quaternion(0.0f,-0.69975036f,0.0f,0.71438754f), new Vector3(1.0f,1f,1f)),
            new PrefabPlacement("Decorative elements/console.prefab", new Vector3(-9.41f,0.099999934f,-2.44f), new Quaternion(0.0f,-0.3826829f,0.0f,0.9238798f), new Vector3(0.7375735f,1f,0.7375735f)),
            new PrefabPlacement("Decorative elements/console_celing.prefab", new Vector3(-13.697f,3.9f,-15.7f), new Quaternion(0.0f,-0.92522573f,0.0f,-0.37941712f), new Vector3(0.5295832f,0.5295833f,0.5295832f)),
            new PrefabPlacement("Lights/light_celing_2.prefab", new Vector3(-0.6892671f,3.8079436f,-0.06356643f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-10.0f,0.0f,-6.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1.0f,1f,1.0f)),
            new PrefabPlacement("Machines/storage_container_small.prefab", new Vector3(-5.9981523f,0.0f,-5.970154f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/Battery.prefab", new Vector3(2.57f,1.63f,-7.37f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(0.546731f,0.546731f,0.546731f)),
            new PrefabPlacement("Decorative elements/decorative_chair.prefab", new Vector3(2.15f,0.08f,-2.17f), new Quaternion(0.0f,0.9634657f,0.0f,0.26783183f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(-6.0f,0.0f,2.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/Battery.prefab", new Vector3(2.57f,1.63f,-8.72f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(0.546731f,0.546731f,0.546731f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(12.0f,0.0f,6.0f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(16.0f,0.0f,-4.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Extended Primitives/Cylinder_16_extended.prefab", new Vector3(-14.61f,0.09999994f,-15.24f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(0.54385734f,0.6910685f,0.54385734f)),
            new PrefabPlacement("Decorative elements/floor_corner_ornament.prefab", new Vector3(16.35f,0.18174535f,-5.81f), new Quaternion(0.0f,4.5597553e-06f,0.0f,-1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant.prefab", new Vector3(-14.410451f,0.09999997f,-7.3970923f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/floor_corner_ornament.prefab", new Vector3(12.35f,0.18174535f,-5.81f), new Quaternion(0.0f,4.5597553e-06f,0.0f,-1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Doors/glass_panel_1_with_door.prefab", new Vector3(0.0f,0.0f,-6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_1.prefab", new Vector3(-1.23f,0.100000076f,-14.546479f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(18.0f,0.0f,0.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/Battery.prefab", new Vector3(2.57f,0.09999995f,-8.72f), new Quaternion(0.0f,0.707106f,0.0f,0.7071076f), new Vector3(0.546731f,0.546731f,0.546731f)),
            new PrefabPlacement("Lights/light_celing_2.prefab", new Vector3(-11.9f,3.8182592f,-5.59f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(2.0f,0.0f,-6.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/floor_corner_ornament.prefab", new Vector3(8.349997f,0.18174535f,5.85f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Corridors/Corridor_L.prefab", new Vector3(-12.0f,0.0f,0.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/hydroponic.prefab", new Vector3(-14.39f,0.09999998f,-0.14f), new Quaternion(0.0f,0.70710593f,0.0f,0.70710766f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Windows/window_big_blocker_3.prefab", new Vector3(6.0f,0.0f,-2.0f), new Quaternion(0.0f,4.5597553e-06f,0.0f,-1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Tables/desk.prefab", new Vector3(-2.3f,0.09999999f,3.56f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Arts/art_5.prefab", new Vector3(2.505083f,1.1903027f,-3.7262146f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(6.0f,0.0f,-4.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/rotor.prefab", new Vector3(-11.710843f,0.0f,7.54f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/Column/column_end.prefab", new Vector3(-2.5861166f,0.09999992f,-9.4124775f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/floor_corner_ornament.prefab", new Vector3(12.35f,0.18174535f,5.85f), new Quaternion(0.0f,1.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_celing_1.prefab", new Vector3(11.51335f,3.818255f,0.06958035f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Lights/light_wall_3_blue.prefab", new Vector3(15.824965f,0.18174535f,-4.984302f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Machines/container_small.prefab", new Vector3(-6.94f,0.9672138f,-5.09f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(12.0f,0.0f,4.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(6.0f,0.0f,4.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/decorative_plant_desk.prefab", new Vector3(1.9224995f,1.2720482f,-12.293505f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/computer_station.prefab", new Vector3(-8.992672f,0.09999995f,-15.130316f), new Quaternion(0.0f,-0.3826829f,0.0f,0.9238798f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_3.prefab", new Vector3(18.0f,0.0f,-4.0f), new Quaternion(0.0f,0.7071092f,0.0f,-0.7071043f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(8.0f,4.0f,-4.0f), new Quaternion(1.0f,0.0f,0.0f,2.324581e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Walls/decorative_wall_1.prefab", new Vector3(8.0f,0.0f,6.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Decorative elements/hydroponic.prefab", new Vector3(-12.17f,0.09999998f,2.2792783f), new Quaternion(0.0f,1.0f,0.0f,2.3245811e-06f), new Vector3(1f,1f,1f)),
            new PrefabPlacement("Floors/floor_5.prefab", new Vector3(16.0f,0.0f,0.0f), new Quaternion(0.0f,0.0f,0.0f,1.0f), new Vector3(1f,1f,1f)),
        };
    }
}
