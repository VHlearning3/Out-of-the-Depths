using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The HUD's shared look, so every piece of it reads as one thing: dark sea-glass panels with a hairline rim and a soft
// shadow, rounded corners, the game font with a soft drop shadow, and a small palette (aqua accent, gold for goals and
// keys, coral for danger). Holds the colours, makes every shape it needs once (rounded panels, pills, circles, the
// fish-shaped hunger gauge) as textures or sprites, and draws them for the IMGUI parts of the HUD, plus crisp
// anti-aliased arcs and triangles with GL for the rings round the crosshair. Nothing here is an asset: it is all
// generated the first time it is asked for and kept for the session.
public static class HudStyle
{
    // ---- Palette -------------------------------------------------------------------------------------------------

    public static readonly Color Ink = new Color(0.03f, 0.09f, 0.12f, 0.72f);        // panel fill
    public static readonly Color Rim = new Color(0.70f, 0.93f, 0.95f, 0.16f);        // hairline round panels
    public static readonly Color Text = new Color(0.91f, 0.97f, 0.97f);
    public static readonly Color Muted = new Color(0.60f, 0.73f, 0.75f);
    public static readonly Color Accent = new Color(0.45f, 0.90f, 0.86f);            // sea glass
    public static readonly Color Gold = new Color(1f, 0.80f, 0.42f);                 // goals, keys, weapons
    public static readonly Color Danger = new Color(1f, 0.42f, 0.36f);               // coral
    public static readonly Color Health = new Color(0.94f, 0.34f, 0.37f);
    public static readonly Color Hunger = new Color(1f, 0.74f, 0.34f);
    public static readonly Color Blocked = new Color(1f, 0.56f, 0.50f);              // a prompt you cannot do yet
    public static readonly Color Ready = new Color(0.56f, 0.94f, 0.71f);             // a prompt you now can
    public static readonly Color KeyCap = new Color(0.91f, 0.97f, 0.97f);
    public static readonly Color KeyInk = new Color(0.04f, 0.12f, 0.15f);
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);

    // Pixels per 1080p pixel, with the player's HUD size (Settings) folded in, for the HUD drawn in code.
    public static float Scale => Screen.height / 1080f * UIScale.Hud;

    // ---- Sprites for the canvas HUD --------------------------------------------------------------------------------

    private static Sprite pill, pillShaded, pillRim, rounded, roundedShaded, roundedRim, glow, dot, ring, pearl, backlight;
    private static Sprite fishBody, fishFill, fishOverlay;

    // A circle with 32 px borders: stretched (sliced) it is a pill of any size, rounded ends and all.
    public static Sprite Pill => pill != null ? pill : pill = SlicedSprite(RoundedTexture(64, 32f, 0f, false, 1f), 32);
    // The same, lighter at the top: for fills, tinted by the Image colour.
    public static Sprite PillShaded => pillShaded != null ? pillShaded : pillShaded = SlicedSprite(RoundedTexture(64, 32f, 0f, false, 0.78f), 32);
    public static Sprite PillRim => pillRim != null ? pillRim : pillRim = SlicedSprite(RoundedTexture(64, 32f, 1.3f, true, 1f), 32);
    // A rounded square (12 px corners) for slots and keycaps.
    public static Sprite Rounded => rounded != null ? rounded : rounded = SlicedSprite(RoundedTexture(64, 12f, 0f, false, 1f), 16);
    public static Sprite RoundedShaded => roundedShaded != null ? roundedShaded : roundedShaded = SlicedSprite(RoundedTexture(64, 12f, 0f, false, 0.55f), 16);
    public static Sprite RoundedRim => roundedRim != null ? roundedRim : roundedRim = SlicedSprite(RoundedTexture(64, 12f, 1.3f, true, 1f), 16);
    // A soft rounded glow (for the selected slot), 9-sliced.
    public static Sprite Glow => glow != null ? glow : glow = SlicedSprite(GlowTexture(96, 12f, 22f), 40);
    // The reticle: a white dot with a soft dark halo, so it reads on light and dark water alike.
    public static Sprite Dot => dot != null ? dot : dot = PlainSprite(DotTexture(48));
    public static Sprite Ring => ring != null ? ring : ring = PlainSprite(RingTexture(96, 2.6f));
    // A radial light, for behind icons.
    public static Sprite Backlight => backlight != null ? backlight : backlight = PlainSprite(RadialTexture(64));
    // A pearl: pale, pink-tinged, with a highlight (the collectible counter's icon). Coloured, so leave the Image white.
    public static Sprite Pearl => pearl != null ? pearl : pearl = PlainSprite(PearlTexture(64));

    // The fish-shaped hunger gauge: Body (tint it dark; with its own soft shadow), Fill (the same shape, lighter at
    // the top; tint it and fill it horizontally from the tail) and Overlay (rim, eye and gill, coloured: leave white).
    public static Sprite FishBody { get { MakeFish(); return fishBody; } }
    public static Sprite FishFill { get { MakeFish(); return fishFill; } }
    public static Sprite FishOverlay { get { MakeFish(); return fishOverlay; } }
    public const float FishAspect = 116f / 48f;   // width / height of the fish

    private static Sprite SlicedSprite(Texture2D tex, int border) =>
        Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));

    private static Sprite PlainSprite(Texture2D tex) =>
        Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

    private static Texture2D NewTexture(int width, int height) =>
        new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };

    // Signed distance to a rounded rectangle centred in a size x size square (negative inside).
    private static float RoundedDistance(float x, float y, float width, float height, float radius)
    {
        float qx = Mathf.Abs(x - width * 0.5f) - width * 0.5f + radius;
        float qy = Mathf.Abs(y - height * 0.5f) - height * 0.5f + radius;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
    }

    // White rounded rectangle: filled (lightness from 1 at the top to `bottomShade` at the bottom) or just a rim
    // `rimWidth` wide along the inside of its edge.
    private static Texture2D RoundedTexture(int size, float radius, float rimWidth, bool rimOnly, float bottomShade)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, size, size, radius);
                float a = rimOnly ? Mathf.Clamp01(rimWidth * 0.5f + 0.5f - Mathf.Abs(d + rimWidth * 0.5f)) : Mathf.Clamp01(0.5f - d);
                float shade = Mathf.Lerp(bottomShade, 1f, (y + 0.5f) / size);   // row 0 is the bottom
                pixels[y * size + x] = new Color(shade, shade, shade, a);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // A soft glow round a rounded rectangle `inset` px in from the edges, fading out over `spread` px.
    private static Texture2D GlowTexture(int size, float radius, float spread)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float inner = size - spread * 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f - spread, y + 0.5f - spread, inner, inner, radius);
                float k = Mathf.Clamp01(1f - Mathf.Max(0f, d) / spread);
                pixels[y * size + x] = new Color(1f, 1f, 1f, k * k * (3f - 2f * k));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D DotTexture(int size)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f, core = size * 0.2f, halo = size * 0.46f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                float white = Mathf.Clamp01(core - d + 0.5f);
                float shade = Mathf.Clamp01(1f - (d - core) / (halo - core));
                float a = Mathf.Max(white, shade * shade * 0.4f);
                float lum = a > 0f ? white / a : 0f;
                pixels[y * size + x] = new Color(lum, lum, lum, a);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D RingTexture(int size, float width)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f, radius = half - width - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(width * 0.5f + 0.5f - Mathf.Abs(d - radius)));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D RadialTexture(int size)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float k = Mathf.Clamp01(1f - new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half);
                pixels[y * size + x] = new Color(1f, 1f, 1f, k * k);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D PearlTexture(int size)
    {
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float half = size * 0.5f, radius = half - 3f;
        var light = new Vector2(-0.35f, 0.4f) * radius;   // up-left (rows go up)
        Color white = new Color(1f, 1f, 1f), pink = new Color(0.96f, 0.9f, 0.93f), edge = new Color(0.72f, 0.6f, 0.66f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d = p.magnitude;
                float a = Mathf.Clamp01(radius - d + 0.5f);
                float shadow = Mathf.Clamp01(1f - (d - radius) / 3f) * 0.45f * (1f - a);   // a soft dark edge round it
                float k = Mathf.Clamp01((p - light).magnitude / (radius * 1.6f));
                Color c = k < 0.3f ? Color.Lerp(white, pink, k / 0.3f) : Color.Lerp(pink, edge, (k - 0.3f) / 0.7f);
                float alpha = a + shadow;
                pixels[y * size + x] = alpha > 0f ? new Color(c.r * a / alpha, c.g * a / alpha, c.b * a / alpha, alpha) : Color.clear;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ---- The fish gauge --------------------------------------------------------------------------------------------

    // The fish's outline in shares of its box (x from the tail on the left to the nose on the right, y from the top),
    // as curves: a teardrop body, a thin stalk and a forked tail.
    private static List<Vector2> FishOutline()
    {
        var points = new List<Vector2>();
        Vector2 at = new Vector2(0.99f, 0.5f);
        points.Add(at);
        void Cubic(Vector2 b, Vector2 c, Vector2 d)
        {
            for (int i = 1; i <= 24; i++)
            {
                float t = i / 24f, u = 1f - t;
                points.Add(u * u * u * at + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
            at = d;
        }
        void Quad(Vector2 b, Vector2 c)
        {
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f, u = 1f - t;
                points.Add(u * u * at + 2f * u * t * b + t * t * c);
            }
            at = c;
        }
        void Line(Vector2 b)
        {
            points.Add(b);
            at = b;
        }
        Cubic(new Vector2(0.95f, 0.10f), new Vector2(0.55f, -0.02f), new Vector2(0.36f, 0.26f));   // the back
        Quad(new Vector2(0.26f, 0.38f), new Vector2(0.20f, 0.44f));                                  // into the stalk
        Line(new Vector2(0.04f, 0.06f));                                                              // upper tail tip
        Quad(new Vector2(0.10f, 0.50f), new Vector2(0.04f, 0.94f));                                  // the fork
        Line(new Vector2(0.20f, 0.56f));
        Quad(new Vector2(0.26f, 0.62f), new Vector2(0.36f, 0.74f));
        Cubic(new Vector2(0.55f, 1.02f), new Vector2(0.95f, 0.90f), new Vector2(0.99f, 0.5f));      // the belly
        return points;
    }

    private static void MakeFish()
    {
        if (fishBody != null && fishFill != null && fishOverlay != null)
            return;
        const int width = 256, height = 106, pad = 8;
        float boxW = width - pad * 2f, boxH = height - pad * 2f;
        List<Vector2> outline = FishOutline();
        // Into texture pixels (rows go up).
        var poly = new Vector2[outline.Count];
        for (int i = 0; i < poly.Length; i++)
            poly[i] = new Vector2(pad + outline[i].x * boxW, height - (pad + outline[i].y * boxH));

        Texture2D body = NewTexture(width, height), fill = NewTexture(width, height), overlay = NewTexture(width, height);
        var bodyPx = new Color[width * height];
        var fillPx = new Color[width * height];
        var overPx = new Color[width * height];
        Vector2 eye = new Vector2(pad + 0.80f * boxW, height - (pad + 0.40f * boxH));
        float eyeR = 0.075f * boxH, glintR = 0.028f * boxH;
        Vector2 glint = eye + new Vector2(1.2f, 1.2f);
        Vector2 gill = new Vector2(pad + 0.70f * boxW, height - (pad + 0.5f * boxH));
        float gillR = 0.26f * boxH;
        Color rimColor = new Color(0.78f, 0.96f, 0.97f, 0.42f);
        Color eyeColor = new Color(0.03f, 0.09f, 0.12f, 0.92f);

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float sd = SignedDistance(p, poly);   // positive inside
                float cover = Mathf.Clamp01(sd + 0.5f);
                float shadowK = Mathf.Clamp01(1f - (-sd + 2.5f) / 7f);   // below and round it
                int i = y * width + x;
                bodyPx[i] = new Color(1f, 1f, 1f, Mathf.Max(cover, shadowK * shadowK * 0.55f));
                float shade = Mathf.Lerp(0.78f, 1f, (y + 0.5f) / height) + ((y + 0.5f) / height > 0.62f ? 0.06f : 0f);
                fillPx[i] = new Color(shade, shade, shade, cover);

                // Overlay: the rim just inside the edge, the eye with its glint, a faint gill line.
                Color over = new Color(rimColor.r, rimColor.g, rimColor.b, rimColor.a * Mathf.Clamp01(1.3f - Mathf.Abs(sd - 1.1f)) * Mathf.Clamp01(sd + 1f));
                float gillD = Mathf.Abs((p - gill).magnitude - gillR);
                float gillAngle = Mathf.Abs(Mathf.Atan2(p.y - gill.y, p.x - gill.x));
                if (gillAngle < 0.9f)
                    over = Blend(over, new Color(0.03f, 0.09f, 0.12f, 0.3f * Mathf.Clamp01(1f - gillD) * cover));
                over = Blend(over, new Color(eyeColor.r, eyeColor.g, eyeColor.b, eyeColor.a * Mathf.Clamp01(eyeR - (p - eye).magnitude + 0.5f)));
                over = Blend(over, new Color(1f, 1f, 1f, 0.85f * Mathf.Clamp01(glintR - (p - glint).magnitude + 0.5f)));
                overPx[i] = over;
            }
        body.SetPixels(bodyPx);
        body.Apply();
        fill.SetPixels(fillPx);
        fill.Apply();
        overlay.SetPixels(overPx);
        overlay.Apply();
        fishBody = PlainSprite(body);
        fishFill = PlainSprite(fill);
        fishOverlay = PlainSprite(overlay);
    }

    // `top` over `bottom`, straight alpha.
    private static Color Blend(Color bottom, Color top)
    {
        float a = top.a + bottom.a * (1f - top.a);
        if (a <= 0f)
            return Color.clear;
        return new Color((top.r * top.a + bottom.r * bottom.a * (1f - top.a)) / a, (top.g * top.a + bottom.g * bottom.a * (1f - top.a)) / a,
            (top.b * top.a + bottom.b * bottom.a * (1f - top.a)) / a, a);
    }

    // Distance from p to the polygon's edge, positive inside (even-odd rule).
    private static float SignedDistance(Vector2 p, Vector2[] poly)
    {
        float best = float.MaxValue;
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            best = Mathf.Min(best, (p - (a + ab * t)).sqrMagnitude);
            if ((a.y > p.y) != (b.y > p.y) && p.x < a.x + (p.y - a.y) / (b.y - a.y) * (b.x - a.x))
                inside = !inside;
        }
        float d = Mathf.Sqrt(best);
        return inside ? d : -d;
    }

    // ---- IMGUI drawing ---------------------------------------------------------------------------------------------

    private static readonly Dictionary<int, GUIStyle> fills = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> rims = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<int, GUIStyle> shadows = new Dictionary<int, GUIStyle>();
    private static readonly Dictionary<long, GUIStyle> labels = new Dictionary<long, GUIStyle>();

    // A panel: soft shadow, dark fill, hairline rim, `radius` px corners. Call in Repaint.
    public static void Panel(Rect rect, float radius, float alpha = 1f) => Panel(rect, radius, Ink, Rim, alpha);

    public static void Panel(Rect rect, float radius, Color fill, Color rim, float alpha = 1f)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f)), 1, 64);
        Color keep = GUI.color;
        float s = Scale;
        GUI.color = keep * new Color(0f, 0f, 0f, 0.42f * alpha);
        Rect shadow = new Rect(rect.x, rect.y + 3f * s, rect.width, rect.height);
        int blur = Mathf.Clamp(Mathf.RoundToInt(12f * s), 4, 32);
        ShadowStyle(r, blur).Draw(new Rect(shadow.x - blur, shadow.y - blur, shadow.width + blur * 2f, shadow.height + blur * 2f), GUIContent.none, false, false, false, false);
        GUI.color = keep * new Color(fill.r, fill.g, fill.b, fill.a * alpha);
        FillStyle(r).Draw(rect, GUIContent.none, false, false, false, false);
        if (rim.a > 0f)
        {
            GUI.color = keep * new Color(rim.r, rim.g, rim.b, rim.a * alpha);
            RimStyle(r).Draw(rect, GUIContent.none, false, false, false, false);
        }
        GUI.color = keep;
    }

    // Just a rounded fill (bars, pips, keycaps), in `color`.
    public static void Fill(Rect rect, float radius, Color color)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f)), 1, 64);
        Color keep = GUI.color;
        GUI.color = keep * color;
        FillStyle(r).Draw(rect, GUIContent.none, false, false, false, false);
        GUI.color = keep;
    }

    public static void Outline(Rect rect, float radius, Color color)
    {
        int r = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f)), 1, 64);
        Color keep = GUI.color;
        GUI.color = keep * color;
        RimStyle(r).Draw(rect, GUIContent.none, false, false, false, false);
        GUI.color = keep;
    }

    private static GUIStyle FillStyle(int r)
    {
        if (fills.TryGetValue(r, out GUIStyle style) && style.normal.background != null)
            return style;
        int size = r * 2 + 1;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - RoundedDistance(x + 0.5f, y + 0.5f, size, size, r)));
        tex.SetPixels(pixels);
        tex.Apply();
        style = new GUIStyle { border = new RectOffset(r, r, r, r) };
        style.normal.background = tex;
        return fills[r] = style;
    }

    private static GUIStyle RimStyle(int r)
    {
        if (rims.TryGetValue(r, out GUIStyle style) && style.normal.background != null)
            return style;
        int size = r * 2 + 1;
        const float width = 1.2f;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f, y + 0.5f, size, size, r);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(width * 0.5f + 0.5f - Mathf.Abs(d + width * 0.5f)));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        style = new GUIStyle { border = new RectOffset(r, r, r, r) };
        style.normal.background = tex;
        return rims[r] = style;
    }

    private static GUIStyle ShadowStyle(int r, int blur)
    {
        int key = r * 100 + blur;
        if (shadows.TryGetValue(key, out GUIStyle style) && style.normal.background != null)
            return style;
        int size = (r + blur) * 2 + 1;
        Texture2D tex = NewTexture(size, size);
        var pixels = new Color[size * size];
        float inner = size - blur * 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedDistance(x + 0.5f - blur, y + 0.5f - blur, inner, inner, r);
                float k = Mathf.Clamp01(1f - (d + blur * 0.35f) / (blur * 1.1f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, k * k * (3f - 2f * k));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        int border = r + blur;
        style = new GUIStyle { border = new RectOffset(border, border, border, border) };
        style.normal.background = tex;
        return shadows[key] = style;
    }

    // A label style in the game font at `size` px, cached. The colour comes from GUI.color (the style's is white).
    public static GUIStyle Label(int size, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
    {
        size = Mathf.Max(6, size);
        long key = ((long)size << 8) | ((long)anchor << 1) | (wrap ? 1L : 0L);
        if (labels.TryGetValue(key, out GUIStyle style) && style.font == GameFont.Font)
            return style;
        style = new GUIStyle(GUI.skin.label) { fontSize = size, alignment = anchor, wordWrap = wrap, clipping = TextClipping.Overflow, richText = false, font = GameFont.Font };
        style.padding = new RectOffset(0, 0, 0, 0);
        style.margin = new RectOffset(0, 0, 0, 0);
        style.normal.textColor = Color.white;
        return labels[key] = style;
    }

    // Text with a soft drop shadow, so it reads over bright water.
    public static void Write(Rect rect, string text, GUIStyle style, Color color, float alpha = 1f)
    {
        if (string.IsNullOrEmpty(text) || alpha <= 0f)
            return;
        Color keep = GUI.color;
        float offset = Mathf.Max(1f, 1.5f * Scale * 0.75f);
        GUI.color = keep * new Color(0f, 0f, 0f, 0.55f * alpha * color.a);
        GUI.Label(new Rect(rect.x + offset * 0.6f, rect.y + offset, rect.width, rect.height), text, style);
        GUI.color = keep * new Color(color.r, color.g, color.b, color.a * alpha);
        GUI.Label(rect, text, style);
        GUI.color = keep;
    }

    // Small capitals spread out (the "OBJECTIVE" label): each letter drawn `spacing` px apart. Returns its width.
    public static float Spaced(Vector2 at, string text, GUIStyle style, Color color, float spacing, float alpha = 1f, bool draw = true)
    {
        float x = at.x, width = 0f;
        var content = new GUIContent();
        for (int i = 0; i < text.Length; i++)
        {
            content.text = text[i].ToString();
            float w = style.CalcSize(content).x;
            if (draw)
                Write(new Rect(x, at.y, w + 4f, style.fontSize * 1.6f), content.text, style, color, alpha);
            x += w + spacing;
            width += w + (i < text.Length - 1 ? spacing : 0f);
        }
        return width;
    }

    // ---- GL shapes (crisp at any size), for the rings round the crosshair -------------------------------------------

    private static Material glMaterial;

    // Start a batch of GL shapes in GUI pixels (y down). Only in a Repaint event; finish with EndShapes.
    public static bool BeginShapes()
    {
        if (Event.current == null || Event.current.type != EventType.Repaint)
            return false;
        if (glMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                return false;
            glMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            glMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            glMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            glMaterial.SetInt("_Cull", (int)CullMode.Off);
            glMaterial.SetInt("_ZWrite", 0);
            glMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        }
        glMaterial.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
        GL.Begin(GL.TRIANGLES);
        return true;
    }

    public static void EndShapes()
    {
        GL.End();
        GL.PopMatrix();
    }

    // Where an angle points on a circle: degrees clockwise from straight up (screen pixels, y down).
    public static Vector2 OnCircle(Vector2 centre, float radius, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return centre + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * radius;
    }

    // An arc `width` px thick from `fromDeg` to `toDeg` (clockwise from up), anti-aliased, with rounded-looking ends
    // that fade over `endFade` degrees (0 = square ends). Between BeginShapes and EndShapes.
    public static void Arc(Vector2 centre, float radius, float width, float fromDeg, float toDeg, Color color, float endFade = 0f)
    {
        if (toDeg < fromDeg)
            (fromDeg, toDeg) = (toDeg, fromDeg);
        float span = toDeg - fromDeg;
        if (span <= 0.01f || color.a <= 0f)
            return;
        int steps = Mathf.Clamp(Mathf.CeilToInt(span / 3f), 2, 120);
        float half = width * 0.5f;
        const float feather = 1f;   // px of soft edge on each side
        for (int i = 0; i < steps; i++)
        {
            float a0 = fromDeg + span * i / steps, a1 = fromDeg + span * (i + 1) / steps;
            float f0 = EndFade(a0 - fromDeg, toDeg - a0, endFade), f1 = EndFade(a1 - fromDeg, toDeg - a1, endFade);
            Color c0 = new Color(color.r, color.g, color.b, color.a * f0), c1 = new Color(color.r, color.g, color.b, color.a * f1);
            Color z0 = new Color(color.r, color.g, color.b, 0f), z1 = z0;
            // outer feather, core, inner feather
            Band(centre, radius + half, radius + half + feather, a0, a1, c0, c1, z0, z1);
            Band(centre, radius - half, radius + half, a0, a1, c0, c1, c0, c1);
            Band(centre, radius - half - feather, radius - half, a0, a1, z0, z1, c0, c1);
        }
    }

    private static float EndFade(float fromStart, float toEnd, float fade)
    {
        if (fade <= 0f)
            return 1f;
        float k = Mathf.Clamp01(Mathf.Min(fromStart, toEnd) / fade);
        return k * k * (3f - 2f * k);
    }

    // One quad of a ring between radii r0 (inner) and r1 (outer), angles a0..a1; colours at inner/outer per angle.
    private static void Band(Vector2 c, float r0, float r1, float a0, float a1, Color inner0, Color inner1, Color outer0, Color outer1)
    {
        Vector2 p0 = OnCircle(c, r0, a0), p1 = OnCircle(c, r0, a1), q0 = OnCircle(c, r1, a0), q1 = OnCircle(c, r1, a1);
        Vertex(p0, inner0); Vertex(q0, outer0); Vertex(q1, outer1);
        Vertex(p0, inner0); Vertex(q1, outer1); Vertex(p1, inner1);
    }

    private static void Vertex(Vector2 p, Color c)
    {
        GL.Color(c);
        GL.Vertex3(p.x, p.y, 0f);
    }

    // A filled triangle (no soft edge; keep them small).
    public static void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        Vertex(a, color);
        Vertex(b, color);
        Vertex(c, color);
    }

    // A small chevron (arrowhead) at `at` pointing `degrees` clockwise from up, `size` px tall, with a soft shadow.
    public static void Chevron(Vector2 at, float degrees, float size, Color color)
    {
        Vector2 Rot(Vector2 v)
        {
            float a = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            return at + new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
        Vector2 tip = new Vector2(0f, -size * 0.55f), left = new Vector2(-size * 0.5f, size * 0.4f), right = new Vector2(size * 0.5f, size * 0.4f), notch = new Vector2(0f, size * 0.08f);
        var shade = new Color(0f, 0f, 0f, 0.35f * color.a);
        Vector2 drop = new Vector2(0f, Mathf.Max(1f, size * 0.08f));
        Triangle(Rot(tip) + drop, Rot(left) + drop, Rot(notch) + drop, shade);
        Triangle(Rot(tip) + drop, Rot(notch) + drop, Rot(right) + drop, shade);
        Triangle(Rot(tip), Rot(left), Rot(notch), color);
        Triangle(Rot(tip), Rot(notch), Rot(right), color);
    }

    // A diamond outline `radius` px from its middle to each point, `width` px thick, with a filled dot in the middle.
    public static void Diamond(Vector2 at, float radius, float width, Color color, Color fill)
    {
        Vector2 up = at + new Vector2(0f, -radius), right = at + new Vector2(radius, 0f), down = at + new Vector2(0f, radius), left = at + new Vector2(-radius, 0f);
        if (fill.a > 0f)
        {
            Triangle(up, right, down, fill);
            Triangle(up, down, left, fill);
        }
        Line(up, right, width, color);
        Line(right, down, width, color);
        Line(down, left, width, color);
        Line(left, up, width, color);
    }

    // A straight line `width` px thick, with a 1 px soft edge.
    public static void Line(Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 1e-4f)
            return;
        Vector2 n = new Vector2(-dir.y, dir.x).normalized;
        // Lengthen a touch so corners meet.
        Vector2 along = dir.normalized * (width * 0.5f);
        a -= along;
        b += along;
        float half = width * 0.5f;
        Color clear = new Color(color.r, color.g, color.b, 0f);
        Strip(a, b, n * (half + 1f), n * half, clear, color);
        Strip(a, b, n * half, -n * half, color, color);
        Strip(a, b, -n * half, -n * (half + 1f), color, clear);
    }

    private static void Strip(Vector2 a, Vector2 b, Vector2 off0, Vector2 off1, Color c0, Color c1)
    {
        Vertex(a + off0, c0); Vertex(b + off0, c0); Vertex(b + off1, c1);
        Vertex(a + off0, c0); Vertex(b + off1, c1); Vertex(a + off1, c1);
    }
}
