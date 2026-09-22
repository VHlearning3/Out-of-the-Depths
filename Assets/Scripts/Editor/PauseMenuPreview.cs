using UnityEditor;
using UnityEngine;

// The pause menu without pressing Play: Tools > Out of the Depths > Preview Pause Menu (or the button at the top of
// a Pause Menu Theme asset) ticks Preview In Editor on the open scene's Pause Menu, which then draws itself open in
// the Game view; this keeps the Game view refreshing while it does, so the eases move and theme edits show at once.
[InitializeOnLoad]
public static class PauseMenuPreview
{
    private const string MenuPath = "Tools/Out of the Depths/Preview Pause Menu";
    private static readonly System.Type gameViewType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
    private static double nextRefresh;

    static PauseMenuPreview()
    {
        EditorApplication.update += Drive;
    }

    private static void Drive()
    {
        if (Application.isPlaying || !PauseMenu.AnyPreviewing)
            return;
        double now = EditorApplication.timeSinceStartup;
        if (now < nextRefresh)
            return;
        nextRefresh = now + 1.0 / 60.0;
        EditorApplication.QueuePlayerLoopUpdate();
        if (gameViewType == null)
            return;
        foreach (Object window in Resources.FindObjectsOfTypeAll(gameViewType))
            (window as EditorWindow)?.Repaint();
    }

    private static PauseMenu SceneMenu => Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);

    public static bool IsOn
    {
        get
        {
            PauseMenu menu = SceneMenu;
            return menu != null && menu.PreviewInEditor;
        }
    }

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        PauseMenu menu = SceneMenu;
        if (menu == null)
        {
            Debug.LogWarning("Preview pause menu: the open scene has no Pause Menu (it lives on the Player).");
            return;
        }
        Undo.RecordObject(menu, "Preview pause menu");
        menu.PreviewInEditor = !menu.PreviewInEditor;
        EditorUtility.SetDirty(menu);
        if (menu.PreviewInEditor)
        {
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            Debug.Log("Pause menu preview on: the Game view shows the menu. Edit the theme asset or the pages on the Player and watch; Tools > Out of the Depths > Preview Pause Menu turns it off.");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleValid()
    {
        Menu.SetChecked(MenuPath, IsOn);
        return !Application.isPlaying;
    }
}

[CustomEditor(typeof(PauseMenuTheme))]
public class PauseMenuThemeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        bool on = PauseMenuPreview.IsOn;
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button(on ? "Stop previewing in the Game view" : "Preview in the Game view (no Play needed)", GUILayout.Height(28f)))
                PauseMenuPreview.Toggle();
        }
        EditorGUILayout.Space();
        DrawDefaultInspector();
    }
}
