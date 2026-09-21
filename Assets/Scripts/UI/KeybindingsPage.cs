using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The Keybindings page of the pause menu: the keyboard and mouse bindings of the Player actions, each a key cap you
// click and then press the new key for (Escape cancels). Changes are remembered in PlayerPrefs and put back when the
// game starts; Reset all restores the defaults. Which actions show, and what they are called, is an Inspector list
// (empty = the swim set: Move, Jump as Swim up, Crouch as Swim down, Sprint, Interact, Attack).
public class KeybindingsPage : MonoBehaviour, IPauseMenuPage
{
    [System.Serializable]
    public class Entry
    {
        public string action;
        public string label;
    }

    [SerializeField] private string pageTitle = "Keybindings";
    [Tooltip("The input actions asset. Found automatically from the Swim Controller.")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string actionMap = "Player";
    [Tooltip("Only bindings in this control scheme are shown: the keyboard and mouse ones.")]
    [SerializeField] private string controlScheme = "Keyboard&Mouse";
    [Tooltip("The actions to show, in order, and what to call them. Empty = the swim set.")]
    [SerializeField] private List<Entry> entries = new List<Entry>();

    private const string PrefsKey = "keybindings.overrides";

    private InputActionRebindingExtensions.RebindingOperation rebinding;
    private InputAction listeningAction;
    private int listeningBinding = -1;
    private bool listeningWasEnabled;
    private string statusNote;

    public string PageTitle => pageTitle;
    public int Order => 20;

    private void Awake()
    {
        FindAsset();
        if (inputActions != null && PlayerPrefs.HasKey(PrefsKey))
            inputActions.LoadBindingOverridesFromJson(PlayerPrefs.GetString(PrefsKey));
    }

    private void OnDisable()
    {
        CancelRebind();
    }

    private void FindAsset()
    {
        if (inputActions == null)
        {
            SwimController swimmer = FindFirstObjectByType<SwimController>();
            if (swimmer != null)
                inputActions = swimmer.InputActions;
        }
        if (entries.Count == 0)
        {
            entries.Add(new Entry { action = "Move", label = "Move" });
            entries.Add(new Entry { action = "Jump", label = "Swim up" });
            entries.Add(new Entry { action = "Crouch", label = "Swim down" });
            entries.Add(new Entry { action = "Sprint", label = "Sprint" });
            entries.Add(new Entry { action = "Interact", label = "Interact" });
            entries.Add(new Entry { action = "Attack", label = "Attack" });
        }
    }

    public void OnPageShown()
    {
        FindAsset();
        statusNote = null;
    }

    public void DrawPage()
    {
        GUIStyle note = MenuGUI.NoteStyle ?? GUI.skin.label;
        if (inputActions == null)
        {
            GUILayout.Label("No input actions asset: assign one on the Keybindings Page component.", note);
            return;
        }
        InputActionMap map = inputActions.FindActionMap(actionMap);
        if (map == null)
        {
            GUILayout.Label($"The asset has no action map called {actionMap}.", note);
            return;
        }

        MenuGUI.Heading("Keyboard and mouse");
        foreach (Entry entry in entries)
        {
            InputAction action = map.FindAction(entry.action);
            if (action == null)
                continue;
            string name = string.IsNullOrEmpty(entry.label) ? action.name : entry.label;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || !InScheme(binding))
                    continue;
                string label = binding.isPartOfComposite ? name + "   " + Capitalise(binding.name) : name;
                bool listening = rebinding != null && listeningAction == action && listeningBinding == i;
                MenuGUI.BeginRow(label);
                if (MenuGUI.KeyCap(listening ? "press a key" : Display(action, i), listening) && !listening)
                    StartRebind(action, i);
                MenuGUI.EndRow();
            }
        }

        GUILayout.Space(14f);
        GUILayout.BeginHorizontal();
        if (MenuGUI.Button("Reset all", MenuGUI.SmallButtonStyle))
            ResetAll();
        if (rebinding != null && MenuGUI.Button("Cancel", MenuGUI.SmallButtonStyle))
            CancelRebind();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(6f);
        if (!string.IsNullOrEmpty(statusNote))
            GUILayout.Label(statusNote, note);
        GUILayout.Label("Click a key, then press the new one; Escape cancels. Mouse look and the hotbar number keys cannot be changed here.", note);
    }

    private bool InScheme(InputBinding binding)
    {
        if (!string.IsNullOrEmpty(controlScheme) && !string.IsNullOrEmpty(binding.groups))
            return binding.groups.Contains(controlScheme);
        string path = binding.effectivePath ?? "";
        return path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>");
    }

    private static string Display(InputAction action, int index)
    {
        string path = action.bindings[index].effectivePath;
        if (string.IsNullOrEmpty(path))
            return "none";
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    private static string Capitalise(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        return char.ToUpperInvariant(text[0]) + text.Substring(1);
    }

    private void StartRebind(InputAction action, int index)
    {
        CancelRebind();
        listeningAction = action;
        listeningBinding = index;
        listeningWasEnabled = action.enabled;
        action.Disable();
        PauseMenu.CaptureInput(true);
        statusNote = null;
        rebinding = action.PerformInteractiveRebinding(index)
            .WithControlsExcluding("<Mouse>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                InputAction done = listeningAction;
                int doneIndex = listeningBinding;
                FinishRebind();
                Save();
                if (done != null)
                    statusNote = SameKeyNote(done, doneIndex);
            })
            .OnCancel(operation => FinishRebind())
            .Start();
    }

    private void FinishRebind()
    {
        rebinding?.Dispose();
        rebinding = null;
        if (listeningAction != null && listeningWasEnabled)
            listeningAction.Enable();
        listeningAction = null;
        listeningBinding = -1;
        PauseMenu.CaptureInput(false);
    }

    private void CancelRebind()
    {
        if (rebinding == null)
            return;
        rebinding.Cancel();   // OnCancel finishes up
        if (rebinding != null)
            FinishRebind();
    }

    // A warning if the key just chosen is already used by something else on this page.
    private string SameKeyNote(InputAction action, int index)
    {
        string path = action.bindings[index].effectivePath;
        if (string.IsNullOrEmpty(path) || inputActions == null)
            return null;
        InputActionMap map = inputActions.FindActionMap(actionMap);
        if (map == null)
            return null;
        foreach (InputAction other in map.actions)
        {
            for (int i = 0; i < other.bindings.Count; i++)
            {
                if (other == action && i == index)
                    continue;
                if (other.bindings[i].isComposite || !InScheme(other.bindings[i]))
                    continue;
                if (other.bindings[i].effectivePath == path)
                    return $"{Display(action, index)} is also used by {other.name}.";
            }
        }
        return null;
    }

    private void Save()
    {
        if (inputActions == null)
            return;
        PlayerPrefs.SetString(PrefsKey, inputActions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    private void ResetAll()
    {
        CancelRebind();
        if (inputActions == null)
            return;
        inputActions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        statusNote = "Back to the defaults.";
    }
}
