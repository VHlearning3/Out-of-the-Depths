using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// The Settings page of the pause menu. What it shows is the Rows list on this component (on the Player), edited in
// the Inspector: headings and notes, the built-in settings (resolution, fullscreen, v-sync, quality, master volume,
// mouse sensitivity, a reset button) and rows of your own, a switch, slider, stepper or button with a label that
// fires its event when used. Reorder, rename, remove or add rows there. Rows with a Save Key are remembered in
// PlayerPrefs and put back (their event fires) when the game starts, as are volume, sensitivity, v-sync and quality;
// resolution and fullscreen the game remembers by itself. An empty list gets the default rows.
public class SettingsPage : MonoBehaviour, IPauseMenuPage
{
    public enum RowType
    {
        Heading,
        Note,
        Resolution,
        Fullscreen,
        VSync,
        Quality,
        MasterVolume,
        MouseSensitivity,
        ResetToDefaults,
        Switch,
        Slider,
        Stepper,
        Button,
        HudSize,
        HudAutoHide
    }

    [Serializable]
    public class Row
    {
        [Tooltip("A heading or a note, one of the built-in settings, or a switch / slider / stepper / button of your own that fires its event.")]
        public RowType type = RowType.Switch;
        [Tooltip("The text on the left of the row (for a heading or a note: the text itself).")]
        public string label = "";
        [Tooltip("The text on the button.")]
        public string buttonText = "Reset to defaults";
        [Tooltip("The ends of the slider.")]
        public float min = 0f;
        public float max = 1f;
        [Tooltip("Show the slider as a percentage of its range instead of the number.")]
        public bool percent = false;
        [Tooltip("The choices the stepper steps through.")]
        public string[] options = new string[0];
        [Tooltip("The switch starts on.")]
        public bool defaultOn = false;
        [Tooltip("Where the slider starts.")]
        public float defaultValue = 0f;
        [Tooltip("The choice the stepper starts on (0 = the first).")]
        public int defaultIndex = 0;
        [Tooltip("Remembered in PlayerPrefs under this key and put back when the game starts. Empty = forgotten on quit.")]
        public string saveKey = "";
        public UnityEvent<bool> onSwitched = new UnityEvent<bool>();
        public UnityEvent<float> onValueChanged = new UnityEvent<float>();
        public UnityEvent<int> onPicked = new UnityEvent<int>();
        public UnityEvent onPressed = new UnityEvent();

        [NonSerialized] public float value;       // the live value: 0 / 1 for a switch, the slider number, the stepper index
        [NonSerialized] public bool valueKnown;
    }

    [SerializeField] private string pageTitle = "Settings";
    [Tooltip("For the mouse sensitivity row. Found automatically.")]
    [SerializeField] private SwimController swimmer;
    [Tooltip("The page, top to bottom. Reorder, rename, remove or add rows here; the button below puts the defaults back.")]
    [SerializeField] private List<Row> rows = new List<Row>();
    [SerializeField, HideInInspector] private bool rowsFilled;

    private const string VolumeKey = GameAudio.VolumeKey;
    private const string SensitivityKey = "settings.mouseSensitivity";
    private const string VSyncKey = "settings.vSync";
    private const string QualityKey = "settings.quality";

    // The project's own quality level and v-sync, noted once before any remembered choice is applied.
    private static int projectQuality = -1;
    private static bool projectVSync;

    private readonly List<Vector2Int> sizes = new List<Vector2Int>();
    private string[] sizeLabels = new string[0];
    private int sizeIndex;
    private float defaultSensitivity = -1f;
    private const float MenuDefaultSensitivity = 0.12f;   // Swim Controller's own default, for the main menu

    public string PageTitle => pageTitle;
    public int Order => 10;
    public bool NeedsDefaultRows => rows.Count == 0 && !rowsFilled;

    private void Awake()
    {
        if (NeedsDefaultRows)
            FillDefaultRows();
        AddHudSizeRow();
        if (projectQuality < 0)
        {
            projectQuality = QualitySettings.GetQualityLevel();
            projectVSync = QualitySettings.vSyncCount > 0;
        }
        if (PlayerPrefs.HasKey(QualityKey))
            QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey), 0, QualitySettings.names.Length - 1), true);
        if (PlayerPrefs.HasKey(VSyncKey))
            QualitySettings.vSyncCount = PlayerPrefs.GetInt(VSyncKey) > 0 ? 1 : 0;
        // The master volume (with the game's mix trim under it) is applied as the game starts: GameAudio.
    }

    private void Start()
    {
        FindSwimmer();
        if (swimmer != null)
        {
            defaultSensitivity = swimmer.MouseSensitivity;
            if (PlayerPrefs.HasKey(SensitivityKey))
                swimmer.MouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey);
        }
        // Your own rows: the remembered (or default) value, and their event so the game applies it.
        foreach (Row row in rows)
            if (IsOwn(row) && !row.valueKnown)
                SetValue(row, Remembered(row), false);
    }

    private void FindSwimmer()
    {
        if (swimmer == null)
            swimmer = FindFirstObjectByType<SwimController>();
    }

    public void OnPageShown()
    {
        FindSwimmer();
        RefreshSizes();
    }

    // Every size the monitor offers (once each, largest last), with the current one picked.
    private void RefreshSizes()
    {
        sizes.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            var size = new Vector2Int(r.width, r.height);
            if (!sizes.Contains(size))
                sizes.Add(size);
        }
        var current = new Vector2Int(Screen.width, Screen.height);
        if (!sizes.Contains(current))
            sizes.Add(current);
        sizes.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        sizeLabels = new string[sizes.Count];
        for (int i = 0; i < sizes.Count; i++)
            sizeLabels[i] = $"{sizes[i].x} x {sizes[i].y}";
        sizeIndex = Mathf.Max(0, sizes.IndexOf(current));
    }

    public void DrawPage()
    {
        if (rows.Count == 0)
        {
            GUILayout.Label("No rows: add some to the Settings Page component on the Player.", MenuGUI.NoteStyle ?? GUI.skin.label);
            return;
        }
        foreach (Row row in rows)
            if (row != null)
                Draw(row);
    }

    private void Draw(Row row)
    {
        switch (row.type)
        {
            case RowType.Heading:
                MenuGUI.Heading(row.label);
                break;

            case RowType.Note:
                GUILayout.Space(12f);
                GUILayout.Label(row.label, MenuGUI.NoteStyle ?? GUI.skin.label);
                break;

            case RowType.Resolution:
            {
                int pick = MenuGUI.StepperRow(row.label, sizeIndex, sizeLabels);
                if (pick != sizeIndex && pick >= 0 && pick < sizes.Count)
                {
                    sizeIndex = pick;
                    Screen.SetResolution(sizes[pick].x, sizes[pick].y, Screen.fullScreenMode);
                }
                break;
            }

            case RowType.Fullscreen:
            {
                bool on = Screen.fullScreenMode != FullScreenMode.Windowed;
                bool now = MenuGUI.SwitchRow(row.label, on);
                if (now != on)
                    Screen.fullScreenMode = now ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                break;
            }

            case RowType.VSync:
            {
                bool on = QualitySettings.vSyncCount > 0;
                bool now = MenuGUI.SwitchRow(row.label, on);
                if (now != on)
                {
                    QualitySettings.vSyncCount = now ? 1 : 0;
                    PlayerPrefs.SetInt(VSyncKey, now ? 1 : 0);
                }
                break;
            }

            case RowType.Quality:
            {
                int level = QualitySettings.GetQualityLevel();
                int pick = MenuGUI.StepperRow(row.label, level, QualitySettings.names);
                if (pick != level)
                {
                    QualitySettings.SetQualityLevel(pick, true);
                    PlayerPrefs.SetInt(QualityKey, pick);
                }
                break;
            }

            case RowType.MasterVolume:
            {
                float volume = GameAudio.Master;   // the player's own share; the game's mix trim sits under it
                float now = MenuGUI.SliderRow(row.label, volume, 0f, 1f, Mathf.RoundToInt(volume * 100f) + "%");
                if (!Mathf.Approximately(now, volume))
                    GameAudio.Master = now;
                break;
            }

            case RowType.MouseSensitivity:
            {
                // No player (the main menu): the saved value, which the game picks up when it starts.
                float sensitivity = swimmer != null ? swimmer.MouseSensitivity : PlayerPrefs.GetFloat(SensitivityKey, MenuDefaultSensitivity);
                float now = MenuGUI.SliderRow(row.label, sensitivity, row.min, row.max, sensitivity.ToString("0.00"));
                if (!Mathf.Approximately(now, sensitivity))
                {
                    if (swimmer != null)
                        swimmer.MouseSensitivity = now;
                    PlayerPrefs.SetFloat(SensitivityKey, now);
                }
                break;
            }

            case RowType.HudSize:
            {
                float size = UIScale.Hud;
                float now = MenuGUI.SliderRow(row.label, size, UIScale.Min, UIScale.Max, Mathf.RoundToInt(size * 100f) + "%");
                if (!Mathf.Approximately(now, size))
                    UIScale.Set(Mathf.Round(now * 20f) / 20f);   // in 5% steps
                break;
            }

            case RowType.HudAutoHide:
                HudAutoHide.Enabled = MenuGUI.SwitchRow(row.label, HudAutoHide.Enabled);
                break;

            case RowType.ResetToDefaults:
                if (MenuGUI.ButtonRow(row.label, row.buttonText))
                    ResetToDefaults();
                break;

            case RowType.Switch:
            {
                Know(row);
                bool on = row.value > 0.5f;
                bool now = MenuGUI.SwitchRow(row.label, on);
                if (now != on)
                    SetValue(row, now ? 1f : 0f, true);
                break;
            }

            case RowType.Slider:
            {
                Know(row);
                float now = MenuGUI.SliderRow(row.label, row.value, row.min, row.max, Shown(row));
                if (!Mathf.Approximately(now, row.value))
                    SetValue(row, now, true);
                break;
            }

            case RowType.Stepper:
            {
                Know(row);
                int index = Mathf.RoundToInt(row.value);
                int pick = MenuGUI.StepperRow(row.label, index, row.options);
                if (pick != index)
                    SetValue(row, pick, true);
                break;
            }

            case RowType.Button:
                if (MenuGUI.ButtonRow(row.label, row.buttonText))
                    row.onPressed.Invoke();
                break;
        }
    }

    // ---- rows of your own -----------------------------------------------------------------------------------------

    private static bool IsOwn(Row row)
    {
        return row != null && (row.type == RowType.Switch || row.type == RowType.Slider || row.type == RowType.Stepper);
    }

    // A row added while playing has no value yet: the remembered or default one, applied.
    private static void Know(Row row)
    {
        if (!row.valueKnown)
            SetValue(row, Remembered(row), false);
    }

    private static float Default(Row row)
    {
        switch (row.type)
        {
            case RowType.Switch:
                return row.defaultOn ? 1f : 0f;
            case RowType.Slider:
                return Mathf.Clamp(row.defaultValue, Mathf.Min(row.min, row.max), Mathf.Max(row.min, row.max));
            case RowType.Stepper:
                return row.options == null || row.options.Length == 0 ? 0f : Mathf.Clamp(row.defaultIndex, 0, row.options.Length - 1);
            default:
                return 0f;
        }
    }

    private static float Remembered(Row row)
    {
        return !string.IsNullOrEmpty(row.saveKey) && PlayerPrefs.HasKey(row.saveKey) ? PlayerPrefs.GetFloat(row.saveKey) : Default(row);
    }

    // Sets the row and fires its event; save = a change from the menu, remembered under the save key.
    private static void SetValue(Row row, float value, bool save)
    {
        row.value = value;
        row.valueKnown = true;
        if (save && !string.IsNullOrEmpty(row.saveKey))
            PlayerPrefs.SetFloat(row.saveKey, value);
        switch (row.type)
        {
            case RowType.Switch:
                row.onSwitched.Invoke(value > 0.5f);
                break;
            case RowType.Slider:
                row.onValueChanged.Invoke(value);
                break;
            case RowType.Stepper:
                row.onPicked.Invoke(Mathf.RoundToInt(value));
                break;
        }
    }

    private static string Shown(Row row)
    {
        if (row.percent)
            return Mathf.RoundToInt(Mathf.InverseLerp(row.min, row.max, row.value) * 100f) + "%";
        return row.value.ToString(Mathf.Abs(row.max - row.min) >= 20f ? "0" : "0.00");
    }

    // Every remembered setting back to what the project ships with: the built-in ones and every row with a save key.
    private void ResetToDefaults()
    {
        GameAudio.ResetMaster();
        UIScale.Reset();
        PlayerPrefs.DeleteKey(HudAutoHide.PrefsKey);
        HudAutoHide.Enabled = true;
        if (swimmer != null && defaultSensitivity > 0f)
            swimmer.MouseSensitivity = defaultSensitivity;
        PlayerPrefs.DeleteKey(SensitivityKey);
        if (projectQuality >= 0 && QualitySettings.GetQualityLevel() != projectQuality)
            QualitySettings.SetQualityLevel(projectQuality, true);
        PlayerPrefs.DeleteKey(QualityKey);
        if (projectQuality >= 0)
            QualitySettings.vSyncCount = projectVSync ? 1 : 0;
        PlayerPrefs.DeleteKey(VSyncKey);
        foreach (Row row in rows)
        {
            if (!IsOwn(row))
                continue;
            if (!string.IsNullOrEmpty(row.saveKey))
                PlayerPrefs.DeleteKey(row.saveKey);
            SetValue(row, Default(row), false);
        }
    }

    // ---- the default page -----------------------------------------------------------------------------------------

    // The page as it ships. Reset on the component, its context menu or the button under the list put it back.
    // Pages saved before the HUD size / auto hide existed get their rows, under the mouse sensitivity (or at the end).
    private void AddHudSizeRow()
    {
        if (!rows.Exists(r => r.type == RowType.HudSize))
        {
            int at = rows.FindIndex(r => r.type == RowType.MouseSensitivity);
            rows.Insert(at >= 0 ? at + 1 : rows.Count, new Row { type = RowType.HudSize, label = "HUD size" });
        }
        if (!rows.Exists(r => r.type == RowType.HudAutoHide))
        {
            int at = rows.FindIndex(r => r.type == RowType.HudSize);
            rows.Insert(at + 1, new Row { type = RowType.HudAutoHide, label = "Hide HUD when not needed" });
        }
    }

    [ContextMenu("Fill with the default rows")]
    public void FillDefaultRows()
    {
        rows.Clear();
        rows.Add(new Row { type = RowType.Heading, label = "Display" });
        rows.Add(new Row { type = RowType.Resolution, label = "Resolution" });
        rows.Add(new Row { type = RowType.Fullscreen, label = "Fullscreen" });
        rows.Add(new Row { type = RowType.VSync, label = "V-Sync" });
        rows.Add(new Row { type = RowType.Heading, label = "Audio" });
        rows.Add(new Row { type = RowType.MasterVolume, label = "Master volume" });
        rows.Add(new Row { type = RowType.Heading, label = "Controls" });
        rows.Add(new Row { type = RowType.MouseSensitivity, label = "Mouse sensitivity", min = 0.03f, max = 0.4f });
        rows.Add(new Row { type = RowType.HudSize, label = "HUD size" });
        rows.Add(new Row { type = RowType.HudAutoHide, label = "Hide HUD when not needed" });
        rows.Add(new Row { type = RowType.ResetToDefaults, label = "Everything above", buttonText = "Reset to defaults" });
        rows.Add(new Row { type = RowType.Note, label = "Resolution and fullscreen take effect straight away and are remembered by the game on its own." });
        rowsFilled = true;
    }

    private void Reset()
    {
        FillDefaultRows();
    }

#if UNITY_EDITOR
    // A Settings Page from before the list existed shows the default rows as soon as it is looked at.
    private void OnValidate()
    {
        if (NeedsDefaultRows)
            FillDefaultRows();
    }
#endif

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
