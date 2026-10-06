using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Tutorial tips: the first time something new comes up (swimming, using things, your items, locks, fighting, hunger,
// checkpoints, pufferfish...), a small card slides in at the left edge of the screen: a title, a little picture of the
// keys and mouse buttons to use (the keys the player has actually bound, so a rebind shows) and a short line. The game
// keeps going: nothing pauses, nothing waits for a key. A tip stays for a reading time (a thin gold line runs down while
// it does) and goes as soon as you do what it teaches (a tick, and it slides away). Tips come one at a time with a
// breather between them. Each shows once (remembered in PlayerPrefs); the Settings page can switch them off, and Reset
// to defaults brings them back. Show(id) from anywhere; the Quest Log shows a step's tip as the step comes up and
// watches for the rest (the first item, the first lock, low hunger, the first pufferfish...). A tip waits (and its
// clock stops) while something else has the screen (the pause menu, a puzzle board, the inspect view, the chase,
// dying, the credits).
public class TutorialCards : MonoBehaviour
{
    public const string EnabledKey = "settings.tutorials";
    private const string SeenKey = "tutorials.seen";

    private const float Gap = 2.5f;           // seconds between one tip going and the next coming
    private const float MinSeconds = 4.5f;    // reading time: at least this...
    private const float MaxSeconds = 9f;      // ...at most this, from the length of the text
    private const float DoneAfter = 0.8f;     // doing the thing counts once the tip has been up this long

    private enum Kind { Key, Wasd, Mouse, Word }
    private enum Button { None, Left, Right, Wheel, Move }

    private struct Glyph
    {
        public Kind kind;
        public string text;       // Key: an action name (or a fixed label, "#1"); Word: the word
        public Button button;     // Mouse: which part is lit
        public string caption;
    }

    private class Lesson
    {
        public string title;
        public string text;
        public Glyph[] picture;
        public string[] doneBy;   // input actions that show you have got it ("#slot" = a hotbar key or the wheel)
    }

    private static TutorialCards instance;
    private static bool? enabledSetting;
    private static HashSet<string> seen;

    public static bool IsOpen => instance != null && instance.showing != null;

    public static bool Enabled
    {
        get
        {
            enabledSetting ??= PlayerPrefs.GetInt(EnabledKey, 1) == 1;
            return enabledSetting.Value;
        }
        set
        {
            enabledSetting = value;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
        }
    }

    // Forget which tips were seen (Settings: Reset to defaults), so they show again.
    public static void ResetSeen()
    {
        seen = new HashSet<string>();
        PlayerPrefs.DeleteKey(SeenKey);
    }

    public static void Show(string id)
    {
        if (string.IsNullOrEmpty(id) || !Enabled || !Lessons.ContainsKey(id))
            return;
        LoadSeen();
        if (seen.Contains(id))
            return;
        if (instance == null)
            instance = new GameObject("TutorialCards").AddComponent<TutorialCards>();
        if (!instance.queue.Contains(id) && instance.showingId != id)
            instance.queue.Enqueue(id);
    }

    private static void LoadSeen()
    {
        if (seen != null)
            return;
        seen = new HashSet<string>();
        foreach (string id in PlayerPrefs.GetString(SeenKey, "").Split(','))
            if (!string.IsNullOrEmpty(id))
                seen.Add(id);
    }

    private static void MarkSeen(string id)
    {
        LoadSeen();
        seen.Add(id);
        PlayerPrefs.SetString(SeenKey, string.Join(",", seen));
    }

    // ---- The lessons ----------------------------------------------------------------------------------------------

    private static Glyph K(string action, string caption = null) => new Glyph { kind = Kind.Key, text = action, caption = caption };
    private static Glyph M(Button button, string caption = null) => new Glyph { kind = Kind.Mouse, button = button, caption = caption };
    private static Glyph W(string word) => new Glyph { kind = Kind.Word, text = word };
    private static readonly Glyph Wasd = new Glyph { kind = Kind.Wasd, caption = "swim" };

    private static readonly Dictionary<string, Lesson> Lessons = new Dictionary<string, Lesson>
    {
        ["swim"] = new Lesson { title = "Swimming", text = "You swim the way you look. Space goes up, Ctrl down.",
            picture = new[] { Wasd, M(Button.Move, "look") }, doneBy = new[] { "Move" } },
        ["dash"] = new Lesson { title = "Dash", text = "A burst of speed. Costs a little hunger; the ring round the dot shows when it is back.",
            picture = new[] { K("Sprint", "dash") }, doneBy = new[] { "Sprint" } },
        ["goals"] = new Lesson { title = "Your goal", text = "The card at the top says what to do next. Stuck for a while? A hint and a gold marker show up.",
            picture = new Glyph[0] },
        ["interact"] = new Lesson { title = "Using things", text = "Put the dot on something with a white outline and press the key.",
            picture = new[] { K("Interact", "use") }, doneBy = new[] { "Interact" } },
        ["items"] = new Lesson { title = "Your items", text = "Pick what to hold with the number keys or the wheel. The last slot is for your weapon.",
            picture = new[] { K("#1"), K("#2"), K("#3"), M(Button.Wheel) }, doneBy = new[] { "#slot" } },
        ["locks"] = new Lesson { title = "Keys and locks", text = "Hold the item it needs and use the lock. Green prompt = you have it, red = not yet.",
            picture = new[] { K("#1", "hold"), K("Interact", "use") }, doneBy = new[] { "Interact" } },
        ["fight"] = new Lesson { title = "Fighting", text = "Click to slash. One slash cuts everything in its arc.",
            picture = new[] { M(Button.Left, "slash") }, doneBy = new[] { "Attack" } },
        ["eat"] = new Lesson { title = "Hunger", text = "Your fish drains as you swim. Eat the green fish that float up after a kill to fill it.",
            picture = new[] { K("Interact", "eat") }, doneBy = new[] { "Interact" } },
        ["checkpoint"] = new Lesson { title = "Checkpoints", text = "Use a checkpoint pillar to save. Dying takes you back to it.",
            picture = new[] { K("Interact", "save") }, doneBy = new[] { "Interact" } },
        ["push"] = new Lesson { title = "Pushing boxes", text = "Swim into the side of a box to push it along. You can't pull them back, so mind the walls and corners.",
            picture = new[] { new Glyph { kind = Kind.Wasd, caption = "swim into it" } }, doneBy = new[] { "Move" } },
        ["danger"] = new Lesson { title = "Pufferfish", text = "They chase and bite. Slash them or dash away; break their red nests to stop more coming.",
            picture = new[] { M(Button.Left, "slash"), K("Sprint", "dash") }, doneBy = new[] { "Attack", "Sprint" } },
    };

    // ---- Showing --------------------------------------------------------------------------------------------------

    private readonly Queue<string> queue = new Queue<string>();
    private Lesson showing;
    private string showingId;
    private float age;            // seconds it has been on screen (stops while hidden)
    private float lifetime;       // how long it stays
    private float doneAt = -1f;   // age when the player did the thing (it goes then)
    private float moved;          // seconds of swimming, for the swim tip
    private float nextAt;         // unscaled time the next tip may come
    private bool hidden;
    private InputActionAsset actions;

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        hidden = !Clear();
        if (showing == null)
        {
            if (queue.Count > 0 && !hidden && Time.unscaledTime >= nextAt)
                Open(queue.Dequeue());
            return;
        }
        if (hidden)
            return;   // the clock stops while something else has the screen
        age += Time.unscaledDeltaTime;
        if (doneAt < 0f && age >= DoneAfter && DidIt())
            doneAt = age;
        float end = doneAt >= 0f ? doneAt + 0.9f : lifetime;
        if (age >= end)
        {
            showing = null;
            showingId = null;
            nextAt = Time.unscaledTime + Gap;
        }
    }

    // Nothing else has the screen: no menu, board, inspect view, chase, death or credits.
    private static bool Clear()
    {
        if (PauseMenu.IsOpen || PuzzleBoard.IsOpen || EndCredits.Playing)
            return false;
        ChaseSequence chase = ChaseSequence.Active;
        if (chase != null && chase.IsRunning)
            return false;
        var interactor = FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null && interactor.Busy)
            return false;   // the inspect view, the knot board...
        var death = FindFirstObjectByType<DeathManager>();
        return death == null || !death.IsDead;
    }

    private void Open(string id)
    {
        if (!Lessons.TryGetValue(id, out Lesson lesson))
            return;
        showing = lesson;
        showingId = id;
        age = 0f;
        doneAt = -1f;
        moved = 0f;
        int words = lesson.text.Split(' ').Length + lesson.title.Split(' ').Length;
        lifetime = Mathf.Clamp(2.5f + words * 0.3f, MinSeconds, MaxSeconds);
        MarkSeen(id);
    }

    // Has the player just done what the tip teaches?
    private bool DidIt()
    {
        if (showing.doneBy == null)
            return false;
        foreach (string what in showing.doneBy)
        {
            if (what == "#slot")
            {
                Keyboard keys = Keyboard.current;
                if (keys != null)
                    for (int i = 0; i < 5; i++)
                        if (keys[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                            return true;
                Mouse mouse = Mouse.current;
                if (mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
                    return true;
                continue;
            }
            InputAction action = Action(what);
            if (action == null)
                continue;
            if (what == "Move")
            {
                // Swimming for a moment, not a tap.
                if (action.ReadValue<Vector2>().sqrMagnitude > 0.25f)
                    moved += Time.unscaledDeltaTime;
                if (moved >= 0.6f)
                    return true;
                continue;
            }
            if (action.WasPressedThisFrame())
                return true;
        }
        return false;
    }

    // ---- Drawing --------------------------------------------------------------------------------------------------

    // A small card at the left edge, below the stats: it slides in, a tick when you have done it, then slides out.
    private void OnGUI()
    {
        if (showing == null || hidden || Event.current.type != EventType.Repaint)
            return;
        GUI.depth = -800;
        float s = HudStyle.Scale;
        float end = doneAt >= 0f ? doneAt + 0.9f : lifetime;
        float appear = Ease.OutCubic(Mathf.Clamp01(age / 0.35f));
        float leave = Ease.OutCubic(Mathf.Clamp01((end - age) / 0.35f));
        float alpha = Mathf.Min(appear, leave);
        if (alpha <= 0f)
            return;

        float w = 360f * s, pad = 16f * s;
        GUIStyle label = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(10.5f * s)));
        GUIStyle titleStyle = HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(19f * s)));
        GUIStyle body = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(14f * s)), TextAnchor.UpperLeft, true);
        bool hasPicture = showing.picture != null && showing.picture.Length > 0;
        float gs = s * 0.72f;   // the key and mouse pictures, smaller than on the old full-screen card
        float pictureH = hasPicture ? 130f * gs : 0f;
        float textH = body.CalcHeight(new GUIContent(showing.text), w - pad * 2f);
        float h = pad + 16f * s + 26f * s + (hasPicture ? pictureH + 6f * s : 0f) + textH + pad + 4f * s;
        float slide = (1f - alpha) * -40f * s;
        var card = new Rect(24f * s + slide, Screen.height * 0.3f, w, h);

        HudStyle.Panel(card, 12f * s, alpha);
        HudStyle.Fill(new Rect(card.x, card.y + 12f * s, Mathf.Max(2f, 3f * s), card.height - 24f * s), 1.5f * s,
            new Color(HudStyle.Gold.r, HudStyle.Gold.g, HudStyle.Gold.b, 0.85f * alpha));   // a gold edge down the left

        float y = card.y + pad;
        HudStyle.Spaced(new Vector2(card.x + pad, y), "TIP", label, HudStyle.Gold, 2.2f * s, alpha);
        y += 16f * s;
        HudStyle.Write(new Rect(card.x + pad, y, w - pad * 2f, 26f * s), showing.title, titleStyle, HudStyle.Text, alpha);
        if (doneAt >= 0f && HudStyle.BeginShapes())
        {
            // Done: a green tick at the right of the title, drawn (the game font has no tick).
            float pop = Ease.OutCubic(Mathf.Clamp01((age - doneAt) / 0.25f));
            var at = new Vector2(card.xMax - pad - 10f * s, y + 13f * s);
            float k = 7f * s * (0.6f + 0.4f * pop);
            var green = new Color(HudStyle.Ready.r, HudStyle.Ready.g, HudStyle.Ready.b, alpha * pop);
            HudStyle.Line(at + new Vector2(-k, 0f), at + new Vector2(-k * 0.35f, k * 0.65f), 2.4f * s, green);
            HudStyle.Line(at + new Vector2(-k * 0.35f, k * 0.65f), at + new Vector2(k, -k * 0.7f), 2.4f * s, green);
            HudStyle.EndShapes();
        }
        y += 26f * s;
        if (hasPicture)
        {
            DrawPicture(new Rect(card.x + pad, y, w - pad * 2f, pictureH), gs, alpha);
            y += pictureH + 6f * s;
        }
        HudStyle.Write(new Rect(card.x + pad, y, w - pad * 2f, textH), showing.text, body, HudStyle.Muted, alpha);

        // Time left: a thin gold line along the bottom, running down (full while you do the thing).
        float left = doneAt >= 0f ? 1f : Mathf.Clamp01(1f - age / Mathf.Max(0.1f, lifetime));
        var line = new Rect(card.x + pad, card.yMax - 7f * s, (w - pad * 2f) * left, Mathf.Max(1.5f, 2f * s));
        if (line.width > 2f)
            HudStyle.Fill(line, 1f * s, new Color(HudStyle.Gold.r, HudStyle.Gold.g, HudStyle.Gold.b, 0.5f * alpha));
    }

    // The glyphs in a row, from the left, each with its caption under it.
    private void DrawPicture(Rect area, float s, float alpha)
    {
        float gap = 14f * s;
        float x = area.x;
        float mid = area.y + area.height * 0.42f;
        GUIStyle caption = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(13f * s)), TextAnchor.MiddleCenter);
        foreach (Glyph g in showing.picture)
        {
            float width = Width(g, s);
            var box = new Rect(x, area.y, width, area.height);
            Draw(g, box, mid, s, alpha);
            if (!string.IsNullOrEmpty(g.caption))
                HudStyle.Write(new Rect(box.x - 20f * s, area.yMax - 22f * s, box.width + 40f * s, 20f * s), g.caption, caption, HudStyle.Muted, alpha);
            x += width + gap;
        }
    }

    private float Width(Glyph g, float s)
    {
        switch (g.kind)
        {
            case Kind.Wasd: return 3f * 46f * s + 2f * 6f * s;
            case Kind.Mouse: return 64f * s;
            case Kind.Word: return HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(18f * s))).CalcSize(new GUIContent(g.text)).x;
            default: return Mathf.Max(46f * s, HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(17f * s))).CalcSize(new GUIContent(KeyLabel(g.text))).x + 24f * s);
        }
    }

    private void Draw(Glyph g, Rect box, float mid, float s, float alpha)
    {
        switch (g.kind)
        {
            case Kind.Word:
                HudStyle.Write(new Rect(box.x, mid - 14f * s, box.width, 28f * s), g.text, HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(18f * s)), TextAnchor.MiddleCenter), HudStyle.Muted, alpha);
                break;
            case Kind.Key:
                KeyCap(new Rect(box.x, mid - 23f * s, box.width, 46f * s), KeyLabel(g.text), s, alpha);
                break;
            case Kind.Wasd:
            {
                float k = 46f * s, gap = 6f * s;
                float left = box.x, top = mid - k - gap * 0.5f;
                KeyCap(new Rect(left + k + gap, top, k, k), MoveKey("up", "W"), s, alpha);
                KeyCap(new Rect(left, top + k + gap, k, k), MoveKey("left", "A"), s, alpha);
                KeyCap(new Rect(left + k + gap, top + k + gap, k, k), MoveKey("down", "S"), s, alpha);
                KeyCap(new Rect(left + 2f * (k + gap), top + k + gap, k, k), MoveKey("right", "D"), s, alpha);
                break;
            }
            case Kind.Mouse:
                DrawMouse(new Rect(box.x + (box.width - 52f * s) * 0.5f, mid - 40f * s, 52f * s, 80f * s), g.button, s, alpha);
                break;
        }
    }

    private static void KeyCap(Rect r, string text, float s, float alpha)
    {
        HudStyle.Fill(new Rect(r.x, r.y + 4f * s, r.width, r.height), 8f * s, new Color(0f, 0f, 0f, 0.35f * alpha));
        HudStyle.Fill(r, 8f * s, new Color(HudStyle.KeyCap.r, HudStyle.KeyCap.g, HudStyle.KeyCap.b, alpha));
        HudStyle.Write(r, text, HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(17f * s)), TextAnchor.MiddleCenter), HudStyle.KeyInk, alpha);
    }

    // A mouse from above: the body, the split between the buttons, the wheel; the part that matters in gold (or arrows
    // either side for moving it).
    private static void DrawMouse(Rect r, Button lit, float s, float alpha)
    {
        Color body = new Color(HudStyle.KeyCap.r, HudStyle.KeyCap.g, HudStyle.KeyCap.b, alpha);
        Color gold = new Color(HudStyle.Gold.r, HudStyle.Gold.g, HudStyle.Gold.b, alpha);
        HudStyle.Fill(new Rect(r.x, r.y + 4f * s, r.width, r.height), r.width * 0.45f, new Color(0f, 0f, 0f, 0.35f * alpha));
        HudStyle.Fill(r, r.width * 0.45f, body);
        float buttonsH = r.height * 0.42f;
        if (lit == Button.Left)
            HudStyle.Fill(new Rect(r.x + 3f * s, r.y + 3f * s, r.width * 0.5f - 4f * s, buttonsH), r.width * 0.3f, gold);
        if (lit == Button.Right)
            HudStyle.Fill(new Rect(r.center.x + 1f * s, r.y + 3f * s, r.width * 0.5f - 4f * s, buttonsH), r.width * 0.3f, gold);
        HudStyle.Fill(new Rect(r.center.x - 1f * s, r.y + 4f * s, 2f * s, buttonsH), 1f, new Color(HudStyle.KeyInk.r, HudStyle.KeyInk.g, HudStyle.KeyInk.b, 0.5f * alpha));
        HudStyle.Fill(new Rect(r.x + 2f * s, r.y + buttonsH + 2f * s, r.width - 4f * s, 2f * s), 1f, new Color(HudStyle.KeyInk.r, HudStyle.KeyInk.g, HudStyle.KeyInk.b, 0.35f * alpha));
        Color wheel = lit == Button.Wheel ? gold : new Color(HudStyle.KeyInk.r, HudStyle.KeyInk.g, HudStyle.KeyInk.b, 0.7f * alpha);
        HudStyle.Fill(new Rect(r.center.x - 4f * s, r.y + buttonsH * 0.35f, 8f * s, buttonsH * 0.5f), 4f * s, wheel);
        if (lit == Button.Move)
        {
            GUIStyle arrows = HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(20f * s)), TextAnchor.MiddleCenter);
            HudStyle.Write(new Rect(r.x - 30f * s, r.center.y - 14f * s, 24f * s, 28f * s), "<", arrows, gold, alpha);
            HudStyle.Write(new Rect(r.xMax + 6f * s, r.center.y - 14f * s, 24f * s, 28f * s), ">", arrows, gold, alpha);
        }
    }

    // ---- Key names ------------------------------------------------------------------------------------------------

    // "#1" = a fixed label; otherwise the first keyboard / mouse key the action is bound to (a rebind shows).
    private static string KeyLabel(string action)
    {
        if (string.IsNullOrEmpty(action))
            return "";
        if (action[0] == '#')
            return action.Substring(1);
        InputAction found = FindAction(action);
        if (found != null)
            for (int i = 0; i < found.bindings.Count; i++)
            {
                InputBinding b = found.bindings[i];
                string path = b.effectivePath;
                if (b.isComposite || b.isPartOfComposite || string.IsNullOrEmpty(path) || !(path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>")))
                    continue;
                string shown = found.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                if (!string.IsNullOrEmpty(shown))
                    return shown.ToUpperInvariant();
            }
        switch (action)
        {
            case "Jump": return "SPACE";
            case "Crouch": return "CTRL";
            case "Sprint": return "SHIFT";
            case "Interact": return "E";
            default: return action.ToUpperInvariant();
        }
    }

    // One direction of the Move composite on the keyboard (a rebind shows), else the usual letter.
    private static string MoveKey(string part, string fallback)
    {
        InputAction move = FindAction("Move");
        if (move != null)
            for (int i = 0; i < move.bindings.Count; i++)
            {
                InputBinding b = move.bindings[i];
                if (b.isPartOfComposite && b.name == part && !string.IsNullOrEmpty(b.effectivePath) && b.effectivePath.StartsWith("<Keyboard>"))
                {
                    string shown = move.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    if (!string.IsNullOrEmpty(shown))
                        return shown.ToUpperInvariant();
                }
            }
        return fallback;
    }

    // The player's input action by name, from the Swim Controller's input asset (kept once found).
    private InputAction Action(string name)
    {
        if (actions == null)
        {
            var swimmer = FindFirstObjectByType<SwimController>();
            actions = swimmer != null ? swimmer.InputActions : null;
        }
        return actions != null ? actions.FindAction("Player/" + name) : null;
    }

    private static InputAction FindAction(string name)
    {
        if (instance != null)
            return instance.Action(name);
        var swimmer = FindFirstObjectByType<SwimController>();
        InputActionAsset asset = swimmer != null ? swimmer.InputActions : null;
        return asset != null ? asset.FindAction("Player/" + name) : null;
    }
}
