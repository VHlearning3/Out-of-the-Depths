using UnityEditor;
using UnityEngine;

// Inspector button and Tools menu to bake a Fish Window's hole into the scene's box wall (undoable).
[CustomEditor(typeof(FishWindow)), CanEditMultipleObjects]
public class FishWindowEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("Cut hole in the wall behind (bake into scene)"))
        {
            foreach (Object t in targets)
                ((FishWindow)t).CutHole(true);
        }
    }

    [MenuItem("Tools/Out of the Depths/Cut Holes For All Fish Windows (Open Scene)")]
    private static void CutAll()
    {
        int cut = 0;
        foreach (FishWindow window in Object.FindObjectsByType<FishWindow>(FindObjectsSortMode.None))
        {
            if (window.CutHole(true))
                cut++;
        }
        Debug.Log($"Fish windows: {cut} hole(s) cut.");
    }
}
