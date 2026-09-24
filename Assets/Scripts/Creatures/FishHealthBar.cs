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
    private Texture2D rounded;
    private GUIStyle nameStyle;
    private GUIStyle valueStyle;
    private GUIStyle frameStyle;

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

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || alpha <= 0f || target == null)
            return;
        EnsureDrawing();
        float scale = Screen.height / 1080f * UIScale.Hud;
        float width = size.x * scale, height = Mathf.Max(4f, size.y * scale);
        var bar = new Rect((Screen.width - width) * 0.5f, top * scale, width, height);
        Color keep = GUI.color;

        // The name above the bar on the left, the health on the right.
        nameStyle.fontSize = Mathf.RoundToInt(20f * scale);
        valueStyle.fontSize = Mathf.RoundToInt(15f * scale);
        var label = new Rect(bar.x, bar.y - 28f * scale, bar.width, 24f * scale);
        GUI.color = new Color(0f, 0f, 0f, 0.6f * alpha);
        GUI.Label(new Rect(label.x + 2f, label.y + 2f, label.width, label.height), target.Name, nameStyle);
        GUI.color = new Color(nameColor.r, nameColor.g, nameColor.b, alpha);
        GUI.Label(label, target.Name, nameStyle);
        GUI.color = new Color(nameColor.r, nameColor.g, nameColor.b, 0.75f * alpha);
        GUI.Label(label, Mathf.CeilToInt(target.Health.CurrentHealth) + " / " + Mathf.CeilToInt(target.Health.MaxHealth), valueStyle);

        // The frame, the lost chunk, the fill.
        float pad = Mathf.Max(2f, 3f * scale);
        var frame = new Rect(bar.x - pad, bar.y - pad, bar.width + pad * 2f, bar.height + pad * 2f);
        GUI.color = new Color(frameColor.r, frameColor.g, frameColor.b, frameColor.a * alpha);
        GUI.Box(frame, GUIContent.none, frameStyle);   // 9-sliced: the corners stay round on a long thin bar
        if (trailing > shown)
        {
            GUI.color = new Color(lostColor.r, lostColor.g, lostColor.b, lostColor.a * alpha);
            GUI.DrawTexture(new Rect(bar.x + bar.width * shown, bar.y, bar.width * (trailing - shown), bar.height), Texture2D.whiteTexture);
        }
        if (shown > 0f)
        {
            Color fill = Color.Lerp(lowColor, fullColor, shown);
            GUI.color = new Color(fill.r, fill.g, fill.b, alpha);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * shown, bar.height), Texture2D.whiteTexture);
        }
        GUI.color = keep;
    }

    private void EnsureDrawing()
    {
        if (rounded == null)
        {
            const int size = 32;
            const float radius = 8f;
            rounded = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
                }
            rounded.SetPixels32(pixels);
            rounded.Apply();
            frameStyle = new GUIStyle { border = new RectOffset(9, 9, 9, 9) };
            frameStyle.normal.background = rounded;
        }
        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerLeft, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow, font = GameFont.Font };
            nameStyle.normal.textColor = Color.white;
            valueStyle = new GUIStyle(nameStyle) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Normal };
        }
    }
}
