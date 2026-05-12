using UnityEngine;

public class DungeonGeneratorRunner : MonoBehaviour
{
    [SerializeField] private DungeonGeneratorSO _config;

    private void Start() => GenerateAndVisualize();

    public void GenerateAndVisualize()
    {
        if (_config == null)
        {
            Debug.LogError("[DungeonGeneratorRunner] Config SO atanmamış!");
            return;
        }
        var gen = new DungeonGenerator(_config);
        var data = gen.Generate();
        Debug.Log($"[DungeonGeneratorRunner] Seed: {gen.LastSeed}");
        GetComponent<DungeonVisualizer>().Visualize(data);
    }

    public void GenerateAndVisualize(int seed)
    {
        if (_config == null)
        {
            Debug.LogError("[DungeonGeneratorRunner] Config SO atanmamış!");
            return;
        }
        var overrideConfig = ScriptableObject.CreateInstance<DungeonGeneratorSO>();
        overrideConfig.maxRooms           = _config.maxRooms;
        overrideConfig.extraConnectionChance = _config.extraConnectionChance;
        overrideConfig.enableRoomMerging  = _config.enableRoomMerging;
        overrideConfig.newestBias         = _config.newestBias;
        overrideConfig.useRandomSeed      = false;
        overrideConfig.seed               = seed;

        var gen = new DungeonGenerator(overrideConfig);
        var data = gen.Generate();
        Debug.Log($"[DungeonGeneratorRunner] Seed (synced): {gen.LastSeed}");
        GetComponent<DungeonVisualizer>().Visualize(data);
    }
}
