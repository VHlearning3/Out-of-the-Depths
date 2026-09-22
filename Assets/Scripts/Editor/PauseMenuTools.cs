using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The pause menu in the editor. Tools > Out of the Depths > Create Pause Menu Theme makes
// Assets/Settings/PauseMenuTheme.asset (if there is none) and puts it on every Pause Menu in the open scene that has
// no theme yet; Add Pause Menu Pages puts the built-in pages (Map, Settings, Keybindings) on the Pause Menu's object
// so they can be edited in the hierarchy, and gives a Settings page its default rows. Both scene builders run both.
public static class PauseMenuTools
{
    public const string ThemePath = "Assets/Settings/PauseMenuTheme.asset";

    [MenuItem("Tools/Out of the Depths/Create Pause Menu Theme")]
    public static void EnsureTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<PauseMenuTheme>(ThemePath);
        if (theme == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");
            theme = ScriptableObject.CreateInstance<PauseMenuTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Created " + ThemePath);
        }

        int assigned = 0;
        foreach (var menu in Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(menu);
            SerializedProperty prop = so.FindProperty("theme");
            if (prop == null || prop.objectReferenceValue != null)
                continue;
            prop.objectReferenceValue = theme;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            assigned++;
        }
        if (assigned > 0)
            Debug.Log($"Pause menu theme assigned to {assigned} Pause Menu(s) in the open scene.");
    }

    [MenuItem("Tools/Out of the Depths/Add Pause Menu Pages")]
    public static void EnsurePages()
    {
        var menu = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogWarning("Pause menu pages: the open scene has no Pause Menu (it lives on the Player).");
            return;
        }

        GameObject host = menu.gameObject;
        int added = 0;
        if (Object.FindFirstObjectByType<MapPage>(FindObjectsInactive.Include) == null)
        {
            Undo.AddComponent<MapPage>(host);
            added++;
        }
        if (Object.FindFirstObjectByType<SettingsPage>(FindObjectsInactive.Include) == null)
        {
            Undo.AddComponent<SettingsPage>(host);
            added++;
        }
        if (Object.FindFirstObjectByType<KeybindingsPage>(FindObjectsInactive.Include) == null)
        {
            Undo.AddComponent<KeybindingsPage>(host);
            added++;
        }
        if (Object.FindFirstObjectByType<CreditsPage>(FindObjectsInactive.Include) == null)
        {
            Undo.AddComponent<CreditsPage>(host);
            added++;
        }

        bool filled = false;
        foreach (var settings in Object.FindObjectsByType<SettingsPage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!settings.NeedsDefaultRows)
                continue;
            Undo.RecordObject(settings, "Default settings rows");
            settings.FillDefaultRows();
            EditorUtility.SetDirty(settings);
            filled = true;
        }

        if (added > 0 || filled)
        {
            EditorSceneManager.MarkSceneDirty(host.scene);
            Debug.Log($"Pause menu pages: {added} page(s) added to {host.name}" + (filled ? ", the Settings page given its default rows." : "."));
        }
    }
}
