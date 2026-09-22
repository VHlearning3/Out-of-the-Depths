using System;
using UnityEngine;

// Everything about how the pause menu looks, reads and sounds, in one asset: colours, layout, font and sizes, the
// labels, the motion, the sounds, and which pages show under what name. Assets > Create > Out of the Depths > Pause
// Menu Theme (or Tools > Out of the Depths > Create Pause Menu Theme), assign it to the Pause Menu on the Player; every
// scene can share one. Edits show up live, even in Play mode. With no theme assigned the menu uses these defaults.
[CreateAssetMenu(fileName = "PauseMenuTheme", menuName = "Out of the Depths/Pause Menu Theme")]
public class PauseMenuTheme : ScriptableObject
{
    [Serializable]
    public class PageOverride
    {
        [Tooltip("The page as its script names it: Map, Settings, Keybindings, Admin, or a page of your own.")]
        public string page = "";
        [Tooltip("What the sidebar calls it. Empty = the page's own name.")]
        public string label = "";
        [Tooltip("Leave it out of the menu.")]
        public bool hidden = false;
        [Tooltip("Where it sorts (lower = higher up). 0 = the page's own order.")]
        public int order = 0;
    }

    [Header("Text")]
    public string title = "PAUSED";
    public string resumeLabel = "Resume";
    public string mainMenuLabel = "Main menu";
    public string quitLabel = "Quit";
    public string quitConfirm = "Quit the game?";
    public string cancelLabel = "Cancel";
    [Tooltip("Shown top right of the page. {key} is the toggle key.")]
    public string resumeHint = "{key} to resume";
    public string emptyPageText = "Nothing here yet. Any script that implements IPauseMenuPage shows up as a page.";

    [Header("Pages")]
    [Tooltip("Rename, hide or reorder pages. Pages not listed keep their own name and order.")]
    public PageOverride[] pages = new PageOverride[0];

    [Header("Colours")]
    public Color accent = new Color(0.35f, 0.85f, 0.95f);
    public Color panelColor = new Color(0.05f, 0.08f, 0.12f, 0.94f);
    public Color fieldColor = new Color(0.02f, 0.04f, 0.07f, 0.95f);
    [Tooltip("Darkening over the game: the middle of the screen and the edges (a vignette).")]
    public Color backdropCentre = new Color(0f, 0.02f, 0.05f, 0.7f);
    public Color backdropEdge = new Color(0f, 0.01f, 0.03f, 0.92f);
    public Color buttonColor = new Color(0.13f, 0.19f, 0.27f);
    public Color buttonHover = new Color(0.2f, 0.32f, 0.42f);
    public Color textColor = new Color(0.93f, 0.96f, 1f);
    public Color mutedColor = new Color(0.6f, 0.68f, 0.76f);
    public Color dangerColor = new Color(0.85f, 0.3f, 0.3f);

    [Header("Layout")]
    public float panelWidth = 1040f;
    public float panelHeight = 760f;
    public float sidebarWidth = 250f;
    [Tooltip("The page list on the right instead of the left.")]
    public bool sidebarOnRight = false;
    [Tooltip("Extra size on top of the automatic scaling (the menu grows with the screen height).")]
    public float scale = 1f;
    [Tooltip("Room between things: every padding, gap and margin is multiplied by this.")]
    [Range(0.6f, 1.8f)] public float spacing = 1f;
    [Tooltip("How rounded the panel's corners are, in pixels.")]
    [Range(0f, 50f)] public float panelCorner = 26f;
    [Tooltip("How rounded buttons, switches and fields are, in pixels.")]
    [Range(0f, 17f)] public float buttonCorner = 10f;

    [Header("Type")]
    [Tooltip("Empty = the game font (the file in Assets/Resources/Fonts), else the default font.")]
    public Font font;
    public int titleFontSize = 30;
    public int pageTitleFontSize = 24;
    public int navFontSize = 17;
    public int bodyFontSize = 15;
    public int buttonFontSize = 14;
    public int noteFontSize = 13;
    public int sectionFontSize = 12;

    [Header("Motion")]
    [Tooltip("Seconds for the menu to fade and ease in, and out again.")]
    public float fadeSeconds = 0.28f;
    [Tooltip("How far the panel rises into place while fading in, in pixels, and how much smaller it starts.")]
    public float riseDistance = 24f;
    [Range(0.8f, 1f)] public float startScale = 0.96f;
    [Tooltip("Seconds for a page to fade and rise in when you switch pages.")]
    public float pageFadeSeconds = 0.2f;
    [Tooltip("How quickly hover highlights, the switch knobs, the sidebar marker and wheel scrolling ease, per second.")]
    public float smoothing = 16f;

    [Header("Sound")]
    [Tooltip("A quiet tick when the mouse moves onto a button, entry, switch or key (Assets/Sound/UI).")]
    public AudioClip hoverSound;
    [Range(0f, 1f)] public float hoverVolume = 0.35f;
    [Tooltip("Played when the menu opens / closes. Optional.")]
    public AudioClip openSound;
    public AudioClip closeSound;
    [Range(0f, 1f)] public float menuVolume = 0.6f;

    // Bumped whenever any theme is edited, so open menus rebuild their look (live in Play mode too).
    public static int Version { get; private set; }

    private static PauseMenuTheme defaults;

    // The built-in look, for a Pause Menu with no theme assigned.
    public static PauseMenuTheme Default
    {
        get
        {
            if (defaults == null)
            {
                defaults = CreateInstance<PauseMenuTheme>();
                defaults.name = "PauseMenuTheme (defaults)";
                defaults.hideFlags = HideFlags.HideAndDontSave;
            }
            return defaults;
        }
    }

    private void OnValidate()
    {
        Version++;
    }

    public bool IsHidden(string page)
    {
        PageOverride o = Find(page);
        return o != null && o.hidden;
    }

    public string LabelFor(string page)
    {
        PageOverride o = Find(page);
        return o != null && !string.IsNullOrEmpty(o.label) ? o.label : page;
    }

    public int OrderFor(string page, int own)
    {
        PageOverride o = Find(page);
        return o != null && o.order != 0 ? o.order : own;
    }

    private PageOverride Find(string page)
    {
        if (pages == null)
            return null;
        foreach (PageOverride o in pages)
            if (o != null && string.Equals(o.page, page, StringComparison.OrdinalIgnoreCase))
                return o;
        return null;
    }
}
