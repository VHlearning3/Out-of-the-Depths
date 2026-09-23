using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The game's mouse cursor, everywhere the cursor shows (menus, the pause menu, puzzle boards, the inspect view, the
// death screen): Assets/Resources/Cursors/CursorTheme.asset says which picture and where it points, and it is set as
// the game starts and again as each scene loads, so no scene needs anything for it. Every left click lets a few
// bubbles go where it clicked: they wobble up, grow a little and pop, over a small ripple (drawn over every menu,
// only while the cursor is on screen, in real time so the pause menu has them too). To use other art, put the new
// cursor on the asset (Texture Type: Cursor in its import settings, which also keeps it readable and uncompressed)
// and set Hotspot to the pixel it points with, from the top-left corner; Bubble is the picture each bubble is drawn
// with. Changes on the asset show at once while playing. The bone key tying board hides the cursor (and so has no
// bubbles) and draws its strand instead.
[CreateAssetMenu(menuName = "Out of the Depths/Cursor Theme", fileName = "CursorTheme")]
public class CursorTheme : ScriptableObject
{
    [Header("Cursor")]
    [Tooltip("Off = the system arrow.")]
    public bool useCustomCursor = true;
    [Tooltip("The cursor picture (32 x 32 reads best). Texture Type must be Cursor.")]
    public Texture2D cursor;
    [Tooltip("The pixel it points with, from the top-left corner.")]
    public Vector2 hotspot = new Vector2(2f, 2f);
    [Tooltip("Used instead on screens at least Large From Height tall, so the cursor is not tiny at 1440p or 4K. Empty = Cursor on every screen.")]
    public Texture2D largeCursor;
    public Vector2 largeHotspot = new Vector2(2f, 2f);
    [Min(1)] public int largeFromHeight = 1400;

    [Header("Click bubbles")]
    [Tooltip("Bubbles where you click, while the cursor is on screen.")]
    public bool clickBubbles = true;
    [Tooltip("The picture each bubble is drawn with.")]
    public Texture2D bubble;
    [Range(1, 12)] public int bubblesPerClick = 5;
    [Tooltip("Smallest and largest bubble, in pixels across on a 1080p screen (scaled with the screen).")]
    public Vector2 bubbleSize = new Vector2(7f, 16f);
    [Tooltip("How far they rise before they pop, in pixels on a 1080p screen.")]
    public float bubbleRise = 80f;
    [Tooltip("Shortest and longest a bubble lasts, in seconds.")]
    public Vector2 bubbleLifetime = new Vector2(0.7f, 1.1f);
    [Tooltip("A faint ring spreading out where the click landed.")]
    public bool ripple = true;

    private static CursorTheme current;
    private static bool looked;

    // The theme in Resources, or null if there is none (then the system arrow stays and there are no bubbles).
    public static CursorTheme Current
    {
        get
        {
            if (current == null && !looked)
            {
                current = Resources.Load<CursorTheme>("Cursors/CursorTheme");
                looked = true;
            }
            return current;
        }
    }

    // Sets the cursor from the theme (the large one on tall screens), or back to the system arrow.
    public static void Apply()
    {
        CursorTheme theme = Current;
        if (theme == null || !theme.useCustomCursor || theme.cursor == null)
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            return;
        }
        bool large = theme.largeCursor != null && Screen.height >= theme.largeFromHeight;
        Cursor.SetCursor(large ? theme.largeCursor : theme.cursor, large ? theme.largeHotspot : theme.hotspot, CursorMode.Auto);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyAsScenesLoad()
    {
        current = null;
        looked = false;
        Apply();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        var overlay = new GameObject("ClickBubbles") { hideFlags = HideFlags.HideAndDontSave };
        DontDestroyOnLoad(overlay);
        overlay.AddComponent<ClickBubbles>();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

    // Edited in the Inspector while playing: show the change straight away.
    private void OnValidate()
    {
        if (Application.isPlaying && this == current)
            Apply();
    }

    // The bubbles: an IMGUI overlay above everything (uGUI menus and the IMGUI pause menu alike). The click is read
    // from the GUI event, so it lands exactly where the cursor is even in a scaled Game view; the event is left for
    // the menus underneath.
    private class ClickBubbles : MonoBehaviour
    {
        private struct Bubble
        {
            public Vector2 at;
            public float born, life, size, drift, phase;
        }

        private readonly List<Bubble> bubbles = new List<Bubble>();
        private readonly List<Vector3> ripples = new List<Vector3>();   // x, y = where, z = when

        private const float RippleSeconds = 0.4f;

        private void OnGUI()
        {
            CursorTheme theme = Current;
            if (theme == null || !theme.clickBubbles || theme.bubble == null)
                return;
            GUI.depth = -10000;   // drawn last, over every other OnGUI
            Event e = Event.current;
            float now = Time.unscaledTime;
            if (e.type == EventType.MouseDown && e.button == 0 && Cursor.visible && Cursor.lockState != CursorLockMode.Locked)
                Burst(theme, e.mousePosition, now);
            if (e.type != EventType.Repaint || (bubbles.Count == 0 && ripples.Count == 0))
                return;

            float s = Screen.height / 1080f;
            Color keep = GUI.color;
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                float t = (now - ripples[i].z) / RippleSeconds;
                if (t >= 1f)
                {
                    ripples.RemoveAt(i);
                    continue;
                }
                float eased = 1f - (1f - t) * (1f - t);
                float size = Mathf.Lerp(10f, 46f, eased) * s;
                GUI.color = new Color(1f, 1f, 1f, 0.45f * (1f - t));
                GUI.DrawTexture(new Rect(ripples[i].x - size * 0.5f, ripples[i].y - size * 0.5f, size, size), theme.bubble);
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                Bubble b = bubbles[i];
                float t = (now - b.born) / b.life;
                if (t >= 1f)
                {
                    bubbles.RemoveAt(i);
                    continue;
                }
                if (t < 0f)
                    continue;   // not let go yet (they leave one after another)
                float rise = 1f - (1f - t) * (1f - t);   // quick off the mark, slowing as it goes
                Vector2 p = b.at + new Vector2(Mathf.Sin(b.phase + t * 7f) * b.drift * s, -theme.bubbleRise * rise * s);
                float grow = Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(t * 5f));
                float popping = Mathf.Clamp01((t - 0.82f) / 0.18f);   // the last bit: swells and fades as it pops
                float size = b.size * s * grow * (1f + 0.45f * popping);
                GUI.color = new Color(1f, 1f, 1f, 1f - popping);
                GUI.DrawTexture(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), theme.bubble);
            }
            GUI.color = keep;
        }

        // A handful of bubbles at the click, the biggest first, the rest just after it and a little to the sides.
        private void Burst(CursorTheme theme, Vector2 at, float now)
        {
            if (theme.ripple)
                ripples.Add(new Vector3(at.x, at.y, now));
            float s = Screen.height / 1080f;
            int count = Mathf.Max(1, theme.bubblesPerClick);
            for (int i = 0; i < count; i++)
            {
                float sizeT = i == 0 ? 1f : Random.Range(0f, 0.75f);
                bubbles.Add(new Bubble
                {
                    at = at + (i == 0 ? Vector2.zero : new Vector2(Random.Range(-12f, 12f), Random.Range(-4f, 8f)) * s),
                    born = now + i * 0.045f,
                    life = Random.Range(theme.bubbleLifetime.x, Mathf.Max(theme.bubbleLifetime.x, theme.bubbleLifetime.y)),
                    size = Mathf.Lerp(theme.bubbleSize.x, theme.bubbleSize.y, sizeT),
                    drift = Random.Range(3f, 8f),
                    phase = Random.Range(0f, Mathf.PI * 2f),
                });
            }
            if (bubbles.Count > 120)
                bubbles.RemoveRange(0, bubbles.Count - 120);
        }
    }
}
