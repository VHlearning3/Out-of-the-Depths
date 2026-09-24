using UnityEngine;

// The dash on the HUD: a thin smooth ring round the crosshair while the dash is cooling down, filling clockwise from
// the top until it is ready, then a quick pulse outwards as it comes back; a coral flash when a dash is refused because
// you are too hungry. Nothing shows while the dash is simply ready. Drawn straight onto the screen in OnGUI with GL
// (HudStyle arcs), like the rest of the game's IMGUI. Added to the player by Swim Controller.
public class DashIndicator : MonoBehaviour
{
    [Tooltip("Ring radius and thickness, in pixels at 1080p (times the HUD size).")]
    [SerializeField] private float radius = 20f;
    [SerializeField] private float thickness = 2.2f;
    [SerializeField] private Color fillColor = new Color(0.45f, 0.9f, 0.86f, 0.9f);
    [SerializeField] private Color trackColor = new Color(1f, 1f, 1f, 0.14f);
    [SerializeField] private Color readyColor = new Color(0.75f, 1f, 0.97f, 1f);
    [SerializeField] private Color refusedColor = new Color(1f, 0.42f, 0.36f, 0.95f);

    private SwimController swimmer;
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

        float scale = HudStyle.Scale;
        var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        float r = radius * scale;
        float width = Mathf.Max(1.5f, thickness * scale);
        if (!HudStyle.BeginShapes())
            return;

        if (cooling > 0f)
        {
            // The track, then what has come back so far, clockwise from the top.
            float filled = 1f - Mathf.Clamp01(cooling / Mathf.Max(0.01f, swimmer.DashCooldown));
            HudStyle.Arc(centre, r, width, 0f, 360f, trackColor);
            HudStyle.Arc(centre, r, width, 0f, 360f * filled, fillColor, 6f);
        }
        else if (sinceReady <= 0.35f)
        {
            // Ready again: the ring pulses out and fades.
            float k = sinceReady / 0.35f;
            HudStyle.Arc(centre, r * (1f + 0.35f * k), width * (1f + (1f - k)), 0f, 360f, new Color(readyColor.r, readyColor.g, readyColor.b, readyColor.a * (1f - k)));
        }
        if (sinceRefused <= 0.45f)
        {
            // Refused (too hungry): the ring flashes coral.
            float k = sinceRefused / 0.45f;
            HudStyle.Arc(centre, r * 1.1f, width * 1.3f, 0f, 360f, new Color(refusedColor.r, refusedColor.g, refusedColor.b, refusedColor.a * (1f - k)));
        }
        HudStyle.EndShapes();
    }
}
