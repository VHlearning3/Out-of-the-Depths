using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// The Player Manager's Inspector: every other script on the Player, in folding groups, each with its on/off box, its
// settings (its own Inspector, drawn inline), and a button to find its script file. Groups and scripts remember
// whether they are open for the editor session.
[CustomEditor(typeof(PlayerManager))]
public class PlayerManagerEditor : Editor
{
    private readonly Dictionary<Component, Editor> editors = new Dictionary<Component, Editor>();
    private string filter = "";

    private void OnDisable()
    {
        foreach (Editor e in editors.Values)
            if (e != null)
                DestroyImmediate(e);
        editors.Clear();
    }

    public override void OnInspectorGUI()
    {
        var manager = (PlayerManager)target;
        if (!manager.isActiveAndEnabled)
        {
            EditorGUILayout.HelpBox("Switched off: every script on the Player shows the normal way. Tick the box to fold them in here.", MessageType.Info);
            return;
        }
        manager.Tidy(true);   // scripts added since (in play, or by hand) fold in too

        // The scripts, sorted into their groups in the order of the list.
        var grouped = new Dictionary<string, List<Component>>();
        foreach (Component c in manager.GetComponents<Component>())
        {
            if (c == null || c == manager || c is Transform)
                continue;
            string group = PlayerManager.GroupOf(c);
            if (!grouped.TryGetValue(group, out List<Component> list))
                grouped[group] = list = new List<Component>();
            list.Add(c);
        }

        EditorGUILayout.HelpBox("The Player's scripts, folded into groups. Untick Player Manager to see them the normal way.", MessageType.None);
        filter = EditorGUILayout.TextField("Find", filter);
        EditorGUILayout.Space(2f);

        var order = new List<string>();
        foreach (var (group, _) in PlayerManager.Groups)
            order.Add(group);
        order.Add(PlayerManager.Other);
        foreach (string group in order)
            if (grouped.TryGetValue(group, out List<Component> list))
                DrawGroup(group, list);
    }

    private void DrawGroup(string group, List<Component> list)
    {
        bool filtering = !string.IsNullOrEmpty(filter);
        var shown = new List<Component>();
        foreach (Component c in list)
            if (!filtering || Label(c).IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                shown.Add(c);
        if (shown.Count == 0)
            return;

        string key = "PlayerManager.group." + group;
        bool open = filtering || SessionState.GetBool(key, false);
        bool now = EditorGUILayout.BeginFoldoutHeaderGroup(open, $"{group}  ({shown.Count})");
        EditorGUILayout.EndFoldoutHeaderGroup();
        if (!filtering && now != open)
            SessionState.SetBool(key, now);
        if (!now)
            return;

        EditorGUI.indentLevel++;
        foreach (Component c in shown)
            DrawComponent(c);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4f);
    }

    private void DrawComponent(Component c)
    {
        string key = "PlayerManager.script." + c.GetInstanceID();
        bool open = SessionState.GetBool(key, false);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        bool now = EditorGUILayout.Foldout(open, Label(c), true, EditorStyles.foldoutHeader);
        if (now != open)
            SessionState.SetBool(key, now);
        GUILayout.FlexibleSpace();
        if (c is Behaviour behaviour)
        {
            bool on = EditorGUILayout.Toggle(behaviour.enabled, GUILayout.Width(18f));
            if (on != behaviour.enabled)
            {
                Undo.RecordObject(behaviour, (on ? "Switch on " : "Switch off ") + Label(c));
                behaviour.enabled = on;
                EditorUtility.SetDirty(behaviour);
            }
        }
        if (c is MonoBehaviour mono && GUILayout.Button("Script", EditorStyles.miniButton, GUILayout.Width(48f)))
            EditorGUIUtility.PingObject(MonoScript.FromMonoBehaviour(mono));
        EditorGUILayout.EndHorizontal();

        if (now)
        {
            if (!editors.TryGetValue(c, out Editor editor) || editor == null)
            {
                editor = CreateEditor(c);
                editors[c] = editor;
            }
            EditorGUILayout.Space(2f);
            editor.OnInspectorGUI();
        }
        EditorGUILayout.EndVertical();
    }

    // "Swim Controller", as Unity names it in its own header.
    private static string Label(Component c) => ObjectNames.NicifyVariableName(c.GetType().Name);
}
