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
// With Match Background on, the menu takes its colours from the background art (Noora's porthole animation: deep navy
// water, the steel grey of the porthole's frame, the pale blue of the figure) instead of the theme's own: dark navy
// buttons with a steel rim like the porthole, pale blue accents and title. Only this menu changes (it works on a copy
// of the theme), the Settings and Credits panel opened from it too; the in-game pause menu keeps the theme as it is.
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

    [Header("Colours (to fit the background)")]
    [SerializeField] private bool matchBackground = true;
    [SerializeField] private Color buttonColour = new Color(0.05f, 0.11f, 0.18f, 0.88f);
    [SerializeField] private Color buttonHoverColour = new Color(0.11f, 0.22f, 0.31f, 0.95f);
    [Tooltip("Rims, the title line, hovered labels: the pale blue of the figure in the porthole.")]
    [SerializeField] private Color accentColour = new Color(0.47f, 0.74f, 0.9f);
    [Tooltip("The rim round every button: the steel grey of the porthole's frame.")]
    [SerializeField] private Color rimColour = new Color(0.2f, 0.29f, 0.34f);
    [SerializeField] private Color labelColour = new Color(0.84f, 0.93f, 1f);
    [SerializeField] private Color titleColour = new Color(0.64f, 0.84f, 0.97f);
    [Tooltip("The soft panel behind the buttons and the Settings / Credits panel.")]
    [SerializeField] private Color panelColour = new Color(0.02f, 0.06f, 0.11f, 0.94f);

    private PauseMenu menu;

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        settingsButton.onClick.AddListener(OnSettings);
        creditsButton.onClick.AddListener(OnCredits);
        quitButton.onClick.AddListener(OnQuit);

        PauseMenuTheme look = MenuTheme();
        menu = PauseMenu.CreateFrontEnd(look, inputActions);
        if (restyleButtons)
            Restyle(look);
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

    // The theme, or with Match Background on a copy of it in the background's colours (the asset is left alone).
    private PauseMenuTheme MenuTheme()
    {
        PauseMenuTheme t = theme != null ? theme : PauseMenuTheme.Default;
        if (!matchBackground || t == null)
            return t;
        PauseMenuTheme copy = Instantiate(t);
        copy.hideFlags = HideFlags.DontSave;
        copy.buttonColor = buttonColour;
        copy.buttonHover = buttonHoverColour;
        copy.accent = accentColour;
        copy.textColor = labelColour;
        copy.panelColor = panelColour;
        copy.fieldColor = new Color(panelColour.r * 0.6f, panelColour.g * 0.6f, panelColour.b * 0.6f, 0.95f);
        copy.mutedColor = Color.Lerp(labelColour, buttonColour, 0.4f);
        return copy;
    }

    // Rounded buttons in the theme's colours (New Game a little brighter, it is the one to press), its font, and the
    // title in the same font and text colour with a soft shadow.
    private void Restyle(PauseMenuTheme t)
    {
        Font font = t.font != null ? t.font : GameFont.Font;
        float corner = Mathf.Clamp(t.buttonCorner * 1.4f, 4f, 28f);
        Sprite rounded = Rounded(64, corner);
        Sprite rim = matchBackground ? Rounded(64, corner, 4f) : null;

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
            Color normal = primary ? Color.Lerp(t.buttonColor, t.accent, matchBackground ? 0.18f : 0.35f) : t.buttonColor;
            normal.a = matchBackground ? t.buttonColor.a : 0.9f;
            ColorBlock colours = button.colors;
            colours.normalColor = normal;
            colours.highlightedColor = primary ? Color.Lerp(t.buttonHover, t.accent, 0.45f) : t.buttonHover;
            colours.pressedColor = Color.Lerp(t.buttonHover, t.accent, 0.6f);
            colours.selectedColor = normal;   // a clicked button does not stay lit
            colours.disabledColor = normal;   // asleep under the Settings panel: looks the same
            colours.colorMultiplier = 1f;
            colours.fadeDuration = 0.08f;
            button.colors = colours;

            // The porthole's steel rim round it (the New Game one in the pale blue).
            if (rim != null && button.transform.Find("Rim") == null)
            {
                var edge = new GameObject("Rim", typeof(RectTransform), typeof(Image));
                var edgeRect = (RectTransform)edge.transform;
                edgeRect.SetParent(button.transform, false);
                edgeRect.SetSiblingIndex(0);
                edgeRect.anchorMin = Vector2.zero;
                edgeRect.anchorMax = Vector2.one;
                edgeRect.offsetMin = edgeRect.offsetMax = Vector2.zero;
                var edgeImage = edge.GetComponent<Image>();
                edgeImage.sprite = rim;
                edgeImage.type = Image.Type.Sliced;
                edgeImage.color = primary ? Color.Lerp(rimColour, accentColour, 0.55f) : rimColour;
                edgeImage.raycastTarget = false;
            }

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
            title.color = matchBackground ? titleColour : t.textColor;
            Shadow shadow = title.GetComponent<Shadow>() != null ? title.GetComponent<Shadow>() : title.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -3f);
            LineUnder(title, t.accent);
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
        image.color = new Color(t.panelColor.r, t.panelColor.g, t.panelColor.b, matchBackground ? 0.55f : 0.72f);
        image.raycastTarget = false;
    }

    // A short accent line under the title: centred, or from its left end when the title is set to the left.
    private static void LineUnder(Text text, Color accent)
    {
        RectTransform title = text.rectTransform;
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
        bool left = text.alignment == TextAnchor.UpperLeft || text.alignment == TextAnchor.MiddleLeft || text.alignment == TextAnchor.LowerLeft;
        Vector2 under = left ? bottomLeft + Vector2.right * (rect.sizeDelta.x * 0.5f + 6f) : (bottomLeft + bottomRight) * 0.5f;
        rect.anchoredPosition = under + Vector2.up * 4f - parent.rect.center;
        var image = line.GetComponent<Image>();
        image.color = new Color(accent.r, accent.g, accent.b, 0.85f);
        image.raycastTarget = false;
    }

    // A white rounded square, 9-sliced by its corners, tinted by the button colours; with `rim` above 0 only its edge,
    // that many pixels thick (the rim round a button).
    private static Sprite Rounded(int size, float radius, float rim = 0f)
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
                float a = Mathf.Clamp01(0.5f - d);
                if (rim > 0f)
                    a *= Mathf.Clamp01(d + rim + 0.5f);   // hollow inside the rim
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        float border = Mathf.Ceil(radius) + 1f;
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
