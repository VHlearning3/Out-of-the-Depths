using UnityEngine;

// A boss-style health bar for the fish you are looking at: while the crosshair is on a fish (within Range, nothing
// solid in the way), a bar across the top of the screen shows its name and health. The fill goes from green to red as
// it runs low; after a hit a pale chunk shows what was just lost and drains away; it fades in when you look at a fish
// and out a moment after you look away (or it dies). One for the whole game, made by the first fish that wakes
// (Fish Health Display); drawn straight onto the screen in OnGUI like the rest of the game's IMGUI.
public class FishHealthBar : MonoBehaviour
{
    private static FishHealthBar instance;

    [Tooltip("How far the crosshair reaches for a fish, in metres.")]
    [SerializeField] private float range = 18f;
    [Tooltip("How forgiving the aim is: the radius of the look, in metres.")]
    [SerializeField] private float aimRadius = 0.35f;
    [Tooltip("Seconds it stays after you look away.")]
    [SerializeField] private float linger = 0.6f;
    [Tooltip("Bar size in pixels at 1080p, and how far down from the top of the screen it sits.")]
    [SerializeField] private Vector2 size = new Vector2(520f, 12f);
    [SerializeField] private float top = 70f;
    [SerializeField] private Color fullColor = new Color(0.45f, 1f, 0.6f);
    [SerializeField] private Color lowColor = new Color(1f, 0.4f, 0.3f);
    [SerializeField] private Color lostColor = new Color(1f, 0.95f, 0.85f, 0.85f);
    [SerializeField] private Color frameColor = new Color(0f, 0.02f, 0.05f, 0.75f);
    [SerializeField] private Color nameColor = new Color(0.93f, 0.96f, 1f);

    private readonly RaycastHit[] hits = new RaycastHit[24];
    private FishHealthDisplay target;
    private float seenAt = -100f;
    private float shown;        // the fill as drawn, easing to the real health
    private float trailing;     // the pale chunk behind it, draining after a hit
    private float alpha;

    public static void Ensure()
    {
        if (instance != null)
            return;
        var host = new GameObject("FishHealthBar") { hideFlags = HideFlags.HideInHierarchy };
        DontDestroyOnLoad(host);
        instance = host.AddComponent<FishHealthBar>();
    }

    private void Update()
    {
        FishHealthDisplay looked = LookedAt();
        if (looked != null)
        {
            if (looked != target)
            {
                target = looked;
                shown = trailing = Share(target);
            }
            seenAt = Time.time;
        }
        bool showing = target != null && Time.time - seenAt <= linger && !PauseMenu.IsOpen;
        alpha = Mathf.MoveTowards(alpha, showing ? 1f : 0f, Time.unscaledDeltaTime / (showing ? 0.12f : 0.35f));
        if (target == null)
            return;
        float share = Share(target);
        shown = Mathf.MoveTowards(shown, share, Time.deltaTime * 3f);
        if (Time.time - target.LastHitAt > 0.35f)   // the lost chunk waits a moment, then drains
            trailing = Mathf.MoveTowards(trailing, shown, Time.deltaTime * 0.8f);
        trailing = Mathf.Max(trailing, shown);
        if (alpha <= 0f && !showing)
            target = null;
    }

    private static float Share(FishHealthDisplay fish) => fish.Health.MaxHealth > 0f ? Mathf.Clamp01(fish.Health.CurrentHealth / fish.Health.MaxHealth) : 0f;

    // The fish under the crosshair: the nearest one along the look, unless something solid comes first.
    private FishHealthDisplay LookedAt()
    {
        Camera view = Camera.main;
        if (view == null)
            return null;
        Ray look = new Ray(view.transform.position, view.transform.forward);
        int count = Physics.SphereCastNonAlloc(look, aimRadius, hits, range, ~0, QueryTriggerInteraction.Collide);
        FishHealthDisplay best = null;
        float bestDistance = float.MaxValue;
        float wall = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i].collider;
            if (PlayerBody.Is(hit))
                continue;
            FishHealthDisplay fish = hit.GetComponentInParent<FishHealthDisplay>();
            if (fish != null)
            {
                if ((fish.Alive || Time.time - fish.LastHitAt < 1f) && hits[i].distance < bestDistance)
                {
                    best = fish;
                    bestDistance = hits[i].distance;
                }
            }
            else if (!hit.isTrigger && hits[i].distance < wall && hits[i].distance > 0f)
            {
                wall = hits[i].distance;
            }
        }
        return best != null && bestDistance <= wall ? best : null;
    }

    // In the HUD style (HudStyle): the name on the left above a slim rounded bar, the health on the right, a dark pill
    // with a hairline rim behind the fill and the pale chunk. Under the chase's objective card when that is up.
    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || alpha <= 0f || target == null)
            return;
        float scale = HudStyle.Scale;
        float width = size.x * 0.7f * scale, height = Mathf.Max(5f, size.y * 0.7f * scale);
        float y = Mathf.Max(top * scale + 30f * scale, ChaseGuide.CardBottom + 40f * scale);
        var bar = new Rect((Screen.width - width) * 0.5f, y, width, height);

        // The name above the bar on the left, the health on the right.
        GUIStyle nameStyle = HudStyle.Label(Mathf.Max(9, Mathf.RoundToInt(16f * scale)), TextAnchor.LowerLeft);
        GUIStyle valueStyle = HudStyle.Label(Mathf.Max(8, Mathf.RoundToInt(12f * scale)), TextAnchor.LowerRight);
        var label = new Rect(bar.x, bar.y - 26f * scale, bar.width, 22f * scale);
        HudStyle.Write(label, target.Name, nameStyle, nameColor, alpha);
        HudStyle.Write(label, Mathf.CeilToInt(target.Health.CurrentHealth) + " / " + Mathf.CeilToInt(target.Health.MaxHealth), valueStyle, HudStyle.Muted, alpha);

        // The pill, the lost chunk, the fill.
        float pad = Mathf.Max(2f, 2.5f * scale);
        var frame = new Rect(bar.x - pad, bar.y - pad, bar.width + pad * 2f, bar.height + pad * 2f);
        HudStyle.Panel(frame, frame.height * 0.5f, new Color(frameColor.r, frameColor.g, frameColor.b, Mathf.Max(frameColor.a, 0.72f)), HudStyle.Rim, alpha);
        float radius = bar.height * 0.5f;
        if (trailing > shown)
            HudStyle.Fill(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * trailing), bar.height), radius, new Color(lostColor.r, lostColor.g, lostColor.b, lostColor.a * alpha * 0.8f));
        if (shown > 0f)
        {
            Color fill = Color.Lerp(lowColor, fullColor, shown);
            HudStyle.Fill(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * shown), bar.height), radius, new Color(fill.r, fill.g, fill.b, alpha));
        }
    }
}
