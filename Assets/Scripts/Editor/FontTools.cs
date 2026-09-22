using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Puts the game font (GameFont: the file in Assets/Resources/Fonts) on every Text. Use Game Font (Open Scene) does
// the open scene and every prefab under Assets; Use Game Font (All Scenes) opens each scene in Assets/Scenes in turn
// as well and saves it. Both scene builders run the open-scene one. The pause menu needs nothing: it takes the game
// font by itself, and so does every scene while playing (GameFont switches the built-in font as scenes load).
public static class FontTools
{
    [MenuItem("Tools/Out of the Depths/Use Game Font (Open Scene)")]
    public static void ApplyToOpenScene()
    {
        GameFont.Forget();
        Font font = GameFont.Custom;
        if (font == null)
        {
            Debug.LogWarning("Game font: there is no font in Assets/Resources/" + GameFont.ResourcesFolder + ".");
            return;
        }
        int changed = ApplyToScene(SceneManager.GetActiveScene(), font) + ApplyToPrefabs(font);
        Debug.Log($"Game font: {font.name} is now on {changed} more text(s) in the open scene and the prefabs.");
    }

    [MenuItem("Tools/Out of the Depths/Use Game Font (All Scenes)")]
    public static void ApplyToAllScenes()
    {
        GameFont.Forget();
        Font font = GameFont.Custom;
        if (font == null)
        {
            Debug.LogWarning("Game font: there is no font in Assets/Resources/" + GameFont.ResourcesFolder + ".");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        string open = SceneManager.GetActiveScene().path;
        int changed = ApplyToPrefabs(font);
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int n = ApplyToScene(scene, font);
            if (n > 0)
                EditorSceneManager.SaveScene(scene);
            changed += n;
        }
        if (!string.IsNullOrEmpty(open) && SceneManager.GetActiveScene().path != open)
            EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
        Debug.Log($"Game font: {font.name} is now on {changed} more text(s) across every scene and prefab.");
    }

    // For the builders: the open scene, and never a reason to stop the build.
    internal static void ApplyToOpenSceneQuietly()
    {
        GameFont.Forget();
        Font font = GameFont.Custom;
        if (font != null)
            ApplyToScene(SceneManager.GetActiveScene(), font);
    }

    // Every Text in the scene, undoable, the scene marked dirty when anything changed.
    private static int ApplyToScene(Scene scene, Font font)
    {
        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Text text in root.GetComponentsInChildren<Text>(true))
            {
                if (text.font == font)
                    continue;
                Undo.RecordObject(text, "Game font");
                text.font = font;
                EditorUtility.SetDirty(text);
                changed++;
            }
        }
        if (changed > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        return changed;
    }

    // Every prefab under Assets that has a Text, saved back when anything changed.
    private static int ApplyToPrefabs(Font font)
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<Text>(true) == null)
                continue;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = GameFont.Apply(root, font, false);
                if (n > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed += n;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return changed;
    }
}
