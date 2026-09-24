using System.Collections.Generic;
using UnityEngine;

// Which way things are, kept quiet so it never shouts over the water:
//  - Hunters: a soft coral arc on a ring round the crosshair points at each fish that is coming for you (a chasing
//    pufferfish, the chase pack), stronger the closer it is, fading once it is in plain view in front of you, with a
//    tiny chevron on its outside. The ring's top is straight ahead, its bottom behind you.
//  - Bites: a brighter, wider arc flashes toward whatever just bit you and fades (DamageManager.HitFrom).
//  - The goal: while a script points at something (Point; the chase's Chase Guide does), a small gold diamond sits on
//    it with the distance under it, easing off as you look straight at it or get close; off screen or behind you, a
//    small gold chevron on a ring round the middle of the screen points the way, with the distance.
// Made as the game starts, one for the whole game (it survives scene loads); drawn in OnGUI with GL
// (HudStyle), hidden while the pause menu, a puzzle board, the inspect view or the chase's reveal is up.
public class DirectionIndicators : MonoBehaviour
{
    // Off = none of it (hunters, bites or the goal). The Settings page's "Direction indicators" switch; remembered.
    public const string PrefsKey = "settings.directionIndicators";
    private static bool? setting;
    public static bool Enabled
    {
        get
        {
            setting ??= PlayerPrefs.GetInt(PrefsKey, 1) == 1;
            return setting.Value;
        }
        set
        {
            if (setting == value)
                return;
            setting = value;
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
        }
    }

    [Header("Hunters")]
    [Tooltip("Hunters further than this (metres) are not shown.")]
    [SerializeField] private float threatRange = 16f;
    [Tooltip("The ring round the crosshair: radius and arc thickness, pixels at 1080p (times the HUD size).")]
    [SerializeField] private float ringRadius = 64f;
    [SerializeField] private float arcThickness = 3.5f;
    [Tooltip("Degrees each hunter's arc covers.")]
    [SerializeField] private float arcSpan = 36f;
    [SerializeField] private Color threatColor = new Color(1f, 0.42f, 0.36f);
    [Tooltip("How strong a hunter's arc is at the edge of the range and right on you.")]
    [SerializeField] private Vector2 threatAlpha = new Vector2(0.16f, 0.72f);

    [Header("Bites")]
    [SerializeField] private float hitSeconds = 0.9f;
    [SerializeField] private float hitSpan = 54f;

    [Header("Goal")]
    [SerializeField] private Color goalColor = new Color(1f, 0.8f, 0.42f);
    [Tooltip("The off-screen chevron rides on an ellipse this share of the screen out from the middle.")]
    [SerializeField, Range(0.1f, 0.48f)] private float goalRing = 0.3f;
    [Tooltip("Closer than this (metres) the goal marker fades away: you are there.")]
    [SerializeField] private float arriveDistance = 2.5f;

    private static DirectionIndicators instance;

    private class Threat
    {
        public float angle;     // degrees clockwise from straight ahead
        public float alpha;
        public bool seen;       // still a threat this frame
    }

    private struct Hit
    {
        public Vector3 from;
        public float at;
    }

    private readonly Dictionary<Transform, Threat> threats = new Dictionary<Transform, Threat>();
    private readonly List<Transform> stale = new List<Transform>();
    private readonly List<Hit> hits = new List<Hit>();

    private Object goalOwner;
    private Vector3 goal;
    private float goalSetAt = -10f;
    private float goalShown;
    private Camera eye;

    private static DirectionIndicators Ensure()
    {
        if (instance == null)
        {
            var host = new GameObject("DirectionIndicators") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(host);
            instance = host.AddComponent<DirectionIndicators>();
        }
        return instance;
    }

    // Point the goal marker at `world` for `owner` (call it every frame while it applies; it lapses a moment after
    // the calls stop).
    public static void Point(Object owner, Vector3 world)
    {
        DirectionIndicators it = Ensure();
        it.goalOwner = owner;
        it.goal = world;
        it.goalSetAt = Time.unscaledTime;
    }

    // Take the goal marker down, if `owner` put it up.
    public static void Clear(Object owner)
    {
        if (instance != null && instance.goalOwner == owner)
            instance.goalOwner = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Listen()
    {
        DamageManager.HitFrom -= OnHitFrom;
        DamageManager.HitFrom += OnHitFrom;
        Ensure();   // one for the whole game, from the start, so the hunters show as soon as one comes
    }

    private static void OnHitFrom(Vector3 from)
    {
        DirectionIndicators it = Ensure();
        it.hits.Add(new Hit { from = from, at = Time.time });
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (eye == null)
            eye = Camera.main;
        if (eye == null)
            return;

        // Hunters: angle round the ring and how strong, eased so they glide rather than jump.
        float fade = 1f - Mathf.Exp(-8f * Time.deltaTime);
        foreach (Threat threat in threats.Values)
            threat.seen = false;
        foreach (FishAggression fish in FishAggression.Chasing)
            if (fish != null && fish.isActiveAndEnabled)
                Track(fish.transform);
        ChaseSequence chase = ChaseSequence.Active;
        if (chase != null && chase.IsRunning)
        {
            IReadOnlyList<ChasePufferfish> pack = ChasePufferfish.Active;
            for (int i = 0; i < pack.Count; i++)
                if (pack[i] != null && pack[i].IsHunting)
                    Track(pack[i].transform);
        }
        ChaseSwarm swarm = ChaseSwarm.Active;
        if (swarm != null && swarm.IsHunting)
            Track(swarm.transform);
        stale.Clear();
        foreach (KeyValuePair<Transform, Threat> pair in threats)
        {
            if (!pair.Value.seen || pair.Key == null)
            {
                pair.Value.alpha = Mathf.MoveTowards(pair.Value.alpha, 0f, Time.deltaTime * 2f);
                if (pair.Value.alpha <= 0f || pair.Key == null)
                    stale.Add(pair.Key);
            }
        }
        foreach (Transform gone in stale)
            threats.Remove(gone);

        for (int i = hits.Count - 1; i >= 0; i--)
            if (Time.time - hits[i].at > hitSeconds)
                hits.RemoveAt(i);

        bool pointing = goalOwner != null && Time.unscaledTime - goalSetAt < 0.25f;
        goalShown = Mathf.MoveTowards(goalShown, pointing ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);

        void Track(Transform fish)
        {
            Vector3 local = eye.transform.InverseTransformPoint(fish.position);
            float distance = local.magnitude;
            if (distance > threatRange)
                return;
            if (!threats.TryGetValue(fish, out Threat threat))
            {
                threat = new Threat { angle = Angle(local) };
                threats[fish] = threat;
            }
            threat.seen = true;
            threat.angle = Mathf.LerpAngle(threat.angle, Angle(local), fade * 1.5f);
            float near = 1f - Mathf.Clamp01((distance - 2f) / Mathf.Max(0.1f, threatRange - 2f));
            float alpha = Mathf.Lerp(threatAlpha.x, threatAlpha.y, near * near);
            // In plain view in front of you: you can see it, so the arc steps back.
            Vector3 view = eye.WorldToViewportPoint(fish.position);
            if (view.z > 0f && view.x > 0.15f && view.x < 0.85f && view.y > 0.15f && view.y < 0.85f)
                alpha *= 0.3f;
            threat.alpha = Mathf.Lerp(threat.alpha, alpha, fade);
        }
    }

    // Degrees clockwise from straight ahead, from a point in camera space (only the heading counts: up/down does not).
    private static float Angle(Vector3 local) => Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;

    private void OnGUI()
    {
        if (!Enabled || Event.current.type != EventType.Repaint || eye == null)
            return;
        ChaseSequence chase = ChaseSequence.Active;
        if (PauseMenu.IsOpen || PuzzleBoard.IsOpen || Cursor.visible || (chase != null && chase.InCutscene))
            return;
        bool anyThreat = threats.Count > 0 || hits.Count > 0;
        if (!anyThreat && goalShown <= 0f)
            return;

        float scale = HudStyle.Scale;
        var centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        string distanceText = null;
        Rect distanceRect = default;
        float distanceAlpha = 0f;

        if (!HudStyle.BeginShapes())
            return;
        float radius = ringRadius * scale;
        float width = Mathf.Max(1.5f, arcThickness * scale);
        foreach (Threat threat in threats.Values)
        {
            if (threat.alpha <= 0.01f)
                continue;
            Color c = new Color(threatColor.r, threatColor.g, threatColor.b, threat.alpha);
            HudStyle.Arc(centre, radius, width, threat.angle - arcSpan * 0.5f, threat.angle + arcSpan * 0.5f, c, arcSpan * 0.35f);
            HudStyle.Chevron(HudStyle.OnCircle(centre, radius + 9f * scale, threat.angle), threat.angle, 8f * scale, new Color(c.r, c.g, c.b, Mathf.Min(1f, threat.alpha * 1.15f)));
        }
        foreach (Hit hit in hits)
        {
            float t = (Time.time - hit.at) / hitSeconds;
            float angle = Angle(eye.transform.InverseTransformPoint(hit.from));
            float a = (1f - t) * (1f - t) * 0.85f;
            float grow = 1f + 0.12f * t;
            HudStyle.Arc(centre, radius * grow, width * 1.8f, angle - hitSpan * 0.5f, angle + hitSpan * 0.5f, new Color(threatColor.r, threatColor.g, threatColor.b, a), hitSpan * 0.3f);
        }

        if (goalShown > 0f)
            DrawGoal(centre, scale, ref distanceText, ref distanceRect, ref distanceAlpha);
        HudStyle.EndShapes();

        if (distanceText != null)
            HudStyle.Write(distanceRect, distanceText, HudStyle.Label(Mathf.Max(9, Mathf.RoundToInt(13f * scale)), TextAnchor.MiddleCenter), HudStyle.Text, distanceAlpha);
    }

    // The goal: a diamond on it when it is on screen, else a chevron round the middle pointing the way.
    private void DrawGoal(Vector2 centre, float scale, ref string text, ref Rect textRect, ref float textAlpha)
    {
        Vector3 screen = eye.WorldToScreenPoint(goal);
        float metres = Vector3.Distance(eye.transform.position, goal);
        float here = Mathf.Clamp01((metres - arriveDistance) / 2f);   // fades out as you arrive
        var at = new Vector2(screen.x, Screen.height - screen.y);
        float margin = Screen.height * 0.08f;
        bool onScreen = screen.z > 0f && at.x > margin && at.x < Screen.width - margin && at.y > margin && at.y < Screen.height - margin;
        float alpha = goalShown * here;
        if (alpha <= 0.01f)
            return;
        text = Mathf.RoundToInt(metres) + " m";

        if (onScreen)
        {
            // Looking straight at it: it steps back so it never covers what you are aiming at.
            float fromMiddle = (at - centre).magnitude;
            alpha *= Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01((fromMiddle - 40f * scale) / (160f * scale)));
            float r = 8f * scale;
            Color gold = new Color(goalColor.r, goalColor.g, goalColor.b, alpha);
            HudStyle.Diamond(at + new Vector2(0f, Mathf.Max(1f, 1.2f * scale)), r, 1.6f * scale, new Color(0f, 0f, 0f, 0.35f * alpha), Color.clear);   // shadow
            HudStyle.Diamond(at, r, Mathf.Max(1f, 1.6f * scale), gold, new Color(goalColor.r, goalColor.g, goalColor.b, 0.18f * alpha));
            float d = 2.4f * scale;
            HudStyle.Diamond(at, d, 0.1f, Color.clear, gold);   // the dot in the middle
            textRect = new Rect(at.x - 60f * scale, at.y + r + 4f * scale, 120f * scale, 18f * scale);
            textAlpha = alpha;
            return;
        }

        // Off screen or behind: which way on the screen (flipped when behind), on an ellipse round the middle.
        Vector2 direction = at - centre;
        if (screen.z < 0f)
            direction = -direction;
        if (direction.sqrMagnitude < 1f)
            direction = Vector2.down;
        direction.Normalize();
        float degrees = Mathf.Atan2(direction.x, -direction.y) * Mathf.Rad2Deg;
        var edge = new Vector2(centre.x + direction.x * Screen.width * goalRing, centre.y + direction.y * Screen.height * goalRing);
        alpha *= 0.85f;
        HudStyle.Chevron(edge, degrees, 16f * scale, new Color(goalColor.r, goalColor.g, goalColor.b, alpha));
        Vector2 inward = edge - direction * 22f * scale;
        textRect = new Rect(inward.x - 60f * scale, inward.y - 9f * scale, 120f * scale, 18f * scale);
        textAlpha = alpha * 0.9f;
    }
}
