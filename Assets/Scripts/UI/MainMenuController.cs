using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main menu buttons: New Game loads the level, Settings and Credits open the pause menu's own panel over the menu (in
// its main menu mode, the same look as in the game: Settings shows the Settings and Keybindings pages, Credits only
// the credits; Back or Escape to close; it is made here at start with the Theme), Quit exits. With Restyle Buttons on, the buttons and the title are
// dressed in the theme's colours, corners and font at start, so the two menus match: a soft rounded panel behind the
// buttons, an accent line under the title, and on every button a tick when the mouse comes onto it, a click when it
// is pressed, a small grow and an accent label on hover (Menu Button Feel).
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private string gameplaySceneName = "Main_Scene";
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Look")]
    [Tooltip("The pause menu's theme: the Settings / Credits panel uses it, and the buttons take its colours. Empty = the built-in defaults.")]
    [SerializeField] private PauseMenuTheme theme;
    [Tooltip("The game's controls, for the Keybindings page (there is no player here to take them from).")]
    [SerializeField] private InputActionAsset inputActions;
    [Tooltip("Dress the buttons and the title in the theme at start: rounded, its colours and font.")]
    [SerializeField] private bool restyleButtons = true;
    [SerializeField] private int buttonFontSize = 26;
    [SerializeField] private int titleFontSize = 76;

    private PauseMenu menu;

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        settingsButton.onClick.AddListener(OnSettings);
        creditsButton.onClick.AddListener(OnCredits);
        quitButton.onClick.AddListener(OnQuit);

        menu = PauseMenu.CreateFrontEnd(theme, inputActions);
        if (restyleButtons)
            Restyle();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // While the panel is up, the buttons underneath sleep: no hover ticks or clicks through it.
    private void Update()
    {
        bool free = !PauseMenu.IsOpen;
        foreach (Button button in new[] { newGameButton, settingsButton, creditsButton, quitButton })
            if (button != null && button.interactable != free)
                button.interactable = free;
    }

#if UNITY_EDITOR
    // The theme and the controls from their usual places, so the scene needs nothing dragged in.
    private void OnValidate()
    {
        if (theme == null)
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<PauseMenuTheme>("Assets/Settings/PauseMenuTheme.asset");
        if (inputActions == null)
            inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
    }
#endif

    private void OnNewGame()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OnSettings()
    {
        if (menu != null)
            menu.OpenPage<SettingsPage>(typeof(KeybindingsPage));   // Settings and Keybindings
    }

    private void OnCredits()
    {
        if (menu != null)
            menu.OpenPage<CreditsPage>();   // the credits and nothing else
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---- the look ---------------------------------------------------------------------------------------------------

    // Rounded buttons in the theme's colours (New Game a little brighter, it is the one to press), its font, and the
    // title in the same font and text colour with a soft shadow.
    private void Restyle()
    {
        PauseMenuTheme t = theme != null ? theme : PauseMenuTheme.Default;
        Font font = t.font != null ? t.font : GameFont.Font;
        Sprite rounded = Rounded(64, Mathf.Clamp(t.buttonCorner * 1.4f, 4f, 28f));

        foreach (Button button in new[] { newGameButton, settingsButton, creditsButton, quitButton })
        {
            if (button == null)
                continue;
            bool primary = button == newGameButton;
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            Color normal = primary ? Color.Lerp(t.buttonColor, t.accent, 0.35f) : t.buttonColor;
            normal.a = 0.9f;
            ColorBlock colours = button.colors;
            colours.normalColor = normal;
            colours.highlightedColor = primary ? Color.Lerp(t.buttonHover, t.accent, 0.45f) : t.buttonHover;
            colours.pressedColor = Color.Lerp(t.buttonHover, t.accent, 0.6f);
            colours.selectedColor = normal;   // a clicked button does not stay lit
            colours.disabledColor = normal;   // asleep under the Settings panel: looks the same
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.font = font;
                label.fontSize = buttonFontSize;
                label.color = t.textColor;
                label.alignment = TextAnchor.MiddleCenter;
            }
            MenuButtonFeel feel = button.GetComponent<MenuButtonFeel>() != null ? button.GetComponent<MenuButtonFeel>() : button.gameObject.AddComponent<MenuButtonFeel>();
            feel.Setup(t);
        }
        PanelBehindButtons(t);

        GameObject titleObject = GameObject.Find("Title");
        Text title = titleObject != null ? titleObject.GetComponent<Text>() : null;
        if (title != null)
        {
            title.font = font;
            title.fontSize = titleFontSize;
            title.color = t.textColor;
            Shadow shadow = title.GetComponent<Shadow>() != null ? title.GetComponent<Shadow>() : title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -3f);
            LineUnder(title.rectTransform, t.accent);
        }
    }

    // A soft rounded panel in the pause menu's panel colour behind the column of buttons, a little bigger than it.
    private void PanelBehindButtons(PauseMenuTheme t)
    {
        var buttons = new System.Collections.Generic.List<RectTransform>();
        foreach (Button button in new[] { newGameButton, settingsButton, creditsButton, quitButton })
            if (button != null)
                buttons.Add((RectTransform)button.transform);
        if (buttons.Count == 0)
            return;
        var parent = buttons[0].parent as RectTransform;
        if (parent == null)
            return;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        int firstIndex = int.MaxValue;
        var corners = new Vector3[4];
        foreach (RectTransform rect in buttons)
        {
            if (rect.parent != parent)
                continue;
            rect.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 local = parent.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            firstIndex = Mathf.Min(firstIndex, rect.GetSiblingIndex());
        }
        if (firstIndex == int.MaxValue)
            return;
        const float padding = 30f;
        var panel = new GameObject("ButtonPanel", typeof(RectTransform), typeof(Image));
        var panelRect = (RectTransform)panel.transform;
        panelRect.SetParent(parent, false);
        panelRect.SetSiblingIndex(firstIndex);   // behind the buttons
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = max - min + Vector2.one * padding * 2f;
        panelRect.anchoredPosition = (min + max) * 0.5f - parent.rect.center;
        var image = panel.GetComponent<Image>();
        image.sprite = Rounded(96, Mathf.Clamp(t.panelCorner, 6f, 40f));
        image.type = Image.Type.Sliced;
        image.color = new Color(t.panelColor.r, t.panelColor.g, t.panelColor.b, 0.72f);
        image.raycastTarget = false;
    }

    // A short accent line centred under the title.
    private static void LineUnder(RectTransform title, Color accent)
    {
        var parent = title.parent as RectTransform;
        if (parent == null)
            return;
        var corners = new Vector3[4];
        title.GetWorldCorners(corners);
        Vector2 bottomLeft = parent.InverseTransformPoint(corners[0]), bottomRight = parent.InverseTransformPoint(corners[3]);
        var line = new GameObject("TitleLine", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)line.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(220f, 3f);
        rect.anchoredPosition = (bottomLeft + bottomRight) * 0.5f + Vector2.up * 4f - parent.rect.center;
        var image = line.GetComponent<Image>();
        image.color = new Color(accent.r, accent.g, accent.b, 0.85f);
        image.raycastTarget = false;
    }

    // A white rounded square, 9-sliced by its corners, tinted by the button colours.
    private static Sprite Rounded(int size, float radius)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        float border = Mathf.Ceil(radius) + 1f;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
