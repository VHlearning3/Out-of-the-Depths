using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Tutorial cards: the first time something new comes up (swimming, using things, your items, locks, fighting, hunger,
// checkpoints, pufferfish...), the game pauses and a card shows how it is done: a title, a drawn picture of the keys
// and mouse buttons to use (the keys the player has actually bound, so a rebind shows), and a line or two. Any key or a
// click carries on. Each card shows once (remembered in PlayerPrefs); the Settings page can switch them off, and Reset to
// defaults brings them back. Show(id) from anywhere; the Quest Log shows a step's card as the step comes up and watches
// for the rest (the first item, the first lock, low hunger, the first pufferfish...). A card waits while something
// else has the screen (the pause menu, a puzzle board, the inspect view, the chase, dying, the credits).
public class TutorialCards : MonoBehaviour
{
    public const string EnabledKey = "settings.tutorials";
    private const string SeenKey = "tutorials.seen";

    private enum Kind { Key, Wasd, Mouse, Word }
    private enum Button { None, Left, Right, Wheel, Move }

    private struct Glyph
    {
        public Kind kind;
        public string text;       // Key: an action name (or a fixed label, "1"); Word: the word
        public Button button;     // Mouse: which part is lit
        public string caption;
    }

    private class Lesson
    {
        public string title;
        public string text;
        public Glyph[] picture;
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

    // Forget which cards were seen (Settings: Reset to defaults), so they show again.
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
        ["swim"] = new Lesson { title = "Swimming", text = "Swim with W A S D; you swim the way you look, so turn with the mouse. Space takes you up, Ctrl down.",
            picture = new[] { Wasd, W("+"), M(Button.Move, "look"), K("Jump", "up"), K("Crouch", "down") } },
        ["dash"] = new Lesson { title = "Dash", text = "A quick burst of speed. It uses a little hunger and needs a moment to recharge: the ring round the dot shows when it is ready.",
            picture = new[] { K("Sprint", "dash") } },
        ["goals"] = new Lesson { title = "Your goal", text = "The card at the top of the screen says what to do next, loosely. Explore; if you are stuck for a while, a hint and a gold marker show up.",
            picture = new Glyph[0] },
        ["interact"] = new Lesson { title = "Using things", text = "Put the dot in the middle of the screen on something and press the key: it gets a white outline when you can use it. Drawers, doors, levers, fish to eat...",
            picture = new[] { K("Interact", "use") } },
        ["items"] = new Lesson { title = "Your items", text = "What you pick up goes in the bar at the bottom. Choose one with the number keys or the mouse wheel; the last two slots are for weapons.",
            picture = new[] { K("#1"), K("#2"), K("#3"), W("or"), M(Button.Wheel, "choose") } },
        ["locks"] = new Lesson { title = "Keys and locks", text = "To use an item on a lock or a socket, hold it (choose it in the bar) and use the lock. The prompt turns green when you have what it needs, red when you do not.",
            picture = new[] { K("#1", "hold it"), W("then"), K("Interact", "use") } },
        ["fight"] = new Lesson { title = "Fighting", text = "Click to slash. One slash cuts everything in its arc, so a whole school or a wall of fish can go at once. Fish blocking a way stay until you cut through them.",
            picture = new[] { M(Button.Left, "slash") } },
        ["eat"] = new Lesson { title = "Hunger", text = "Your hunger drains slowly, and each dash takes a bite of it. Fish you kill float up; the green ones are food: look at one and use it to eat.",
            picture = new[] { K("Interact", "eat") } },
        ["checkpoint"] = new Lesson { title = "Checkpoints", text = "Use a checkpoint pillar to save. If you die, everything goes back to how it was when you saved.",
            picture = new[] { K("Interact", "save") } },
        ["danger"] = new Lesson { title = "Pufferfish", text = "Pufferfish chase you and bite. Slash them or swim away. Their red, spiny nests keep sending more until you break them.",
            picture = new[] { M(Button.Left, "slash"), W("or"), K("Sprint", "dash away") } },
    };

    // ---- Showing --------------------------------------------------------------------------------------------------

    private readonly Queue<string> queue = new Queue<string>();
    private Lesson showing;
    private string showingId;
    private float openedAt;
    private float timeScaleWas = 1f;
    private bool frozenWas, lookWas, attackWas;
    private SwimController swimmer;
    private PlayerInteractor interactor;
    private PlayerInventory inventory;
    private SlashAttack slash;

    private void OnDestroy()
    {
        if (showing != null)
            Close();
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (showing != null)
        {
            if (Time.unscaledTime - openedAt > 0.5f && ContinuePressed())
                Close();
            return;
        }
        if (queue.Count > 0 && Clear())
            Open(queue.Dequeue());
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

    private static bool ContinuePressed()
    {
        Keyboard keys = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (keys != null && keys.anyKey.wasPressedThisFrame) || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame));
    }

    private void Open(string id)
    {
        if (!Lessons.TryGetValue(id, out Lesson lesson))
            return;
        showing = lesson;
        showingId = id;
        openedAt = Time.unscaledTime;
        MarkSeen(id);

        timeScaleWas = Time.timeScale;
        Time.timeScale = 0f;
        swimmer = FindFirstObjectByType<SwimController>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        slash = FindFirstObjectByType<SlashAttack>();
        if (swimmer != null)
        {
            frozenWas = swimmer.Frozen;
            lookWas = swimmer.LookLocked;
            swimmer.Frozen = true;
            swimmer.LookLocked = true;
        }
        if (interactor != null)
            interactor.Busy = true;
        if (inventory != null)
            inventory.InputBlocked = true;
        if (slash != null)
        {
            attackWas = slash.CanAttack;
            slash.SetCanAttack(false);
        }
        PauseMenu.CaptureInput(true);
    }

    private void Close()
    {
        showing = null;
        showingId = null;
        Time.timeScale = timeScaleWas <= 0f ? 1f : timeScaleWas;
        if (swimmer != null)
        {
            swimmer.Frozen = frozenWas;
            swimmer.LookLocked = lookWas;
        }
        if (inventory != null)
            inventory.InputBlocked = false;
        if (slash != null)
            slash.SetCanAttack(attackWas);
        PauseMenu.CaptureInput(false);
        if (isActiveAndEnabled)
            StartCoroutine(FreeInteract());   // the key that closed the card must not also use what is in front of you
        else if (interactor != null)
            interactor.Busy = false;
    }

    private IEnumerator FreeInteract()
    {
        yield return new WaitForSecondsRealtime(0.2f);
        if (interactor != null && showing == null)
            interactor.Busy = false;
    }

    // ---- Drawing --------------------------------------------------------------------------------------------------

    private void OnGUI()
    {
        if (showing == null || Event.current.type != EventType.Repaint)
            return;
        GUI.depth = -800;
        float s = HudStyle.Scale;
        float t = Time.unscaledTime - openedAt;
        float fade = Mathf.Clamp01(t / 0.25f);
        Color keep = GUI.color;

        GUI.color = new Color(0f, 0.02f, 0.04f, 0.6f * fade);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = keep;

        float w = 640f * s, pad = 30f * s;
        GUIStyle label = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(12f * s)));
        GUIStyle titleStyle = HudStyle.Label(Mathf.Max(12, Mathf.RoundToInt(30f * s)), TextAnchor.MiddleCenter);
        GUIStyle body = HudStyle.Label(Mathf.Max(10, Mathf.RoundToInt(18f * s)), TextAnchor.UpperCenter, true);
        GUIStyle foot = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(13f * s)), TextAnchor.MiddleCenter);
        bool hasPicture = showing.picture != null && showing.picture.Length > 0;
        float pictureH = hasPicture ? 130f * s : 0f;
        float textH = body.CalcHeight(new GUIContent(showing.text), w - pad * 2f);
        float h = pad + 22f * s + 44f * s + pictureH + 14f * s + textH + 20f * s + 30f * s + pad * 0.6f;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f + (1f - fade) * 16f * s, w, h);
        HudStyle.Panel(panel, 16f * s, fade);
        HudStyle.Outline(panel, 16f * s, new Color(HudStyle.Gold.r, HudStyle.Gold.g, HudStyle.Gold.b, 0.35f * fade));

        float y = panel.y + pad;
        float labelW = HudStyle.Spaced(Vector2.zero, "NEW", label, HudStyle.Gold, 2.4f * s, 0f, false);
        HudStyle.Spaced(new Vector2(panel.center.x - labelW * 0.5f, y), "NEW", label, HudStyle.Gold, 2.4f * s, fade);
        y += 22f * s;
        HudStyle.Write(new Rect(panel.x, y, w, 44f * s), showing.title, titleStyle, HudStyle.Text, fade);
        y += 44f * s;
        if (hasPicture)
        {
            DrawPicture(new Rect(panel.x + pad, y, w - pad * 2f, pictureH), s, fade);
            y += pictureH;
        }
        y += 14f * s;
        HudStyle.Write(new Rect(panel.x + pad, y, w - pad * 2f, textH), showing.text, body, HudStyle.Muted, fade);
        y += textH + 20f * s;
        float blink = t > 0.5f ? 0.55f + 0.45f * Mathf.Sin((t - 0.5f) * 3f) : 0f;
        HudStyle.Write(new Rect(panel.x, y, w, 30f * s), "Press any key to carry on", foot, HudStyle.Gold, fade * blink);
        GUI.color = keep;
    }

    // The glyphs in a row, centred, each with its caption under it.
    private void DrawPicture(Rect area, float s, float alpha)
    {
        float gap = 18f * s;
        var widths = new float[showing.picture.Length];
        float total = 0f;
        for (int i = 0; i < showing.picture.Length; i++)
        {
            widths[i] = Width(showing.picture[i], s);
            total += widths[i] + (i > 0 ? gap : 0f);
        }
        float x = area.center.x - total * 0.5f;
        float mid = area.y + area.height * 0.42f;
        GUIStyle caption = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(13f * s)), TextAnchor.MiddleCenter);
        for (int i = 0; i < showing.picture.Length; i++)
        {
            Glyph g = showing.picture[i];
            var box = new Rect(x, area.y, widths[i], area.height);
            Draw(g, box, mid, s, alpha);
            if (!string.IsNullOrEmpty(g.caption))
                HudStyle.Write(new Rect(box.x - 20f * s, area.yMax - 24f * s, box.width + 40f * s, 22f * s), g.caption, caption, HudStyle.Muted, alpha);
            x += widths[i] + gap;
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
        InputAction found = Action(action);
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
        InputAction move = Action("Move");
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

    private static InputAction Action(string name)
    {
        var swimmer = FindFirstObjectByType<SwimController>();
        InputActionAsset asset = swimmer != null ? swimmer.InputActions : null;
        return asset != null ? asset.FindAction("Player/" + name) : null;
    }
}
