using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class DungeonGeneratorRunner : NetworkBehaviour
{
    [SerializeField] private DungeonGeneratorSO _config;

    private readonly NetworkVariable<int> _dungeonSeed = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            int seedToUse = _config.useRandomSeed ? System.Environment.TickCount : _config.seed;
            _dungeonSeed.Value = seedToUse;
            GenerateAndVisualize(seedToUse);
        }
        else
        {
            if (_dungeonSeed.Value != 0)
            {
                GenerateAndVisualize(_dungeonSeed.Value);
            }
            else
            {
                _dungeonSeed.OnValueChanged += OnSeedChanged;
            }
        }
    }

    private void OnSeedChanged(int previousValue, int newValue)
    {
        _dungeonSeed.OnValueChanged -= OnSeedChanged;
        GenerateAndVisualize(newValue);
    }

    public override void OnNetworkDespawn()
    {
        _dungeonSeed.OnValueChanged -= OnSeedChanged;
    }

    [ContextMenu("Generate Dungeon")]
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
        GetComponent<DungeonVisualizer>().Visualize(data, gen.LastSeed);
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
        GetComponent<DungeonVisualizer>().Visualize(data, seed);
    }
}
