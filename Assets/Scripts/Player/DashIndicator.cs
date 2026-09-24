using UnityEngine;

// The dash on the HUD: a thin ring round the crosshair while the dash is cooling down, filling clockwise from the top
// until it is ready, then a quick pulse as it comes back; a red flash when a dash is refused because you are too
// hungry. Nothing shows while the dash is simply ready. Drawn straight onto the screen in OnGUI like the rest of the
// game's IMGUI. Added to the player by Swim Controller.
public class DashIndicator : MonoBehaviour
{
    [Tooltip("Ring radius and dot size, in pixels at 1080p.")]
    [SerializeField] private float radius = 26f;
    [SerializeField] private float dotSize = 4f;
    [SerializeField] private Color fillColor = new Color(0.55f, 0.95f, 1f, 0.9f);
    [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.18f);
    [SerializeField] private Color readyColor = new Color(0.75f, 1f, 1f, 1f);
    [SerializeField] private Color refusedColor = new Color(1f, 0.35f, 0.3f, 0.95f);

    private const int Dots = 48;

    private SwimController swimmer;
    private static Texture2D dot;
    private float wasCooling;
    private float readyAt = -10f;

    private void Awake()
    {
        swimmer = GetComponent<SwimController>();
    }

    private void Update()
    {
        if (swimmer == null)
            return;
        float cooling = swimmer.DashReadyIn;
        if (wasCooling > 0f && cooling <= 0f)
            readyAt = Time.time;   // just came back
        wasCooling = cooling;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || swimmer == null || PauseMenu.IsOpen || Cursor.visible)
            return;
        float now = Time.time;
        float cooling = swimmer.DashReadyIn;
        float sinceReady = now - readyAt;
        float sinceRefused = now - swimmer.LastDashRefusedAt;
        if (cooling <= 0f && sinceReady > 0.35f && sinceRefused > 0.45f)
            return;

        EnsureDot();
        float scale = Screen.height / 1080f * UIScale.Hud;
        var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float r = radius * scale;
        float size = dotSize * scale;
        Color keep = GUI.color;

        if (cooling > 0f)
        {
            // The track, then what has come back so far, clockwise from the top.
            float filled = 1f - Mathf.Clamp01(cooling / Mathf.Max(0.01f, swimmer.DashCooldown));
            for (int i = 0; i < Dots; i++)
            {
                float t = (float)i / Dots;
                GUI.color = t < filled ? fillColor : trackColor;
                DrawDot(centre, r, t, size);
            }
        }
        else if (sinceReady <= 0.35f)
        {
            // Ready again: the ring pulses out and fades.
            float k = sinceReady / 0.35f;
            GUI.color = new Color(readyColor.r, readyColor.g, readyColor.b, readyColor.a * (1f - k));
            for (int i = 0; i < Dots; i++)
                DrawDot(centre, r * (1f + 0.35f * k), (float)i / Dots, size * (1f + 0.5f * (1f - k)));
        }
        if (sinceRefused <= 0.45f)
        {
            // Refused (too hungry): the ring flashes red.
            float k = sinceRefused / 0.45f;
            GUI.color = new Color(refusedColor.r, refusedColor.g, refusedColor.b, refusedColor.a * (1f - k));
            for (int i = 0; i < Dots; i++)
                DrawDot(centre, r * 1.1f, (float)i / Dots, size * 1.3f);
        }
        GUI.color = keep;
    }

    // A dot at a share of the way round, clockwise from the top.
    private static void DrawDot(Vector2 centre, float radius, float share, float size)
    {
        float angle = share * Mathf.PI * 2f - Mathf.PI * 0.5f;
        var at = new Vector2(centre.x + Mathf.Cos(angle) * radius, centre.y + Mathf.Sin(angle) * radius);
        GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), dot);
    }

    private static void EnsureDot()
    {
        if (dot != null)
            return;
        const int size = 16;
        dot = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - d) * 255f));
            }
        dot.SetPixels32(pixels);
        dot.Apply();
    }
}
