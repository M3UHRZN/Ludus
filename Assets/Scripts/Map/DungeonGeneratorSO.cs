using UnityEngine;

[CreateAssetMenu(fileName = "DungeonGeneratorConfig", menuName = "VoidHaul/Dungeon Generator Config")]
public class DungeonGeneratorSO : ScriptableObject
{
    [Header("Oda Sayısı")]
    [Min(5)]
    public int maxRooms = 30;

    [Header("Döngü Oluşturma")]
    [Range(0f, 1f)]
    public float extraConnectionChance = 0.15f;

    [Header("Oda Birleştirme")]
    public bool enableRoomMerging = true;

    [Header("Rastgelelik")]
    [Tooltip("false ise seed değeri kullanılır")]
    public bool useRandomSeed = true;
    public int seed = 0;
}
