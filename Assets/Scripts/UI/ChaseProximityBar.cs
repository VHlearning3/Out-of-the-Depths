using UnityEngine;

// How close the chase is, at the very top of the screen while it runs: a slim panel with SWARM on the left, a track
// that fills as they close in (sea glass while there is room, turning coral, then pulsing and glowing when they are
// nearly on you) and how many metres behind they are on the right. Empty = Far Distance or more away, full = on you.
// Drawn in code in the HUD's own style (Hud Style), scaled by the HUD size; the objective card sits under it. Chase
// Sequence adds one to itself.
[RequireComponent(typeof(ChaseSequence))]
public class ChaseProximityBar : MonoBehaviour
{
    [Tooltip("At this distance (metres) or more the bar is empty; it is full when they reach you.")]
    [SerializeField] private float farDistance = 25f;
    [Tooltip("Width of the whole bar at 1080p, in pixels.")]
    [SerializeField] private float width = 440f;
    [SerializeField] private string label = "SWARM";

    // The bottom of the bar on screen (GUI pixels) while it shows, else 0, so the objective card can sit under it.
    public static float Bottom { get; private set; }

    private ChaseSequence chase;
    private float shown;
    private float fill;        // eased toward the real closeness, so the bar glides
    private float metres;

    private void Awake()
    {
        chase = GetComponent<ChaseSequence>();
    }

    private void OnDisable() => Bottom = 0f;

    private void Update()
    {
        bool running = chase != null && chase.IsRunning && !chase.InCutscene;
        shown = Mathf.MoveTowards(shown, running ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);
        if (!running)
            return;
        float distance = chase.NearestDistance;
        metres = float.IsPositiveInfinity(distance) ? farDistance : distance;
        float closeness = 1f - Mathf.Clamp01(metres / Mathf.Max(0.1f, farDistance));
        fill = Mathf.Lerp(fill, closeness, 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime));
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;
        if (shown <= 0f || PauseMenu.IsOpen || PuzzleBoard.IsOpen)
        {
            Bottom = 0f;
            return;
        }
        float s = HudStyle.Scale;
        float alpha = shown;
        float w = width * s, h = 34f * s, pad = 14f * s;
        var panel = new Rect((Screen.width - w) * 0.5f, 18f * s - (1f - shown) * 10f * s, w, h);
        Bottom = panel.yMax;

        // Sea glass while there is room, coral as they close in; the last stretch pulses.
        float danger = Mathf.Clamp01(fill);
        Color colour = Color.Lerp(HudStyle.Accent, HudStyle.Danger, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, danger)));
        float pulse = danger > 0.75f ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(6f, 14f, (danger - 0.75f) / 0.25f)) : 0f;

        HudStyle.Panel(panel, h * 0.5f, alpha);
        if (pulse > 0f)
            HudStyle.Outline(panel, h * 0.5f, new Color(colour.r, colour.g, colour.b, 0.6f * pulse * alpha));

        // SWARM on the left, the distance on the right, the track between them.
        GUIStyle small = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(11f * s)));
        GUIStyle number = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(15f * s)), TextAnchor.MiddleRight);
        float labelWidth = HudStyle.Spaced(new Vector2(panel.x + pad, panel.center.y - small.fontSize * 0.8f), label, small, colour, 2.2f * s, alpha);
        string distance = metres >= farDistance ? farDistance.ToString("0") + "+ m" : metres.ToString("0") + " m";
        float numberWidth = 54f * s;
        HudStyle.Write(new Rect(panel.xMax - pad - numberWidth, panel.y, numberWidth, h), distance, number, HudStyle.Text, alpha);

        float trackX = panel.x + pad + labelWidth + 12f * s;
        float trackW = panel.xMax - pad - numberWidth - 10f * s - trackX;
        float trackH = 8f * s;
        var track = new Rect(trackX, panel.center.y - trackH * 0.5f, Mathf.Max(10f, trackW), trackH);
        HudStyle.Fill(track, trackH * 0.5f, new Color(1f, 1f, 1f, 0.1f * alpha));
        if (danger > 0.01f)
        {
            var bar = new Rect(track.x, track.y, Mathf.Max(trackH, track.width * danger), trackH);
            HudStyle.Fill(bar, trackH * 0.5f, new Color(colour.r, colour.g, colour.b, (0.85f + 0.15f * pulse) * alpha));
            if (HudStyle.BeginShapes())   // a bright bead at the front of the fill: where they are
            {
                HudStyle.Arc(new Vector2(bar.xMax - trackH * 0.5f, bar.center.y), 0f, trackH * (0.75f + 0.25f * pulse), 0f, 360f,
                    new Color(Mathf.Lerp(colour.r, 1f, 0.5f), Mathf.Lerp(colour.g, 1f, 0.5f), Mathf.Lerp(colour.b, 1f, 0.5f), alpha));
                HudStyle.EndShapes();
            }
        }
    }
}
