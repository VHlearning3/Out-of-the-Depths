using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// The ending: the screen fades to black, the title comes up with a line of thanks, then the credits roll (the team and
// every sound, model and font the game uses, from Assets/Resources/Credits.asset, the same list as the main menu's
// Credits page), and it closes on THE END; any key then goes back to the main menu (or starts the level again when
// there is no main menu in the build). Space, Enter or a click speeds the roll up; Escape skips it.
// EndCredits.Play() starts it from anywhere (the Chase Sequence does, once the last door is opened). The player is held
// still and the pause menu kept shut meanwhile. Drawn over everything else, in the game font.
public class EndCredits : MonoBehaviour
{
    private const float FadeSeconds = 2f;
    private const float TitleSeconds = 4.5f;
    private const float RollSpeed = 70f;        // pixels per second at 1080p
    private const float FastForward = 5f;

    public static bool Playing => instance != null;
    private static EndCredits instance;

    private enum Phase { Fading, Title, Rolling, TheEnd, Leaving }

    private struct Line
    {
        public string text;
        public int size;
        public Color color;
        public float gapBefore;
        public bool spaced;       // small capitals spread out (a heading)
    }

    private Phase phase = Phase.Fading;
    private float phaseAt;
    private float roll;            // how far the credits have scrolled, in 1080p pixels
    private float rollHeight;      // their full height, in 1080p pixels
    private readonly List<Line> lines = new List<Line>();
    private Texture2D black;
    private readonly Dictionary<int, GUIStyle> styles = new Dictionary<int, GUIStyle>();

    public static void Play()
    {
        if (instance != null)
            return;
        instance = new GameObject("EndCredits").AddComponent<EndCredits>();
    }

    private void Start()
    {
        phaseAt = Time.unscaledTime;
        black = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
        black.SetPixel(0, 0, Color.white);
        black.Apply();
        HoldPlayer();
        BuildLines();
    }

    private void OnDestroy()
    {
        if (black != null)
            Destroy(black);
        PauseMenu.CaptureInput(false);
        if (instance == this)
            instance = null;
    }

    // Nothing moves, nothing answers the keys, no pause menu, no reticle: it is the ending.
    private static void HoldPlayer()
    {
        var swimmer = FindFirstObjectByType<SwimController>();
        if (swimmer != null)
        {
            swimmer.Frozen = true;
            swimmer.LookLocked = true;
        }
        var interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null)
            interactor.Busy = true;
        var inventory = FindFirstObjectByType<PlayerInventory>();
        if (inventory != null)
            inventory.InputBlocked = true;
        var slash = FindFirstObjectByType<SlashAttack>();
        if (slash != null)
            slash.SetCanAttack(false);
        // Nothing can end it early: no damage, no hunger (the scene is left afterwards, so these are never undone).
        var damage = FindFirstObjectByType<DamageManager>();
        if (damage != null)
            damage.GodMode = true;
        var hunger = FindFirstObjectByType<HungerSystem>();
        if (hunger != null)
            hunger.DrainPaused = true;
        HitMarker.SetReticleVisible(false);
        PauseMenu.CaptureInput(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // The roll, top to bottom: the title, the team, then every asset by kind, and a last word.
    private void BuildLines()
    {
        var gold = new Color(1f, 0.8f, 0.42f);
        var text = new Color(0.91f, 0.97f, 0.97f);
        var muted = new Color(0.6f, 0.73f, 0.75f);
        void Add(string words, int size, Color color, float gap = 0f, bool spaced = false) =>
            lines.Add(new Line { text = words, size = size, color = color, gapBefore = gap, spaced = spaced });

        CreditsList credits = Resources.Load<CreditsList>("Credits");
        string title = credits != null && !string.IsNullOrEmpty(credits.title) ? credits.title : "Out of the Depths";
        Add(title.ToUpperInvariant(), 64, text);
        Add("a game by", 22, muted, 60f, true);
        if (credits != null && credits.team != null)
            foreach (CreditsList.Member member in credits.team)
            {
                if (member == null || string.IsNullOrEmpty(member.name))
                    continue;
                Add(member.name, 40, text, 26f);
                if (!string.IsNullOrEmpty(member.contributions))
                    foreach (string part in member.contributions.Split('\n'))
                        if (!string.IsNullOrWhiteSpace(part))
                            Add(part.Trim(), 20, muted, 4f);
            }
        if (credits != null && credits.entries != null && credits.entries.Length > 0)
        {
            Add(string.IsNullOrEmpty(credits.assetsHeading) ? "Made with" : credits.assetsHeading, 22, muted, 110f);
            foreach (string category in credits.Categories())
            {
                Add(string.IsNullOrEmpty(category) ? "OTHER" : category.ToUpperInvariant(), 22, gold, 60f, true);
                bool first = true;
                foreach (CreditsList.Entry entry in credits.entries)
                {
                    if (entry == null || entry.hidden || (entry.category ?? "") != category)
                        continue;
                    Add(entry.Line, 18, text, first ? 14f : 6f);
                    first = false;
                }
            }
        }
        Add("Thank you for playing", 34, gold, 140f);
    }

    private void Update()
    {
        float since = Time.unscaledTime - phaseAt;
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        bool skip = keys != null && keys.escapeKey.wasPressedThisFrame;
        bool fast = (keys != null && (keys.spaceKey.isPressed || keys.enterKey.isPressed)) || (mouse != null && mouse.leftButton.isPressed);
        bool anyKey = (keys != null && keys.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame);

        switch (phase)
        {
            case Phase.Fading:
                if (since >= FadeSeconds + 0.6f)
                    Next(Phase.Title);
                break;
            case Phase.Title:
                if (since >= TitleSeconds || skip || (fast && since > 1f))
                    Next(skip ? Phase.TheEnd : Phase.Rolling);
                break;
            case Phase.Rolling:
                roll += RollSpeed * (fast ? FastForward : 1f) * Time.unscaledDeltaTime;
                if (skip || roll >= rollHeight + 1080f * 0.55f)
                    Next(Phase.TheEnd);
                break;
            case Phase.TheEnd:
                if (since > 1.5f && anyKey)
                    Next(Phase.Leaving);
                break;
            case Phase.Leaving:
                if (since >= 1f)
                    Leave();
                break;
        }
    }

    private void Next(Phase next)
    {
        phase = next;
        phaseAt = Time.unscaledTime;
        if (next == Phase.Rolling)
            roll = 0f;
    }

    // Back to the main menu, or the level from the top when the build has no main menu.
    private void Leave()
    {
        const string menu = "MainMenu";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Application.CanStreamedLevelBeLoaded(menu))
            SceneManager.LoadScene(menu);
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnGUI()
    {
        GUI.depth = -1000;   // over everything else drawn in code
        if (Event.current.type != EventType.Repaint)
            return;
        float s = Screen.height / 1080f;
        float since = Time.unscaledTime - phaseAt;
        Color keep = GUI.color;

        float dark = phase == Phase.Fading ? Mathf.SmoothStep(0f, 1f, since / FadeSeconds) : 1f;
        GUI.color = new Color(0f, 0f, 0f, dark);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), black);

        switch (phase)
        {
            case Phase.Title:
            {
                float a = Mathf.Clamp01(since / 1.2f) * Mathf.Clamp01((TitleSeconds - since) / 1f);
                CreditsList credits = Resources.Load<CreditsList>("Credits");
                string title = credits != null && !string.IsNullOrEmpty(credits.title) ? credits.title : "Out of the Depths";
                Centered(title.ToUpperInvariant(), 80, new Color(0.91f, 0.97f, 0.97f, a), Screen.height * 0.45f, s);
                Centered("You made it out of the depths.", 26, new Color(0.6f, 0.73f, 0.75f, a * Mathf.Clamp01((since - 1f) / 1f)), Screen.height * 0.45f + 80f * s, s);
                break;
            }
            case Phase.Rolling:
                DrawRoll(s);
                break;
            case Phase.TheEnd:
            case Phase.Leaving:
            {
                float a = phase == Phase.TheEnd ? Mathf.Clamp01(since / 1.2f) : 1f - Mathf.Clamp01(since / 0.8f);
                Centered("THE END", 72, new Color(0.91f, 0.97f, 0.97f, a), Screen.height * 0.45f, s);
                if (phase == Phase.TheEnd && since > 1.5f)
                {
                    float blink = 0.55f + 0.45f * Mathf.Sin((since - 1.5f) * 2.4f);
                    Centered("Press any key", 22, new Color(0.6f, 0.73f, 0.75f, blink * Mathf.Clamp01((since - 1.5f) / 0.6f)), Screen.height * 0.45f + 90f * s, s);
                }
                break;
            }
        }
        GUI.color = keep;
    }

    // Every line from the bottom of the screen upwards; each fades in at the bottom edge and out at the top.
    private void DrawRoll(float s)
    {
        float y = Screen.height + 40f * s - roll * s;
        float total = 0f;
        float width = Mathf.Min(Screen.width - 80f * s, 1100f * s);
        foreach (Line line in lines)
        {
            GUIStyle style = Style(Mathf.Max(8, Mathf.RoundToInt(line.size * s)));
            string words = line.spaced ? Spread(line.text) : line.text;
            float h = style.CalcHeight(new GUIContent(words), width);
            y += line.gapBefore * s;
            total += line.gapBefore + h / s;
            if (y + h > 0f && y < Screen.height)
            {
                float edge = Mathf.Min(Mathf.Clamp01(y / (Screen.height * 0.12f)), Mathf.Clamp01((Screen.height - y - h) / (Screen.height * 0.12f)));
                GUI.color = new Color(line.color.r, line.color.g, line.color.b, line.color.a * edge);
                GUI.Label(new Rect((Screen.width - width) * 0.5f, y, width, h), words, style);
            }
            y += h;
        }
        rollHeight = total + 40f;
    }

    private void Centered(string words, int size, Color color, float y, float s)
    {
        GUIStyle style = Style(Mathf.Max(8, Mathf.RoundToInt(size * s)));
        float width = Screen.width - 80f * s;
        float h = style.CalcHeight(new GUIContent(words), width);
        GUI.color = color;
        GUI.Label(new Rect(40f * s, y - h * 0.5f, width, h), words, style);
    }

    private static string Spread(string words) => string.Join(" ", words.ToUpperInvariant().ToCharArray());

    private GUIStyle Style(int size)
    {
        if (styles.TryGetValue(size, out GUIStyle style) && style != null)
            return style;
        style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = TextAnchor.UpperCenter, wordWrap = true, font = GameFont.Font };
        style.normal.textColor = Color.white;
        return styles[size] = style;
    }
}
