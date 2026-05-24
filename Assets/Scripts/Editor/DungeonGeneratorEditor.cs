using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DungeonGeneratorRunner))]
public class DungeonGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Dungeon (Editor)"))
            ((DungeonGeneratorRunner)target).GenerateAndVisualize();
    }
}
