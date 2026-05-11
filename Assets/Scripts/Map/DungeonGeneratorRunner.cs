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
        var data = new DungeonGenerator(_config).Generate();
        GetComponent<DungeonVisualizer>().Visualize(data);
    }
}
