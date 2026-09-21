using System.Collections.Generic;
using UnityEngine;

// The Settings page of the pause menu: display (resolution, fullscreen, v-sync), audio (master volume) and controls
// (mouse sensitivity). Volume and sensitivity are remembered in PlayerPrefs and put back when the game starts;
// resolution and fullscreen the game remembers by itself.
public class SettingsPage : MonoBehaviour, IPauseMenuPage
{
    [SerializeField] private string pageTitle = "Settings";
    [Tooltip("For the mouse sensitivity slider. Found automatically.")]
    [SerializeField] private SwimController swimmer;
    [Tooltip("The range of the mouse sensitivity slider.")]
    [SerializeField] private float sensitivityMin = 0.03f;
    [SerializeField] private float sensitivityMax = 0.4f;

    private const string VolumeKey = "settings.masterVolume";
    private const string SensitivityKey = "settings.mouseSensitivity";

    private readonly List<Vector2Int> sizes = new List<Vector2Int>();
    private string[] sizeLabels = new string[0];
    private int sizeIndex;
    private float defaultSensitivity = -1f;

    public string PageTitle => pageTitle;
    public int Order => 10;

    private void Awake()
    {
        if (PlayerPrefs.HasKey(VolumeKey))
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey));
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
        MenuGUI.Heading("Display");
        int pick = MenuGUI.StepperRow("Resolution", sizeIndex, sizeLabels);
        if (pick != sizeIndex && pick >= 0 && pick < sizes.Count)
        {
            sizeIndex = pick;
            Screen.SetResolution(sizes[pick].x, sizes[pick].y, Screen.fullScreenMode);
        }

        bool fullscreen = Screen.fullScreenMode != FullScreenMode.Windowed;
        bool fullscreenNow = MenuGUI.SwitchRow("Fullscreen", fullscreen);
        if (fullscreenNow != fullscreen)
            Screen.fullScreenMode = fullscreenNow ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        bool vsync = QualitySettings.vSyncCount > 0;
        bool vsyncNow = MenuGUI.SwitchRow("V-Sync", vsync);
        if (vsyncNow != vsync)
            QualitySettings.vSyncCount = vsyncNow ? 1 : 0;

        MenuGUI.Heading("Audio");
        float volume = AudioListener.volume;
        float volumeNow = MenuGUI.SliderRow("Master volume", volume, 0f, 1f, Mathf.RoundToInt(volume * 100f) + "%");
        if (!Mathf.Approximately(volumeNow, volume))
        {
            AudioListener.volume = volumeNow;
            PlayerPrefs.SetFloat(VolumeKey, volumeNow);
        }

        MenuGUI.Heading("Controls");
        if (swimmer != null)
        {
            float sensitivity = swimmer.MouseSensitivity;
            float sensitivityNow = MenuGUI.SliderRow("Mouse sensitivity", sensitivity, sensitivityMin, sensitivityMax, sensitivity.ToString("0.00"));
            if (!Mathf.Approximately(sensitivityNow, sensitivity))
            {
                swimmer.MouseSensitivity = sensitivityNow;
                PlayerPrefs.SetFloat(SensitivityKey, sensitivityNow);
            }
        }
        if (MenuGUI.ButtonRow("Volume and sensitivity", "Reset to defaults"))
        {
            AudioListener.volume = 1f;
            PlayerPrefs.DeleteKey(VolumeKey);
            if (swimmer != null && defaultSensitivity > 0f)
                swimmer.MouseSensitivity = defaultSensitivity;
            PlayerPrefs.DeleteKey(SensitivityKey);
        }

        GUILayout.Space(12f);
        GUILayout.Label("Resolution and fullscreen take effect straight away and are remembered by the game on its own.", MenuGUI.NoteStyle ?? GUI.skin.label);
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
