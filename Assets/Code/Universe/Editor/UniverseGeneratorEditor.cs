using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UniverseGenerator))]
public class UniverseGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UniverseGenerator generator = (UniverseGenerator)target;

        GUILayout.Space(20);
        
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("1. GENERATE SCENE HIERARCHY", GUILayout.Height(40)))
        {
            generator.GenerateUniverseHierarchy();
        }
        
        GUILayout.Space(10);
        
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("2. BAKE SCENE TO BINARY", GUILayout.Height(40)))
        {
            generator.BakeToBinary();
        }
        
        GUI.backgroundColor = Color.white;
    }
}
