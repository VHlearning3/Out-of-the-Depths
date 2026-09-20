using UnityEngine;
using UnityEngine.UI;

// The on-screen hint while a pickup is being inspected: a mouse pictogram with the left button lit ("turn"), one with
// the wheel lit ("zoom") and an E key cap ("take"), fading in above the bottom of the screen. Built on the HUD canvas
// the first time it is needed, with generated icons. To use your own art, add an Inspect Hint UI to the HUD canvas
// yourself and drop sprites into Mouse Left / Mouse Wheel / Key Cap; that one is used instead of a generated one.
public class InspectHintUI : MonoBehaviour
{
    [Header("Icons (empty = generated)")]
    [SerializeField] private Sprite mouseLeft;
    [SerializeField] private Sprite mouseWheel;
    [SerializeField] private Sprite keyCap;

    [Header("Look")]
    [SerializeField] private Color color = new Color(0.9f, 0.97f, 1f);
    [Tooltip("The lit button / wheel in the generated icons.")]
    [SerializeField] private Color highlight = new Color(0.35f, 0.85f, 0.95f);
    [SerializeField] private float iconHeight = 46f;
    [SerializeField] private float spacing = 34f;
    [Tooltip("Height above the bottom of the screen, in canvas units.")]
    [SerializeField] private float bottomMargin = 130f;
    [SerializeField] private int labelSize = 16;
    [SerializeField] private float fadeSeconds = 0.15f;

    [Header("Words")]
    [SerializeField] private string turnLabel = "turn";
    [SerializeField] private string zoomLabel = "zoom";
    [SerializeField] private string takeLabel = "take";
    [SerializeField] private string keyLabel = "E";

    private static InspectHintUI instance;
    private CanvasGroup group;
    private float shown;
    private bool built;

    // Show / hide the hint from anywhere (a pickup being inspected). Safe to call with no HUD in the scene.
    public static void Show()
    {
        InspectHintUI ui = Get();
        if (ui != null)
            ui.shown = 1f;
    }

    public static void Hide()
    {
        if (instance != null)
            instance.shown = 0f;
    }

    private static InspectHintUI Get()
    {
        if (instance != null)
            return instance;
        instance = FindFirstObjectByType<InspectHintUI>(FindObjectsInactive.Include);
        if (instance != null)
            return instance;

        Canvas canvas = FindHudCanvas();
        if (canvas == null)
            return null;
        var go = new GameObject("InspectHint", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        instance = go.AddComponent<InspectHintUI>();
        return instance;
    }

    private static Canvas FindHudCanvas()
    {
        GameObject hud = GameObject.Find("HUD");
        Canvas canvas = hud != null ? hud.GetComponentInChildren<Canvas>() : null;
        if (canvas != null)
            return canvas;
        foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (c.renderMode != RenderMode.WorldSpace)
                return c;
        return null;
    }

    private void Awake()
    {
        instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Build()
    {
        if (built)
            return;
        built = true;

        var rect = GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, bottomMargin);
        rect.sizeDelta = new Vector2(600f, iconHeight + labelSize + 12f);

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var row = gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = spacing;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Entry(mouseLeft != null ? mouseLeft : MakeMouse(true, false), turnLabel, font, null);
        Entry(mouseWheel != null ? mouseWheel : MakeMouse(false, true), zoomLabel, font, null);
        Entry(keyCap != null ? keyCap : MakeKeyCap(), takeLabel, font, keyLabel);
    }

    // One icon over one word (and, for the key cap, the key letter drawn on the icon).
    private void Entry(Sprite sprite, string label, Font font, string onIcon)
    {
        float iconWidth = iconHeight * sprite.rect.width / sprite.rect.height;
        var entry = new GameObject(label, typeof(RectTransform));
        entry.transform.SetParent(transform, false);
        var entryRect = entry.GetComponent<RectTransform>();
        entryRect.sizeDelta = new Vector2(Mathf.Max(iconWidth, 56f), iconHeight + labelSize + 12f);

        var icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
        icon.transform.SetParent(entry.transform, false);
        icon.sprite = sprite;
        icon.color = Color.white;   // the colours are baked into the icon
        icon.raycastTarget = false;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(iconWidth, iconHeight);

        if (!string.IsNullOrEmpty(onIcon))
        {
            Text key = MakeText(icon.transform, onIcon, font, Mathf.RoundToInt(iconHeight * 0.5f), FontStyle.Bold, color);
            var keyRect = key.rectTransform;
            keyRect.anchorMin = Vector2.zero;
            keyRect.anchorMax = Vector2.one;
            keyRect.offsetMin = keyRect.offsetMax = Vector2.zero;
        }

        Text word = MakeText(entry.transform, label, font, labelSize, FontStyle.Normal, color);
        var wordRect = word.rectTransform;
        wordRect.anchorMin = wordRect.anchorMax = new Vector2(0.5f, 0f);
        wordRect.pivot = new Vector2(0.5f, 0f);
        wordRect.anchoredPosition = Vector2.zero;
        wordRect.sizeDelta = new Vector2(120f, labelSize + 6f);
    }

    private static Text MakeText(Transform parent, string content, Font font, int size, FontStyle style, Color tint)
    {
        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = tint;
        text.raycastTarget = false;
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    private void Update()
    {
        if (group == null)
            return;
        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, shown, step);
    }

    // ---- generated icons: a mouse outline (left button or wheel lit) and a key cap, anti-aliased ----

    private Sprite MakeMouse(bool leftLit, bool wheelLit)
    {
        const int w = 96, h = 144;
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - w * 0.5f;
                float py = y + 0.5f - h * 0.5f;
                float body = RoundedBox(px, py, 34f, 62f, 30f);        // the mouse itself
                float wheel = RoundedBox(px, py - 36f, 5f, 12f, 5f);   // the wheel, top middle
                float split = Mathf.Max(Mathf.Abs(py - 18f) - 2f, body + 4f);              // line under the buttons
                float divide = Mathf.Max(Mathf.Abs(px) - 2f, Mathf.Max(body + 4f, 18f - py)); // line between the buttons

                Color c = Color.clear;
                if (leftLit && body < 0f && py > 18f && px < 0f)
                    c = highlight;                                     // the left button, lit
                if (wheelLit && wheel < 0f)
                    c = highlight;
                float outline = Mathf.Max(Outline(body, 5f), Mathf.Max(Fill(split), Fill(divide)));
                if (!wheelLit)
                    outline = Mathf.Max(outline, Outline(wheel, 3f));
                c = Color.Lerp(c, color, outline);
                float alpha = Mathf.Max(c.a, outline);
                pixels[y * w + x] = new Color(c.r, c.g, c.b, alpha);
            }
        }
        return ToSprite(pixels, w, h);
    }

    private Sprite MakeKeyCap()
    {
        const int s = 96;
        var pixels = new Color[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float px = x + 0.5f - s * 0.5f;
                float py = y + 0.5f - s * 0.5f;
                float box = RoundedBox(px, py, 42f, 42f, 14f);
                float outline = Outline(box, 5f);
                float inner = Fill(box + 5f) * 0.18f;                  // a faint fill so it reads as a key
                Color c = Color.Lerp(new Color(color.r, color.g, color.b, inner), color, outline);
                pixels[y * s + x] = new Color(c.r, c.g, c.b, Mathf.Max(inner, outline));
            }
        }
        return ToSprite(pixels, s, s);
    }

    // Signed distance to a rounded box centred on the origin: negative inside.
    private static float RoundedBox(float px, float py, float halfW, float halfH, float radius)
    {
        float qx = Mathf.Abs(px) - halfW + radius;
        float qy = Mathf.Abs(py) - halfH + radius;
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;
    }

    private static float Outline(float distance, float thickness) => Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(distance) + 0.5f);
    private static float Fill(float distance) => Mathf.Clamp01(0.5f - distance);

    private static Sprite ToSprite(Color[] pixels, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
}
