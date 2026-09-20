using UnityEngine;
using UnityEngine.UI;

// The chase danger HUD: a soft red vignette that creeps in from the edges of the screen (the middle stays clear) and
// beats like a heart, faster and harder the closer the pack is - plus a small chevron at the edge of the screen that
// points at the nearest hunter whenever it is off screen. Reads the running Chase Sequence.
// Lives on a stretched RectTransform under the HUD canvas (Tools → Out of the Depths → Add Chase Danger HUD To Open
// Scene). The vignette and chevron sprites are generated at start when none are set, so it needs no art.
public class ChaseDangerUI : MonoBehaviour
{
    [Header("Vignette")]
    [Tooltip("Soft darkening at the edges of the screen. Empty = a smooth radial one is generated at start.")]
    [SerializeField] private Sprite vignetteSprite;
    [SerializeField] private Color color = new Color(0.55f, 0.02f, 0.02f);
    [Tooltip("Vignette opacity at full danger, at the very edge of the screen.")]
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.28f;
    [Tooltip("How far the vignette reaches in from the edges: barely a rim when the pack is far, this much of the screen when it's on you.")]
    [SerializeField, Range(0f, 1f)] private float reach = 0.45f;

    [Header("Heartbeat")]
    [Tooltip("Beats per second when the pack is far away / right behind you.")]
    [SerializeField] private float slowBeat = 0.8f;
    [SerializeField] private float fastBeat = 2.2f;
    [Tooltip("How much the vignette swells on each beat. 0 = steady.")]
    [SerializeField, Range(0f, 1f)] private float beatStrength = 0.3f;

    [Header("Direction marker")]
    [Tooltip("A small chevron at the edge of the screen pointing at the nearest hunter while it is off screen.")]
    [SerializeField] private bool showDirection = true;
    [Tooltip("Empty = a clean arrowhead is generated at start. It should point up.")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Color markerColor = new Color(1f, 0.45f, 0.4f);
    [SerializeField] private float markerSize = 34f;
    [Tooltip("Distance from the screen edge, in canvas units.")]
    [SerializeField] private float edgePadding = 56f;

    private RectTransform rect;
    private Image vignette;
    private RectTransform vignetteRect;
    private Image marker;
    private RectTransform markerRect;
    private float danger;
    private float phase;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();

        vignette = MakeImage("Vignette", vignetteSprite != null ? vignetteSprite : MakeVignetteSprite());
        vignetteRect = vignette.rectTransform;
        vignetteRect.anchorMin = Vector2.zero;
        vignetteRect.anchorMax = Vector2.one;
        vignetteRect.offsetMin = Vector2.zero;
        vignetteRect.offsetMax = Vector2.zero;

        marker = MakeImage("Marker", markerSprite != null ? markerSprite : MakeMarkerSprite());
        markerRect = marker.rectTransform;
        markerRect.anchorMin = markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = Vector2.one * markerSize;
        marker.preserveAspect = true;
    }

    private Image MakeImage(string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    private void Update()
    {
        ChaseSequence chase = ChaseSequence.Active;
        // Chase danger or the player's own panic (a bite, low health), whichever is higher.
        float target = Mathf.Max(chase != null ? chase.Danger01 : 0f, PlayerPanic.Level);
        danger = Mathf.Lerp(danger, target, 1f - Mathf.Exp(-3f * Time.deltaTime));

        // A heartbeat: a strong thump, a weaker one right after, then quiet. In time with the audible heartbeat when
        // the player has a PlayerPanic, else on its own clock.
        float cycle;
        if (PlayerPanic.Instance != null)
        {
            cycle = PlayerPanic.Instance.HeartPhase;
        }
        else
        {
            phase += Time.deltaTime * Mathf.Lerp(slowBeat, fastBeat, danger);
            cycle = phase - Mathf.Floor(phase);
        }
        float beat = Thump(cycle, 0f, 0.16f) + 0.55f * Thump(cycle, 0.24f, 0.14f);
        float swell = 1f - beatStrength + beatStrength * beat;

        UpdateVignette(swell);
        UpdateMarker(chase, swell);
    }

    private void UpdateVignette(float swell)
    {
        float alpha = danger * maxAlpha * swell;
        vignette.enabled = alpha > 0.003f;
        if (!vignette.enabled)
            return;
        vignette.color = new Color(color.r, color.g, color.b, alpha);
        // The image is scaled up so only its outer rim shows when danger is low, and closes in as the pack gets near.
        float scale = Mathf.Lerp(1f + 1.4f * (1f - reach), 1f + 0.35f * (1f - reach), danger) + 0.03f * (swell - 1f);
        vignetteRect.localScale = Vector3.one * scale;
    }

    private void UpdateMarker(ChaseSequence chase, float swell)
    {
        Transform nearest = showDirection && chase != null ? chase.NearestPursuer : null;
        Camera cam = Camera.main;
        if (nearest == null || cam == null || danger < 0.02f)
        {
            marker.enabled = false;
            return;
        }

        Vector3 view = cam.WorldToViewportPoint(nearest.position);
        bool behind = view.z < 0f;
        bool onScreen = !behind && view.x > 0.02f && view.x < 0.98f && view.y > 0.02f && view.y < 0.98f;
        if (onScreen)
        {
            marker.enabled = false;
            return;
        }

        // Direction from the screen centre, flipped when the target is behind the camera, pinned to the screen edge.
        Vector2 half = rect.rect.size * 0.5f;
        Vector2 dir = new Vector2((view.x - 0.5f) * half.x * 2f, (view.y - 0.5f) * half.y * 2f);
        if (behind)
            dir = -dir;
        if (dir.sqrMagnitude < 0.001f)
            dir = Vector2.down;
        dir.Normalize();
        Vector2 limit = half - Vector2.one * edgePadding;
        float scaleX = Mathf.Abs(dir.x) > 0.0001f ? limit.x / Mathf.Abs(dir.x) : float.PositiveInfinity;
        float scaleY = Mathf.Abs(dir.y) > 0.0001f ? limit.y / Mathf.Abs(dir.y) : float.PositiveInfinity;
        markerRect.anchoredPosition = dir * Mathf.Min(scaleX, scaleY);
        markerRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        markerRect.localScale = Vector3.one * (0.9f + 0.2f * (swell - (1f - beatStrength)));

        marker.enabled = true;
        marker.color = new Color(markerColor.r, markerColor.g, markerColor.b, Mathf.Lerp(0.35f, 1f, danger));
    }

    // A single thump: quick rise, slower fall, centred on `at` within the 0..1 cycle.
    private static float Thump(float cycle, float at, float width)
    {
        float x = (cycle - at) / width;
        if (x < 0f || x > 1f)
            return 0f;
        return x < 0.3f ? x / 0.3f : 1f - (x - 0.3f) / 0.7f;
    }

    // A radial gradient: clear in the middle, opaque at the edges, with a soft roll-off.
    private static Sprite MakeVignetteSprite()
    {
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;
                float v = (y + 0.5f) / size * 2f - 1f;
                float d = Mathf.Sqrt(u * u + v * v);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.1f, d));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // A clean arrowhead pointing up, anti-aliased.
    private static Sprite MakeMarkerSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size * 2f - 1f;               // -1..1 across
                float v = (y + 0.5f) / size * 2f - 1f;               // -1..1 up
                // Triangle: apex at v = 0.85, base at v = -0.55, half-width grows toward the base; a notch cut into the base.
                float halfWidth = 0.62f * Mathf.InverseLerp(0.85f, -0.55f, v);
                float edge = Mathf.Min(halfWidth - Mathf.Abs(u), v + 0.55f, 0.85f - v);
                float notch = Mathf.Abs(u) * 1.1f + (v + 0.55f) * 1.6f - 0.42f;   // positive = outside the notch
                float inside = Mathf.Min(edge, notch);
                float a = Mathf.Clamp01(inside * size * 0.5f + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
