using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The pause menu (Esc): freezes the game, frees the mouse and shows a panel with the pages down the left (Map,
// Settings, Keybindings, Admin: any script in the scene that implements IPauseMenuPage, sorted by its Order) and the
// open page on the right, with Resume, Main menu and Quit under the pages. IMGUI, so it needs no canvas or
// EventSystem: it builds its own skin (rounded 9-sliced textures, switches, key caps) and hands the extra styles to
// MenuGUI, which the pages draw with, so everything matches and the quiet hover tick knows what the mouse is over.
// The built-in pages are added to this object at runtime if the scene has none. Colours, sizes, fonts, labels, page
// names, timings and sounds come from a PauseMenuTheme asset (Theme; the defaults when none). Lives on the Player.
// Preview In Editor (or Tools > Out of the Depths > Preview Pause Menu) shows it open in the Game view without
// playing, the pages look-only, so the theme and the pages can be worked on and watched.
[ExecuteAlways]
public class PauseMenu : MonoBehaviour
{
    [Header("Keys")]
    [SerializeField] private Key toggleKey = Key.Escape;
    [Tooltip("Also opens the menu, straight onto the Admin page (the old admin key).")]
    [SerializeField] private Key adminKey = Key.Digit0;

    [Header("Look")]
    [Tooltip("Every colour, size, font, label, timing and sound of the menu, in one asset shared by all scenes (Tools > Out of the Depths > Create Pause Menu Theme). Empty = the built-in defaults.")]
    [SerializeField] private PauseMenuTheme theme;

    private PauseMenuTheme T => theme != null ? theme : PauseMenuTheme.Default;

    [Header("Behaviour")]
    [Tooltip("Player scripts switched off while the menu is up (movement, interaction, attack).")]
    [SerializeField] private Behaviour[] pauseWhileOpen;
    [Tooltip("Show the Admin page in release builds too. Off = editor and development builds only.")]
    [SerializeField] private bool adminInReleaseBuilds = false;
    [Tooltip("The scene the Main menu button loads (it has to be in Build Settings). Empty = no button.")]
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Editor preview")]
    [Tooltip("Show the menu open in the Game view without playing, to work on the theme and the pages (Tools > Out of the Depths > Preview Pause Menu toggles it too). Clicking switches pages; the pages themselves are look-only, so nothing changes by accident.")]
    [SerializeField] private bool previewInEditor = false;

    public bool PreviewInEditor { get => previewInEditor; set => previewInEditor = value; }

    // Every enabled menu, so the editor knows whether any is previewing and keeps the Game view refreshing.
    private static readonly List<PauseMenu> live = new List<PauseMenu>();
    public static bool AnyPreviewing
    {
        get
        {
            foreach (PauseMenu menu in live)
                if (menu != null && menu.previewInEditor)
                    return true;
            return false;
        }
    }

    private bool Previewing => !Application.isPlaying && previewInEditor;
    private float lastPreviewTime;
    private float previewPagesAt = -10f;
    private IPauseMenuPage failedPage;
    private string failedPageError;

    public static bool IsOpen { get; private set; }

    // A page that is listening for a key (rebinding) captures the keyboard: the menu keeps its hands off the toggle
    // keys until a moment after it lets go.
    private static bool inputCaptured;
    private static float inputGraceUntil;

    private readonly List<IPauseMenuPage> pages = new List<IPauseMenuPage>();
    private int pageIndex;
    private float timeScaleBefore = 1f;

    // Eased motion: the wheel moves a scroll target the view glides to; the sidebar marker slides between entries.
    private float scrollNow;
    private float scrollTarget;
    private float contentHeight;
    private float viewHeight;
    private float navBarY;
    private float navBarTargetY;
    private float navBarX;
    private float navBarHeight;
    private bool navBarKnown;
    private CursorLockMode cursorLockBefore;
    private bool cursorVisibleBefore;
    private float visibility;      // 0..1, eased: the menu draws while it is above 0
    private float pageVisibility;  // 0..1, the current page fading in
    private float openedAt;
    private bool confirmQuit;

    // Hover: MenuGUI controls report the one under the mouse during each repaint; a change plays the tick.
    private static bool hoverFound;
    private static Rect hoverRect;
    private int lastHoverKey;
    private float lastHoverSoundAt;
    private AudioSource audioSource;

    private GUISkin skin;
    private float builtSpacing = -1f;
    private PauseMenuTheme builtTheme;
    private int builtVersion = -1;
    private Font builtFont;
    private GUIStyle titleStyle;
    private GUIStyle pageTitleStyle;
    private GUIStyle noteStyle;
    private GUIStyle navStyle;
    private GUIStyle resumeStyle;
    private GUIStyle quietStyle;
    private GUIStyle dangerStyle;
    private GUIStyle panelStyle;
    private GUIStyle shadowStyle;
    private Texture2D backdropTex;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;   // the edit-mode preview adds nothing to the scene
        // The built-in pages, unless the scene already has them (then their Inspector settings are used).
        if (FindFirstObjectByType<MapPage>() == null)
            gameObject.AddComponent<MapPage>();
        if (FindFirstObjectByType<SettingsPage>() == null)
            gameObject.AddComponent<SettingsPage>();
        if (FindFirstObjectByType<KeybindingsPage>() == null)
            gameObject.AddComponent<KeybindingsPage>();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;   // the preview runs its eases from OnGUI
        float dt = Time.unscaledDeltaTime;
        float step = T.fadeSeconds > 0f ? dt / T.fadeSeconds : 1f;
        visibility = Mathf.MoveTowards(visibility, IsOpen ? 1f : 0f, step);
        float pageStep = T.pageFadeSeconds > 0f ? dt / T.pageFadeSeconds : 1f;
        pageVisibility = Mathf.MoveTowards(pageVisibility, 1f, pageStep);
        if (visibility > 0f)
            Ease(dt);

        if (inputCaptured || Time.unscaledTime < inputGraceUntil)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard[toggleKey].wasPressedThisFrame)
            SetOpen(!IsOpen);
        else if (keyboard[adminKey].wasPressedThisFrame)
        {
            if (IsOpen)
                SetOpen(false);
            else
            {
                SetOpen(true);
                SelectPage<AdminPanel>();
            }
        }
    }

    private void OnEnable()
    {
        live.Add(this);
        visibility = 0f;
    }

    private void OnDisable()
    {
        live.Remove(this);
        if (IsOpen)
            SetOpen(false);
        visibility = 0f;
    }

    public void Resume() => SetOpen(false);

    // Called by MenuGUI for the control under the mouse.
    public static void NoteHover(Rect rect)
    {
        hoverFound = true;
        hoverRect = rect;
    }

    // Called by a page while it waits for a key press (rebinding), and again when it is done.
    public static void CaptureInput(bool captured)
    {
        inputCaptured = captured;
        if (!captured)
            inputGraceUntil = Time.unscaledTime + 0.25f;
    }

    private void SetOpen(bool open)
    {
        if (IsOpen == open)
            return;
        IsOpen = open;
        PlayClip(open ? T.openSound : T.closeSound, T.menuVolume);

        foreach (Behaviour b in pauseWhileOpen)
            if (b != null)
                b.enabled = !open;

        if (open)
        {
            timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            cursorLockBefore = Cursor.lockState;
            cursorVisibleBefore = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CollectPages();
            pageVisibility = 0f;
            openedAt = Time.unscaledTime;
            lastHoverKey = 0;
            confirmQuit = false;
            scrollNow = scrollTarget = 0f;
            navBarKnown = false;
            MenuGUI.Reset();
            if (pages.Count > 0)
                pages[pageIndex].OnPageShown();
        }
        else
        {
            Time.timeScale = ResumeTimeScale();
            AudioListener.pause = false;
            Cursor.lockState = cursorLockBefore;
            Cursor.visible = cursorVisibleBefore;
        }
    }

    // Every IPauseMenuPage in the scene, in Order (the Admin page only where it is allowed).
    private void CollectPages()
    {
        IPauseMenuPage current = pages.Count > 0 && pageIndex < pages.Count ? pages[pageIndex] : null;
        pages.Clear();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool adminAllowed = true;
#else
        bool adminAllowed = adminInReleaseBuilds;
#endif
        foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(behaviour is IPauseMenuPage page))
                continue;
            if (behaviour is AdminPanel && !adminAllowed)
                continue;
            if (T.IsHidden(page.PageTitle))
                continue;
            pages.Add(page);
        }
        pages.Sort((a, b) =>
        {
            int oa = T.OrderFor(a.PageTitle, a.Order), ob = T.OrderFor(b.PageTitle, b.Order);
            return oa != ob ? oa.CompareTo(ob) : string.Compare(a.PageTitle, b.PageTitle, System.StringComparison.Ordinal);
        });
        pageIndex = Mathf.Max(0, pages.IndexOf(current));
    }

    private void ShowPage(int index)
    {
        if (index == pageIndex || index < 0 || index >= pages.Count)
            return;
        pageIndex = index;
        pageVisibility = 0f;
        scrollNow = scrollTarget = 0f;
        confirmQuit = false;
        pages[index].OnPageShown();
    }

    private void SelectPage<T>() where T : class
    {
        for (int i = 0; i < pages.Count; i++)
            if (pages[i] is T)
            {
                ShowPage(i);
                return;
            }
    }

    // The admin page can ask for a different speed (its Time scale slider); otherwise back to what it was.
    private float ResumeTimeScale()
    {
        foreach (IPauseMenuPage page in pages)
            if (page is AdminPanel admin)
                return admin.RequestedTimeScale;
        return timeScaleBefore;
    }

    private bool HasMainMenu => !string.IsNullOrEmpty(mainMenuScene) && Application.CanStreamedLevelBeLoaded(mainMenuScene);

    private void GoToMainMenu()
    {
        SetOpen(false);   // time, sound and the cursor back to normal before the scene goes
        SceneManager.LoadScene(mainMenuScene);
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Hover highlights, switch knobs, the sidebar marker and the scroll all glide toward where they are going.
    private void Ease(float dt)
    {
        MenuGUI.Smoothing = T.smoothing;
        MenuGUI.Tick(dt);
        float k = 1f - Mathf.Exp(-T.smoothing * dt);
        scrollNow = Mathf.Lerp(scrollNow, scrollTarget, k);
        if (navBarKnown)
            navBarY = Mathf.Lerp(navBarY, navBarTargetY, k);
    }

    // The edit-mode preview: the menu fully open, its eases run from here (nothing else ticks outside Play mode),
    // the pages gathered once a second so a renamed, hidden or added page shows up.
    private void PreviewFrame()
    {
        visibility = 1f;
        float now = Time.realtimeSinceStartup;
        if (Event.current.type == EventType.Layout && now - previewPagesAt > 1f)
        {
            previewPagesAt = now;
            int before = pages.Count;
            CollectPages();
            if (pages.Count > 0 && (before == 0 || pageIndex >= pages.Count))
            {
                pageIndex = Mathf.Clamp(pageIndex, 0, pages.Count - 1);
                pages[pageIndex].OnPageShown();
            }
        }
        if (Event.current.type == EventType.Repaint)
        {
            float dt = Mathf.Clamp(now - lastPreviewTime, 0f, 0.1f);
            lastPreviewTime = now;
            float pageStep = T.pageFadeSeconds > 0f ? dt / T.pageFadeSeconds : 1f;
            pageVisibility = Mathf.MoveTowards(pageVisibility, 1f, pageStep);
            Ease(dt);
        }
    }

    // In the preview a page is look-only: clicks never reach it, and a page that cannot draw outside Play mode says
    // so instead of erroring every frame.
    private void DrawPageSafely(IPauseMenuPage page)
    {
        if (!Previewing)
        {
            page.DrawPage();
            return;
        }
        if (page == failedPage)
        {
            GUILayout.Label("This page only draws while playing: " + failedPageError, MenuGUI.NoteStyle);
            return;
        }
        Event e = Event.current;
        if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.MouseDrag)
            e.Use();
        try
        {
            page.DrawPage();
        }
        catch (ExitGUIException)
        {
            throw;
        }
        catch (System.Exception error)
        {
            failedPage = page;
            failedPageError = error.Message;
            GUIUtility.ExitGUI();
        }
    }

    private void OnGUI()
    {
        if (Previewing)
            PreviewFrame();
        else if (!Application.isPlaying)
            return;
        if (visibility <= 0f)
            return;

        float s = Mathf.Max(1f, Screen.height / 900f) * Mathf.Max(0.5f, T.scale);
        float width = Screen.width / s;
        float height = Screen.height / s;
        float sp = T.spacing;
        EnsureStyles();
        GUISkin previousSkin = GUI.skin;
        GUI.skin = skin;

        // Ease: the backdrop fades; the panel fades, rises and grows into place, a touch faster on the way out.
        float ease = 1f - Mathf.Pow(1f - visibility, 3f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        GUI.color = new Color(1f, 1f, 1f, ease);
        GUI.DrawTexture(new Rect(0f, 0f, width, height), backdropTex, ScaleMode.StretchToFill);

        float panelH = Mathf.Min(height - 72f, T.panelHeight);
        float panelW = Mathf.Min(T.panelWidth, width - 48f);
        float rise = (1f - ease) * T.riseDistance;
        var panel = new Rect((width - panelW) * 0.5f, (height - panelH) * 0.5f + rise, panelW, panelH);
        float grow = Mathf.Lerp(T.startScale, 1f, ease);
        Vector3 centre = new Vector3(panel.center.x, panel.center.y, 0f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f)) * Matrix4x4.Translate(centre) * Matrix4x4.Scale(new Vector3(grow, grow, 1f)) * Matrix4x4.Translate(-centre);
        GUI.Box(new Rect(panel.x - 24f, panel.y - 14f, panel.width + 48f, panel.height + 48f), GUIContent.none, shadowStyle);
        GUI.Box(panel, GUIContent.none, panelStyle);

        // The sidebar on the left, the page on the right, a hairline between.
        float side = Mathf.Min(T.sidebarWidth, panel.width * 0.4f);
        bool right = T.sidebarOnRight;
        var sidebar = right ? new Rect(panel.x + panel.width - side, panel.y, side, panel.height) : new Rect(panel.x, panel.y, side, panel.height);
        var content = right ? new Rect(panel.x, panel.y, panel.width - side, panel.height) : new Rect(panel.x + side, panel.y, panel.width - side, panel.height);
        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.08f * ease);
        GUI.DrawTexture(new Rect(right ? sidebar.x : content.x, panel.y + 30f, 1f, panel.height - 60f), Texture2D.whiteTexture);
        GUI.color = previous;

        DrawSidebar(sidebar, sp, ease);
        DrawContent(content, sp, ease);

        // Hover sound: something new under the mouse this repaint.
        if (Event.current.type == EventType.Repaint)
        {
            int key = hoverFound ? HoverKey(hoverRect) : 0;
            bool settled = Time.unscaledTime > openedAt + 0.15f && Time.unscaledTime - lastHoverSoundAt > 0.04f;
            if (key != 0 && key != lastHoverKey && IsOpen && settled)
                PlayHover();
            lastHoverKey = key;
            hoverFound = false;
        }

        GUI.color = Color.white;
        GUI.skin = previousSkin;
        GUI.matrix = Matrix4x4.identity;
    }

    // The title, one entry per page (the open one marked with an accent bar), then Resume, Main menu and Quit.
    private void DrawSidebar(Rect rect, float sp, float ease)
    {
        GUILayout.BeginArea(new Rect(rect.x + 28f * sp, rect.y + 34f * sp, rect.width - 44f * sp, rect.height - 68f * sp));
        GUILayout.Label(T.title, titleStyle);
        GUILayout.Space(22f * sp);

        for (int i = 0; i < pages.Count; i++)
        {
            bool on = MenuGUI.NavEntry(pageIndex == i, T.LabelFor(pages[i].PageTitle), navStyle);
            if (pageIndex == i && Event.current.type == EventType.Repaint)
            {
                // The accent marker slides to the open entry.
                Rect entry = GUILayoutUtility.GetLastRect();
                navBarTargetY = entry.y;
                navBarX = entry.x;
                navBarHeight = entry.height;
                if (!navBarKnown)
                {
                    navBarY = entry.y;
                    navBarKnown = true;
                }
            }
            if (on && pageIndex != i)
                ShowPage(i);
        }
        if (navBarKnown && Event.current.type == EventType.Repaint)
        {
            Color previous = GUI.color;
            GUI.color = new Color(T.accent.r, T.accent.g, T.accent.b, previous.a);
            GUI.DrawTexture(new Rect(navBarX, navBarY + 12f, 3f, navBarHeight - 24f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        GUILayout.FlexibleSpace();
        if (confirmQuit)
        {
            GUILayout.Label(T.quitConfirm, noteStyle);
            GUILayout.Space(6f * sp);
            if (MenuGUI.Button(T.quitLabel, dangerStyle))
            {
                if (Previewing)
                    confirmQuit = false;
                else
                    Quit();
            }
            if (MenuGUI.Button(T.cancelLabel, quietStyle))
                confirmQuit = false;
        }
        else
        {
            if (MenuGUI.Button(T.resumeLabel, resumeStyle) && IsOpen)
                SetOpen(false);
            GUILayout.Space(4f * sp);
            if (HasMainMenu && MenuGUI.Button(T.mainMenuLabel, quietStyle) && !Previewing)
                GoToMainMenu();
            if (MenuGUI.Button(T.quitLabel, quietStyle))
                confirmQuit = true;
        }
        GUILayout.EndArea();
    }

    // The open page: its title, a rule, then the page itself in a scroll view (fading in on a page change).
    private void DrawContent(Rect rect, float sp, float ease)
    {
        var area = new Rect(rect.x + 40f * sp, rect.y + 34f * sp, rect.width - 80f * sp, rect.height - 68f * sp);
        GUILayout.BeginArea(area);

        // Smooth scrolling: the wheel moves a target and the view glides to it (dragging the slim bar still works).
        if (Event.current.type == EventType.ScrollWheel && new Rect(0f, 0f, area.width, area.height).Contains(Event.current.mousePosition))
        {
            scrollTarget = Mathf.Clamp(scrollTarget + Event.current.delta.y * 24f, 0f, Mathf.Max(0f, contentHeight - viewHeight));
            Event.current.Use();
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(pages.Count > 0 ? T.LabelFor(pages[pageIndex].PageTitle) : T.title, pageTitleStyle);
        GUILayout.FlexibleSpace();
        GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();
        GUILayout.Label(T.resumeHint.Replace("{key}", toggleKey.ToString()), noteStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.Space(8f * sp);
        Rule(0.2f * ease);
        GUILayout.Space(18f * sp);

        // The page fades and rises in on a page change.
        float pageEase = 1f - Mathf.Pow(1f - pageVisibility, 3f);
        GUI.color = new Color(1f, 1f, 1f, ease * pageEase);
        Vector2 result = GUILayout.BeginScrollView(new Vector2(0f, scrollNow));
        GUILayout.BeginVertical();
        GUILayout.Space((1f - pageEase) * 12f);
        if (pages.Count > 0)
            DrawPageSafely(pages[pageIndex]);
        else
            GUILayout.Label(T.emptyPageText, MenuGUI.NoteStyle);
        GUILayout.Space(12f * sp);
        GUILayout.EndVertical();
        if (Event.current.type == EventType.Repaint)
            contentHeight = GUILayoutUtility.GetLastRect().height;
        GUILayout.EndScrollView();
        if (Event.current.type == EventType.Repaint)
            viewHeight = GUILayoutUtility.GetLastRect().height;
        if (Mathf.Abs(result.y - scrollNow) > 0.5f)
            scrollNow = scrollTarget = result.y;   // the bar was dragged (or the view clamped): follow it
        GUI.color = new Color(1f, 1f, 1f, ease);
        GUILayout.EndArea();
    }

    private static int HoverKey(Rect r)
    {
        int key = Mathf.RoundToInt(r.x) * 73856093 ^ Mathf.RoundToInt(r.y) * 19349663 ^ Mathf.RoundToInt(r.width) * 83492791 ^ Mathf.RoundToInt(r.height) * 49979687;
        return key == 0 ? 1 : key;
    }

    private void PlayHover()
    {
        lastHoverSoundAt = Time.unscaledTime;
        PlayClip(T.hoverSound, T.hoverVolume, true);
    }

    private void PlayClip(AudioClip clip, float volume, bool vary = false)
    {
        if (clip == null || volume <= 0f)
            return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;   // the menu pauses the listener; this still has to play
            audioSource.bypassEffects = true;
            audioSource.bypassListenerEffects = true;
            audioSource.bypassReverbZones = true;
        }
        audioSource.pitch = vary ? Random.Range(0.96f, 1.04f) : 1f;
        audioSource.PlayOneShot(clip, volume);
    }

    // A one-pixel accent line across the layout.
    private void Rule(float alpha)
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandWidth(true));
        Color previous = GUI.color;
        GUI.color = new Color(T.accent.r, T.accent.g, T.accent.b, alpha);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // A skin of our own: rounded, 9-sliced textures for everything, switches and key caps for the pages. Flat
    // surfaces, soft corners, generous room; the accent only on what is active.
    private void EnsureStyles()
    {
        Font wanted = T.font != null ? T.font : GameFont.Custom;   // the theme's font, else the game font
        if (skin != null && builtTheme == theme && builtFont == wanted && builtVersion == PauseMenuTheme.Version && Mathf.Approximately(builtSpacing, T.spacing))
            return;
        builtFont = wanted;
        builtSpacing = T.spacing;
        builtTheme = theme;
        builtVersion = PauseMenuTheme.Version;
        float sp = T.spacing;

        backdropTex = Vignette(T.backdropCentre, T.backdropEdge);
        Color accentSoft = new Color(T.accent.r, T.accent.g, T.accent.b, 0.45f);
        Color accentBright = Color.Lerp(T.accent, Color.white, 0.35f);
        Color accentTintHot = new Color(T.accent.r, T.accent.g, T.accent.b, 0.24f);
        Color faint = new Color(1f, 1f, 1f, 0.06f);
        Color faintHot = new Color(1f, 1f, 1f, 0.12f);

        // Hover is not baked into the styles: MenuGUI eases a highlight over whatever the mouse is on.
        Texture2D button = Rounded(36, T.buttonCorner, T.buttonColor, Color.clear, 0f);
        Texture2D buttonDown = Rounded(36, T.buttonCorner, T.accent, Color.clear, 0f);
        Texture2D pillOff = Rounded(36, T.buttonCorner, T.fieldColor, new Color(1f, 1f, 1f, 0.12f), 1f);
        Texture2D pillOn = Rounded(36, T.buttonCorner, T.accent, Color.clear, 0f);
        Texture2D quiet = Rounded(36, T.buttonCorner, faint, Color.clear, 0f);
        Texture2D danger = Rounded(36, T.buttonCorner, T.dangerColor, Color.clear, 0f);
        Texture2D dangerHot = Rounded(36, T.buttonCorner, Color.Lerp(T.dangerColor, Color.white, 0.2f), Color.clear, 0f);
        Texture2D frame = Rounded(72, 16f, T.fieldColor, new Color(1f, 1f, 1f, 0.08f), 1f);
        Texture2D key = Rounded(28, 8f, T.fieldColor, new Color(1f, 1f, 1f, 0.18f), 1f);
        Texture2D keyListen = Rounded(28, 8f, T.accent, Color.clear, 0f);
        Texture2D field = Rounded(24, 7f, T.fieldColor, new Color(1f, 1f, 1f, 0.12f), 1f);
        Texture2D track = Track(20, 8f, T.fieldColor, new Color(1f, 1f, 1f, 0.12f));
        Texture2D thumb = Rounded(20, 10f, T.accent, Color.clear, 0f);
        Texture2D thumbHot = Rounded(20, 10f, accentBright, Color.clear, 0f);
        Texture2D highlight = Rounded(36, T.buttonCorner, faintHot, accentSoft, 1.5f);
        Texture2D onTint = Rounded(36, T.buttonCorner, accentTintHot, Color.clear, 0f);
        Texture2D switchOff = SwitchTrack(52, 28, T.fieldColor, new Color(1f, 1f, 1f, 0.16f));
        Texture2D switchOn = SwitchTrack(52, 28, T.accent, Color.clear);
        Texture2D switchGlow = SwitchTrack(52, 28, Color.white, Color.clear);
        Texture2D knob = Circle(20);
        Texture2D panelTex = Rounded(104, T.panelCorner, T.panelColor, new Color(1f, 1f, 1f, 0.08f), 1.5f);
        Texture2D shadowTex = Shadow(160, 28f, 24f, 0.6f);

        skin = Instantiate(GUI.skin);
        skin.name = "PauseMenu (runtime)";
        skin.hideFlags = HideFlags.DontSave;
        if (wanted != null)
            skin.font = wanted;

        Style(skin.label, T.bodyFontSize, FontStyle.Normal, T.textColor);
        skin.label.wordWrap = true;
        skin.label.padding = new RectOffset(2, 2, Px(5f * sp), Px(5f * sp));

        Style(skin.button, T.buttonFontSize, FontStyle.Normal, T.textColor);
        SetBackgrounds(skin.button, button, button, buttonDown, button);
        skin.button.active.textColor = Color.black;
        skin.button.border = new RectOffset(14, 14, 14, 14);
        skin.button.padding = new RectOffset(Px(18f * sp), Px(18f * sp), 0, 0);
        skin.button.margin = new RectOffset(Px(5f * sp), Px(5f * sp), Px(5f * sp), Px(5f * sp));
        skin.button.fixedHeight = 40f;
        skin.button.alignment = TextAnchor.MiddleCenter;

        Style(skin.toggle, T.bodyFontSize, FontStyle.Normal, T.textColor);
        SetBackgrounds(skin.toggle, pillOff, pillOff, pillOff, pillOff);
        skin.toggle.onNormal.background = pillOn;
        skin.toggle.onHover.background = pillOn;
        skin.toggle.onActive.background = pillOn;
        skin.toggle.onFocused.background = pillOn;
        skin.toggle.onNormal.textColor = Color.black;
        skin.toggle.onHover.textColor = Color.black;
        skin.toggle.onActive.textColor = Color.black;
        skin.toggle.onFocused.textColor = Color.black;
        skin.toggle.border = new RectOffset(14, 14, 14, 14);
        skin.toggle.padding = new RectOffset(Px(18f * sp), Px(18f * sp), 0, 0);
        skin.toggle.margin = new RectOffset(Px(5f * sp), Px(5f * sp), Px(5f * sp), Px(5f * sp));
        skin.toggle.fixedHeight = 38f;
        skin.toggle.alignment = TextAnchor.MiddleLeft;

        // The box is a frame now (the map sits in one).
        skin.box.normal.background = frame;
        skin.box.normal.textColor = T.textColor;
        skin.box.border = new RectOffset(22, 22, 22, 22);
        skin.box.padding = new RectOffset(8, 8, 8, 8);
        skin.box.margin = new RectOffset(0, 0, 0, 0);

        // Sliders sit centred in a 44-pixel row.
        skin.horizontalSlider.normal.background = track;
        skin.horizontalSlider.border = new RectOffset(6, 6, 6, 6);
        skin.horizontalSlider.fixedHeight = 20f;
        skin.horizontalSlider.margin = new RectOffset(6, 6, 12, 12);
        skin.horizontalSliderThumb.normal.background = thumb;
        skin.horizontalSliderThumb.hover.background = thumbHot;
        skin.horizontalSliderThumb.active.background = thumbHot;
        skin.horizontalSliderThumb.fixedWidth = 18f;
        skin.horizontalSliderThumb.fixedHeight = 18f;

        skin.verticalScrollbar.normal.background = null;
        skin.verticalScrollbar.fixedWidth = 6f;
        skin.verticalScrollbar.margin = new RectOffset(8, 0, 0, 0);
        skin.verticalScrollbarThumb.normal.background = Rounded(16, 4f, new Color(1f, 1f, 1f, 0.2f), Color.clear, 0f);
        skin.verticalScrollbarThumb.border = new RectOffset(6, 6, 6, 6);
        skin.verticalScrollbarThumb.fixedWidth = 6f;
        skin.verticalScrollbarUpButton = new GUIStyle();
        skin.verticalScrollbarDownButton = new GUIStyle();

        Style(skin.textField, T.buttonFontSize, FontStyle.Normal, T.textColor);
        SetBackgrounds(skin.textField, field, field, field, field);
        skin.textField.border = new RectOffset(8, 8, 8, 8);
        skin.textField.padding = new RectOffset(12, 12, 8, 8);

        titleStyle = new GUIStyle(skin.label) { fontSize = T.titleFontSize, fontStyle = FontStyle.Bold, wordWrap = false };
        titleStyle.normal.textColor = Color.white;
        titleStyle.padding = new RectOffset(Px(22f * sp), 4, 0, 0);
        pageTitleStyle = new GUIStyle(skin.label) { fontSize = T.pageTitleFontSize, fontStyle = FontStyle.Bold, wordWrap = false };
        pageTitleStyle.normal.textColor = Color.white;
        noteStyle = new GUIStyle(skin.label) { fontSize = T.noteFontSize, wordWrap = false };
        noteStyle.normal.textColor = T.mutedColor;
        var noteWrapStyle = new GUIStyle(noteStyle) { wordWrap = true };

        navStyle = new GUIStyle(skin.label) { fontSize = T.navFontSize, alignment = TextAnchor.MiddleLeft, fixedHeight = 46f, wordWrap = false };
        navStyle.padding = new RectOffset(Px(22f * sp), 12, 0, 0);
        navStyle.margin = new RectOffset(0, 0, 2, 2);
        navStyle.border = new RectOffset(14, 14, 14, 14);
        // No backgrounds of its own: MenuGUI.NavEntry eases the open tint and the hover over it.
        navStyle.normal.background = null;
        navStyle.normal.textColor = T.mutedColor;
        navStyle.hover.background = null;
        navStyle.hover.textColor = T.mutedColor;
        navStyle.active.background = null;
        navStyle.active.textColor = T.textColor;
        navStyle.focused.background = null;
        navStyle.focused.textColor = T.mutedColor;
        navStyle.onNormal.background = null;
        navStyle.onNormal.textColor = Color.white;
        navStyle.onHover.background = null;
        navStyle.onHover.textColor = Color.white;
        navStyle.onActive.background = null;
        navStyle.onActive.textColor = Color.white;
        navStyle.onFocused.background = null;
        navStyle.onFocused.textColor = Color.white;

        resumeStyle = new GUIStyle(skin.button) { fontSize = T.buttonFontSize + 2, fontStyle = FontStyle.Bold, fixedHeight = 46f };
        SetBackgrounds(resumeStyle, pillOn, pillOn, Rounded(36, T.buttonCorner, Color.white, Color.clear, 0f), pillOn);
        resumeStyle.normal.textColor = Color.black;
        resumeStyle.hover.textColor = Color.black;
        resumeStyle.active.textColor = Color.black;
        resumeStyle.focused.textColor = Color.black;
        resumeStyle.margin = new RectOffset(0, 0, 0, Px(8f * sp));
        resumeStyle.padding = new RectOffset(0, 0, 0, 0);

        quietStyle = new GUIStyle(skin.button) { fontSize = T.buttonFontSize + 1, fixedHeight = 42f };
        SetBackgrounds(quietStyle, quiet, quiet, pillOn, quiet);
        quietStyle.normal.textColor = T.mutedColor;
        quietStyle.hover.textColor = T.textColor;
        quietStyle.active.textColor = Color.black;
        quietStyle.focused.textColor = T.mutedColor;
        quietStyle.margin = new RectOffset(0, 0, 3, 3);
        quietStyle.padding = new RectOffset(0, 0, 0, 0);

        dangerStyle = new GUIStyle(quietStyle);
        SetBackgrounds(dangerStyle, danger, danger, dangerHot, danger);
        dangerStyle.normal.textColor = Color.white;
        dangerStyle.hover.textColor = Color.white;
        dangerStyle.active.textColor = Color.white;
        dangerStyle.focused.textColor = Color.white;

        var sectionStyle = new GUIStyle(skin.label) { fontSize = T.sectionFontSize, fontStyle = FontStyle.Bold, wordWrap = false };
        sectionStyle.normal.textColor = T.accent;
        sectionStyle.padding = new RectOffset(2, 2, 2, 2);
        var rowLabelStyle = new GUIStyle(skin.label) { fontSize = T.bodyFontSize, alignment = TextAnchor.MiddleLeft, fixedHeight = MenuGUI.RowHeight, wordWrap = false };
        rowLabelStyle.padding = new RectOffset(2, 8, 0, 0);
        var valueStyle = new GUIStyle(skin.label) { fontSize = T.bodyFontSize, alignment = TextAnchor.MiddleCenter, fixedHeight = MenuGUI.RowHeight, wordWrap = false };
        valueStyle.padding = new RectOffset(0, 0, 0, 0);

        // The switch is drawn by MenuGUI (its knob slides); the style only reserves its size in a row.
        int switchGap = Px((MenuGUI.RowHeight - 28f) * 0.5f);
        var switchStyle = new GUIStyle { fixedWidth = 52f, fixedHeight = 28f };
        switchStyle.margin = new RectOffset(0, 0, switchGap, switchGap);

        var keyStyle = new GUIStyle(skin.button) { fontSize = T.buttonFontSize, fontStyle = FontStyle.Bold, fixedHeight = 32f };
        SetBackgrounds(keyStyle, key, key, keyListen, key);
        keyStyle.border = new RectOffset(10, 10, 10, 10);
        keyStyle.padding = new RectOffset(14, 14, 0, 0);
        keyStyle.margin = new RectOffset(4, 4, 6, 6);
        var keyListeningStyle = new GUIStyle(keyStyle);
        SetBackgrounds(keyListeningStyle, keyListen, keyListen, keyListen, keyListen);
        keyListeningStyle.normal.textColor = Color.black;
        keyListeningStyle.hover.textColor = Color.black;
        keyListeningStyle.active.textColor = Color.black;
        keyListeningStyle.focused.textColor = Color.black;

        var smallButtonStyle = new GUIStyle(skin.button) { fontSize = T.buttonFontSize, fixedHeight = 32f };
        smallButtonStyle.padding = new RectOffset(Px(12f * sp), Px(12f * sp), 0, 0);
        smallButtonStyle.margin = new RectOffset(3, 3, 6, 6);
        var smallDangerStyle = new GUIStyle(smallButtonStyle);
        SetBackgrounds(smallDangerStyle, danger, danger, dangerHot, danger);
        smallDangerStyle.normal.textColor = Color.white;
        smallDangerStyle.hover.textColor = Color.white;
        smallDangerStyle.active.textColor = Color.white;
        smallDangerStyle.focused.textColor = Color.white;

        panelStyle = new GUIStyle(skin.box) { padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
        panelStyle.normal.background = panelTex;
        panelStyle.border = new RectOffset(44, 44, 44, 44);

        shadowStyle = new GUIStyle { border = new RectOffset(60, 60, 60, 60) };
        shadowStyle.normal.background = shadowTex;

        MenuGUI.SectionStyle = sectionStyle;
        MenuGUI.RowLabelStyle = rowLabelStyle;
        MenuGUI.ValueStyle = valueStyle;
        MenuGUI.NoteStyle = noteWrapStyle;
        MenuGUI.SwitchStyle = switchStyle;
        MenuGUI.KeyStyle = keyStyle;
        MenuGUI.KeyListeningStyle = keyListeningStyle;
        MenuGUI.SmallButtonStyle = smallButtonStyle;
        MenuGUI.DangerStyle = smallDangerStyle;
        MenuGUI.HighlightStyle = new GUIStyle { border = new RectOffset(14, 14, 14, 14) };
        MenuGUI.HighlightStyle.normal.background = highlight;
        MenuGUI.OnStyle = new GUIStyle { border = new RectOffset(14, 14, 14, 14) };
        MenuGUI.OnStyle.normal.background = onTint;
        MenuGUI.SwitchTrackOff = switchOff;
        MenuGUI.SwitchTrackOn = switchOn;
        MenuGUI.SwitchGlow = switchGlow;
        MenuGUI.SwitchKnob = knob;
    }

    private static int Px(float value) => Mathf.RoundToInt(value);

    private static void SetBackgrounds(GUIStyle style, Texture2D normal, Texture2D hover, Texture2D active, Texture2D focused)
    {
        style.normal.background = normal;
        style.hover.background = hover;
        style.active.background = active;
        style.focused.background = focused;
    }

    private static void Style(GUIStyle style, int size, FontStyle fontStyle, Color color)
    {
        style.fontSize = size;
        style.fontStyle = fontStyle;
        style.normal.textColor = color;
        style.hover.textColor = color;
        style.active.textColor = color;
        style.focused.textColor = color;
        style.onNormal.textColor = color;
        style.onHover.textColor = color;
        style.onActive.textColor = color;
        style.onFocused.textColor = color;
    }

    private static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
    }

    // A radial darkening: centre colour in the middle, edge colour at the corners.
    private static Texture2D Vignette(Color centre, Color edge)
    {
        const int size = 128;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Clamp01(Mathf.Sqrt(u * u + v * v) / 1.25f);
                pixels[y * size + x] = Color.Lerp(centre, edge, Mathf.SmoothStep(0f, 1f, d));
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // Signed distance from a point to a rounded box of the given half size: negative inside.
    private static float RoundedDistance(float px, float py, float halfW, float halfH, float radius)
    {
        float qx = Mathf.Abs(px) - halfW + radius;
        float qy = Mathf.Abs(py) - halfH + radius;
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;
    }

    // Fill and rim colours for a pixel at signed distance d from an edge.
    private static Color Edge(float d, Color fill, Color rim, float rimWidth)
    {
        float fillA = Mathf.Clamp01(0.5f - d);
        float rimA = rimWidth > 0f ? Mathf.Clamp01(rimWidth * 0.5f - Mathf.Abs(d + rimWidth * 0.5f) + 0.5f) : 0f;
        Color c = Color.Lerp(fill, new Color(rim.r, rim.g, rim.b, 1f), rimA * rim.a);
        c.a = Mathf.Max(fill.a * fillA, rim.a * rimA);
        return c;
    }

    // A rounded box with an optional rim, anti-aliased, for 9-slicing (set the style border to about size / 3).
    private static Texture2D Rounded(int size, float radius, Color fill, Color rim, float rimWidth)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Edge(RoundedDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius), fill, rim, rimWidth);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A slider track: a thin rounded bar across the middle of a square, transparent above and below (border 6).
    private static Texture2D Track(int size, float barHeight, Color fill, Color rim)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = Edge(RoundedDistance(x + 0.5f - half, y + 0.5f - half, half, barHeight * 0.5f, barHeight * 0.5f), fill, rim, 1f);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A switch track: a pill drawn at its real size, not sliced. The knob is a separate disc MenuGUI slides along it.
    private static Texture2D SwitchTrack(int width, int height, Color fill, Color rim)
    {
        Texture2D tex = NewTexture(width, height);
        var pixels = new Color[width * height];
        float halfW = width * 0.5f, halfH = height * 0.5f;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = Edge(RoundedDistance(x + 0.5f - halfW, y + 0.5f - halfH, halfW, halfH, halfH), fill, rim, 1f);
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A soft-edged white disc (the switch knob; tinted when drawn).
    private static Texture2D Circle(int size)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude - (half - 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A soft shadow: a rounded box, inset by 'soft' pixels, whose edge fades out over that distance. Drawn behind the
    // panel, a little larger than it (set the style border to about soft + radius).
    private static Texture2D Shadow(int size, float radius, float soft, float alpha)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f - half, y + 0.5f - half, half - soft, half - soft, radius);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d + soft * 0.5f) / soft));
                pixels[y * size + x] = new Color(0f, 0f, 0f, a * alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }
}
