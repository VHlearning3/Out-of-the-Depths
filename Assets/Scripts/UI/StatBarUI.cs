using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// A HUD meter: give it the Fill rect (a bar) or a Filled Image (any shape, e.g. the fish-shaped hunger gauge) and an
// optional label, then call SetValue(current, max). With Auto Hide it fades away while it is not needed: it shows
// while the value is under Show Below, and for a few seconds after it jumps (a bite, eating, a dash).
// Generated Look (on by default) dresses a meter that still shows the plain white placeholder in the HUD's own style
// (HudStyle): the hunger gauge (a Filled Image filling upwards) becomes a fish that fills from its tail, with the
// number beside it, and a bar becomes a slim rounded pill with a pale chunk showing what the last hit took, tucked
// under the fish. Under Low Share the fill turns coral and pulses. A meter with real art keeps it untouched.
// Drawn Look (ahead of that, when the team's HUD art is in Assets/Resources/HUD) uses the drawings instead: the hunger
// gauge is the tall fish (HungerBarEmpty, the skeleton, under HungerBarFill, the green fish, which drains from the mouth
// back to the tail), and the health bar is the heart and slanted bar (HealthBarEmpty under HealthBarFill, which shrinks along the
// bar, with a pale chunk showing what the last hit took) set beside the fish's head, as the team's mock-up has them.
public class StatBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [Tooltip("Instead of Fill Rect: an Image with Image Type = Filled. Its Fill Amount is driven, so the meter can be any sprite shape.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Text valueLabel;
    [Tooltip("How quickly the bar visually catches up to its real value. 0 = instant.")]
    [SerializeField] private float smoothSpeed = 8f;

    [Header("Auto hide")]
    [Tooltip("Fade the meter away while it is not needed (the Settings page can turn this off for the whole HUD).")]
    [SerializeField] private bool autoHide = false;
    [Tooltip("Always shown while the value is under this fraction of the max.")]
    [SerializeField, Range(0f, 1f)] private float showBelow = 0.5f;
    [Tooltip("A change bigger than this fraction of the max at once (not the slow drain) shows it...")]
    [SerializeField, Range(0f, 0.5f)] private float jumpToShow = 0.015f;
    [Tooltip("...for this many seconds.")]
    [SerializeField] private float showSeconds = 3f;

    [Header("Generated look")]
    [Tooltip("Dress a meter that still shows the plain white placeholder in the HUD style: a fish for a gauge that fills upwards (hunger), a slim pill for a bar (health). Real art is never replaced.")]
    [SerializeField] private bool generatedLook = true;
    [Tooltip("The fill colour. Alpha 0 = the HUD palette's (amber for the fish, coral red for the bar).")]
    [SerializeField] private Color generatedColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Under this share of the max the fill turns coral and pulses gently.")]
    [SerializeField, Range(0f, 1f)] private float lowShare = 0.25f;

    [Header("Drawn look")]
    [Tooltip("Use the team's drawn HUD (Assets/Resources/HUD: HungerBarEmpty / HungerBarFill, HealthBarEmpty / HealthBarFill) when it is there, ahead of the generated look.")]
    [SerializeField] private bool drawnLook = true;
    [Tooltip("Drawn look: how tall the hunger fish is, in canvas units. The heart and bar beside it are sized to match.")]
    [SerializeField] private float drawnFishHeight = 230f;
    [Tooltip("Drawn look: show the number beside the meter too (the mock-up has none).")]
    [SerializeField] private bool drawnNumbers = false;
    [Tooltip("Drawn look: how solid the hunger fish's colour is over the skeleton (1 = solid and hides it; less lets the bones and outline show through).")]
    [SerializeField, Range(0.2f, 1f)] private float fishSeeThrough = 0.7f;
    [Tooltip("Drawn look: the hunger fish's colour when full, at half, and when nearly empty; it shades between them as it drains.")]
    [SerializeField] private Color drawnFishFull = new Color(0.18f, 0.83f, 0.23f);
    [SerializeField] private Color drawnFishHalf = new Color(0.98f, 0.8f, 0.18f);
    [SerializeField] private Color drawnFishEmpty = new Color(0.9f, 0.2f, 0.15f);
    [Tooltip("Drawn look: the health bar's colour when full, at half, and when nearly empty; it shades between them.")]
    [SerializeField] private Color drawnBarFull = new Color(0.18f, 0.83f, 0.23f);
    [SerializeField] private Color drawnBarHalf = new Color(0.98f, 0.8f, 0.18f);
    [SerializeField] private Color drawnBarEmpty = new Color(0.9f, 0.2f, 0.15f);
    [Tooltip("Drawn look: the hunger fish empties from its mouth back to its tail. Untick if it empties from the tail instead.")]
    [SerializeField] private bool fishFillFromTail = true;
    [Tooltip("Drawn look: the hunger number inside the fish (how much hunger you have). Off: the fish itself shows it.")]
    [SerializeField] private bool numberInFish = false;
    [Tooltip("Drawn look: how solid the hunger number is (less = more see-through).")]
    [SerializeField, Range(0.1f, 1f)] private float drawnNumberAlpha = 0.55f;
    [Tooltip("Drawn look: a small name under the meter (Hunger, Health) that shows what it is. Empty = none.")]
    [SerializeField] private string drawnCaption = "";
    [Tooltip("The name shows this many seconds at the start and after the meter jumps (a bite, eating), and while the mouse is over the meter when the cursor is shown.")]
    [SerializeField] private float captionSeconds = 4f;
    // Where the number sits in the fish drawing (shares of it, from the bottom left): the thick of its body.
    private static readonly Vector2 FishNumberAt = new Vector2(0.36f, 0.66f);

    // The drawings' canvases, in pixels, and where the fill runs on them (as shares of the picture): the bar's red
    // from just after the heart to the slanted end; the fish from its tail to its head.
    private static readonly Vector2 FishArt = new Vector2(658f, 919f);
    private static readonly Vector2 BarArt = new Vector2(796f, 168f);
    private const float BarFillFrom = 180f / 796f, BarFillTo = 757f / 796f;
    private const float FishFillFrom = 0f, FishFillTo = 1f;
    // Where the heart and bar sit against the fish, in the fish drawing's pixels (from the mock-up): just past the
    // head, their top a touch above the fish's.
    private static readonly Vector2 BarBesideFish = new Vector2(670f, 10f);

    private enum Shape { None, Fish, Bar }

    private HudAutoHide fade;

    private float targetPct = 1f;
    private float currentPct = 1f;

    private Shape shape;
    private Image fill;
    private Color fillColor;
    private RectTransform lostRect;
    private Image lost;
    private float trailing = 1f;
    private float droppedAt = -10f;
    private bool drawn;
    private Text caption;
    private float captionUntil;
    private float captionShown;
    private Image drawnFill;
    private Image drawnLost;

    private void Awake()
    {
        if (fillRect == null && fillImage == null)
            Debug.LogError($"{name}: StatBarUI has neither a Fill Rect nor a Fill Image assigned, the meter will not update.", this);
        if (autoHide)
        {
            fade = HudAutoHide.On(this, showSeconds);
            fade.Needed = () => targetPct < showBelow;
        }
        if (generatedLook)
            Dress();
    }

    private void Start()
    {
        if (shape == Shape.Bar)
        {
            if (drawn)
                BesideDrawnFish();
            else
                TuckUnderFish();
        }
    }

    public void SetValue(float current, float max)
    {
        float was = targetPct;
        targetPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (fade != null && Mathf.Abs(targetPct - was) > jumpToShow)
            fade.Wake();
        if (targetPct < was - 0.001f)
            droppedAt = Time.time;
        if (caption != null && Mathf.Abs(targetPct - was) > jumpToShow)
            captionUntil = Time.time + captionSeconds;

        if (valueLabel != null)
            valueLabel.text = shape != Shape.None ? $"{current:0}" : $"{current:0}/{max:0}";

        if (smoothSpeed <= 0f)
            ApplyPct(targetPct);
    }

    private void Update()
    {
        if (caption != null)
            FadeCaption();
        if (smoothSpeed > 0f && !Mathf.Approximately(currentPct, targetPct))
            ApplyPct(Mathf.Lerp(currentPct, targetPct, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)));
        if (shape == Shape.None || fill == null)
            return;

        // Low: coral, breathing (the drawings keep their own colours and only breathe; the drawn fish shades from
        // green through yellow to red as it drains).
        bool low = targetPct < lowShare;
        Color colour = drawn ? (shape == Shape.Fish ? FishColour(currentPct) : BarColour(currentPct)) : low ? HudStyle.Danger : fillColor;
        float pulse = low ? 0.78f + 0.22f * Mathf.Sin(Time.time * 5f) : 1f;
        fill.color = new Color(colour.r * pulse, colour.g * pulse, colour.b * pulse, drawn ? colour.a : 1f);

        // The pale chunk behind the fill: waits a moment after a hit, then drains to the fill.
        if (lostRect != null || drawnLost != null)
        {
            if (currentPct >= trailing)
                trailing = currentPct;
            else if (Time.time - droppedAt > 0.4f)
                trailing = Mathf.MoveTowards(trailing, currentPct, Time.deltaTime * 0.6f);
            if (drawnLost != null)
                drawnLost.fillAmount = Mathf.Lerp(BarFillFrom, BarFillTo, trailing);
            else
                lostRect.anchorMax = new Vector2(trailing, lostRect.anchorMax.y);
            lost.enabled = trailing > currentPct + 0.003f;
        }
    }

    private void ApplyPct(float pct)
    {
        currentPct = pct;
        if (drawnFill != null)
            drawnFill.fillAmount = shape == Shape.Fish ? Mathf.Lerp(FishFillFrom, FishFillTo, pct) : Mathf.Lerp(BarFillFrom, BarFillTo, pct);
        else if (fillImage != null)
            fillImage.fillAmount = pct;
        else if (fillRect != null)
            fillRect.anchorMax = new Vector2(pct, fillRect.anchorMax.y);
    }

    // ---- The generated look ------------------------------------------------------------------------------------------

    private static bool IsPlaceholder(Sprite sprite) =>
        sprite == null || sprite.name.StartsWith("UI_White") || sprite.name == "UISprite" || sprite.name == "Background";

    private void Dress()
    {
        Image image = fillImage != null ? fillImage : fillRect != null ? fillRect.GetComponent<Image>() : null;
        if (image == null || !IsPlaceholder(image.sprite))
            return;
        var rect = (RectTransform)transform;
        Image background = transform.Find("Background") != null ? transform.Find("Background").GetComponent<Image>() : null;
        fill = image;

        bool fish = fillImage != null && fillImage.type == Image.Type.Filled && fillImage.fillMethod == Image.FillMethod.Vertical;
        if (drawnLook && DressDrawn(fish, image, background))
            return;
        if (fish)
        {
            shape = Shape.Fish;
            fillColor = generatedColor.a > 0f ? generatedColor : HudStyle.Hunger;
            rect.sizeDelta = new Vector2(48f * HudStyle.FishAspect, 48f);
            if (background != null)
            {
                background.sprite = HudStyle.FishBody;
                background.type = Image.Type.Simple;
                background.color = HudStyle.Ink;
            }
            fillImage.sprite = HudStyle.FishFill;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;   // from the tail
            Stretch(fillImage.rectTransform, 0f);
            Image overlay = NewImage("Overlay", HudStyle.FishOverlay, Color.white, false);
            overlay.transform.SetSiblingIndex(fillImage.transform.GetSiblingIndex() + 1);
            Beside(17);
        }
        else
        {
            shape = Shape.Bar;
            fillColor = generatedColor.a > 0f ? generatedColor : HudStyle.Health;
            if (background != null)
            {
                background.sprite = HudStyle.Pill;
                background.type = Image.Type.Sliced;
                background.color = HudStyle.Ink;
                Image rim = NewImage("Rim", HudStyle.PillRim, HudStyle.Rim, true);
                rim.transform.SetSiblingIndex(background.transform.GetSiblingIndex() + 1);
            }
            image.sprite = HudStyle.PillShaded;
            image.type = Image.Type.Sliced;
            if (fillRect != null)
            {
                // The pale chunk goes behind the fill, both inset a little from the rim.
                lost = NewImage("Lost", HudStyle.PillShaded, new Color(1f, 0.94f, 0.88f, 0.55f), true);
                lostRect = lost.rectTransform;
                lostRect.SetSiblingIndex(fillRect.GetSiblingIndex());
                Inset(lostRect, 2f);
                Inset(fillRect, 2f);
            }
            Beside(13);
        }
        fill.color = fillColor;
    }

    // The team's drawings, when they are in Resources/HUD. False = not there (the generated look goes on instead).
    private bool DressDrawn(bool fish, Image image, Image background)
    {
        Sprite back = Resources.Load<Sprite>(fish ? "HUD/HungerBarEmpty" : "HUD/HealthBarEmpty");
        Sprite front = Resources.Load<Sprite>(fish ? "HUD/HungerBarFill" : "HUD/HealthBarFill");
        if (back == null || front == null)
            return false;

        drawn = true;
        shape = fish ? Shape.Fish : Shape.Bar;
        fill = image;
        // A white copy of the fill (the green fish, the red bar), so it can take any colour: a tint only darkens, so
        // green could never turn red.
        front = Whitened(front);
        fillColor = fish ? FishColour(currentPct) : BarColour(currentPct);
        var rect = (RectTransform)transform;
        if (fish)
        {
            // Tall now: grows down and right from where its top left corner was.
            PivotKeepingPlace(rect, new Vector2(0f, 1f));
            rect.sizeDelta = new Vector2(drawnFishHeight * FishArt.x / FishArt.y, drawnFishHeight);
        }

        if (background == null)
        {
            background = NewImage("Background", back, Color.white, false);
            background.transform.SetAsFirstSibling();
        }
        background.sprite = back;
        background.type = Image.Type.Simple;
        background.color = Color.white;
        Stretch(background.rectTransform, 0f);

        image.sprite = front;
        image.type = Image.Type.Filled;
        // The fish curls round the picture's bottom right corner, mouth at the top, tail at the left: a quarter-circle
        // fill round that corner empties it from the mouth back along the body to the tail.
        image.fillMethod = fish ? Image.FillMethod.Radial90 : Image.FillMethod.Horizontal;
        image.fillOrigin = fish ? (int)Image.Origin90.BottomRight : (int)Image.OriginHorizontal.Left;
        image.fillClockwise = !fish || fishFillFromTail;
        image.color = fillColor;
        Stretch(image.rectTransform, 0f);
        drawnFill = image;

        if (!fish)
        {
            lost = NewImage("Lost", front, new Color(1f, 0.94f, 0.88f, 0.55f), false);
            lost.type = Image.Type.Filled;
            lost.fillMethod = Image.FillMethod.Horizontal;
            lost.fillOrigin = (int)Image.OriginHorizontal.Left;
            lost.transform.SetSiblingIndex(image.transform.GetSiblingIndex());
            drawnLost = lost;
        }

        if (valueLabel != null)
        {
            bool inFish = fish && numberInFish;
            valueLabel.enabled = drawnNumbers || inFish;
            if (inFish)
                NumberInFish();
            else if (drawnNumbers)
                Beside(fish ? 17 : 13);
        }
        MakeCaption(fish);
        ApplyPct(currentPct);
        return true;
    }

    // The colour at this much left: full, then half at 50 %, then empty from 15 % down, shading between.
    private static Color Graded(float pct, Color full, Color half, Color empty)
    {
        return pct >= 0.5f
            ? Color.Lerp(half, full, (pct - 0.5f) / 0.5f)
            : Color.Lerp(empty, half, Mathf.InverseLerp(0.15f, 0.5f, pct));
    }

    private Color FishColour(float pct)
    {
        Color c = Graded(pct, drawnFishFull, drawnFishHalf, drawnFishEmpty);
        c.a = fishSeeThrough;
        return c;
    }

    private Color BarColour(float pct)
    {
        Color c = Graded(pct, drawnBarFull, drawnBarHalf, drawnBarEmpty);
        c.a = 1f;
        return c;
    }

    // The same picture in white, keeping its shape (alpha), so the Image colour is the colour it shows. Needs the
    // texture readable (Read/Write on in its import settings); otherwise the picture as it is.
    private static Sprite Whitened(Sprite sprite)
    {
        Texture2D source = sprite.texture;
        if (!source.isReadable)
        {
            Debug.LogWarning($"{source.name}: turn on Read/Write in its import settings so the hunger fish can change colour.");
            return sprite;
        }
        Color32[] pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, pixels[i].a);
        var white = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false) { name = source.name + "_White", filterMode = source.filterMode, wrapMode = TextureWrapMode.Clamp };
        white.SetPixels32(pixels);
        white.Apply(false, true);
        Rect r = sprite.rect;
        return Sprite.Create(white, r, new Vector2(sprite.pivot.x / r.width, sprite.pivot.y / r.height), sprite.pixelsPerUnit);
    }

    // The meter's name, small, under it: under the fish's tail, or under the bar (past the heart). Hidden until wanted.
    private void MakeCaption(bool fish)
    {
        string name = !string.IsNullOrEmpty(drawnCaption) ? drawnCaption : fish ? "Hunger" : "Health";
        var go = new GameObject("Caption", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        caption = go.AddComponent<Text>();
        caption.text = name;
        caption.font = GameFont.Font;
        caption.fontStyle = FontStyle.Normal;
        caption.raycastTarget = false;
        caption.horizontalOverflow = HorizontalWrapMode.Overflow;
        caption.verticalOverflow = VerticalWrapMode.Overflow;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);
        outline.effectDistance = new Vector2(1f, -1f);
        RectTransform rect = caption.rectTransform;
        if (fish)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.2f, 0f);   // under the tail
            rect.pivot = new Vector2(0.5f, 1f);
            caption.alignment = TextAnchor.UpperCenter;
            caption.fontSize = 16;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = new Vector2(BarFillFrom, 0.12f);   // under the bar, from past the heart
            rect.pivot = new Vector2(0f, 1f);
            caption.alignment = TextAnchor.UpperLeft;
            caption.fontSize = 14;
        }
        rect.anchoredPosition = new Vector2(0f, -2f);
        rect.sizeDelta = new Vector2(120f, 20f);
        caption.color = new Color(HudStyle.Text.r, HudStyle.Text.g, HudStyle.Text.b, 0f);
        captionUntil = Time.time + captionSeconds;   // shown at the start
    }

    private void FadeCaption()
    {
        bool hovered = false;
        Mouse mouse = Mouse.current;
        if (Cursor.visible && mouse != null)
            hovered = RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, mouse.position.ReadValue(), null);
        bool wanted = hovered || Time.time < captionUntil;
        captionShown = Mathf.MoveTowards(captionShown, wanted ? 1f : 0f, Time.unscaledDeltaTime / 0.3f);
        Color c = caption.color;
        c.a = captionShown * 0.9f;
        caption.color = c;
        caption.enabled = captionShown > 0f;
    }

    // The hunger number inside the fish: white with a dark outline (it reads on any colour the fish turns), sized to
    // the fish, on top of it.
    private void NumberInFish()
    {
        RectTransform label = valueLabel.rectTransform;
        if (label.parent != transform)
            label.SetParent(transform, false);
        label.SetAsLastSibling();
        label.anchorMin = label.anchorMax = FishNumberAt;
        label.pivot = new Vector2(0.5f, 0.5f);
        label.anchoredPosition = Vector2.zero;
        label.localScale = Vector3.one;
        float height = ((RectTransform)transform).sizeDelta.y;
        label.sizeDelta = new Vector2(height * 0.6f, height * 0.25f);
        valueLabel.alignment = TextAnchor.MiddleCenter;
        valueLabel.fontSize = Mathf.RoundToInt(height * 0.15f);
        valueLabel.color = new Color(1f, 1f, 1f, drawnNumberAlpha);   // a bit see-through: part of the fish, not on it
        valueLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        valueLabel.verticalOverflow = VerticalWrapMode.Overflow;
        valueLabel.raycastTarget = false;
        foreach (Shadow old in valueLabel.GetComponents<Shadow>())
            Destroy(old);
        var outline = valueLabel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.05f, 0.06f, 0.35f * drawnNumberAlpha);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    // The heart and bar just past the drawn fish's head, sized to it (when the hunger fish is drawn too).
    private void BesideDrawnFish()
    {
        var rect = (RectTransform)transform;
        StatBarUI fishMeter = null;
        if (transform.parent != null)
            foreach (StatBarUI other in transform.parent.GetComponentsInChildren<StatBarUI>(true))
                if (other.drawn && other.shape == Shape.Fish && other.transform.parent == transform.parent)
                    fishMeter = other;
        if (fishMeter == null)
        {
            PivotKeepingPlace(rect, new Vector2(0f, 1f));
            rect.sizeDelta = new Vector2(28f * BarArt.x / BarArt.y, 28f);
            return;
        }
        var fishRect = (RectTransform)fishMeter.transform;
        float k = fishRect.sizeDelta.y / FishArt.y;   // canvas units per pixel of the drawings
        rect.anchorMin = rect.anchorMax = fishRect.anchorMin;
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = BarArt * k;
        Vector2 fishTopLeft = fishRect.anchoredPosition + new Vector2(-fishRect.pivot.x * fishRect.sizeDelta.x, (1f - fishRect.pivot.y) * fishRect.sizeDelta.y);
        rect.anchoredPosition = fishTopLeft + new Vector2(BarBesideFish.x, BarBesideFish.y) * k;
    }

    // A new pivot with the rect staying where it is on screen (the HUD's rects are anchored to a point).
    private static void PivotKeepingPlace(RectTransform rect, Vector2 pivot)
    {
        Vector2 shift = pivot - rect.pivot;
        rect.pivot = pivot;
        rect.anchoredPosition += Vector2.Scale(shift, rect.sizeDelta);
    }

    // Under the fish-shaped hunger gauge, when there is one next to it: the two read as one cluster.
    private void TuckUnderFish()
    {
        if (transform.parent == null)
            return;
        foreach (StatBarUI other in transform.parent.GetComponentsInChildren<StatBarUI>(true))
        {
            if (other.shape != Shape.Fish || other.transform.parent != transform.parent)
                continue;
            var fishRect = (RectTransform)other.transform;
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = fishRect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            float fishTop = fishRect.anchoredPosition.y + (1f - fishRect.pivot.y) * fishRect.sizeDelta.y;
            float fishLeft = fishRect.anchoredPosition.x - fishRect.pivot.x * fishRect.sizeDelta.x;
            rect.sizeDelta = new Vector2(fishRect.sizeDelta.x - 10f, 9f);
            rect.anchoredPosition = new Vector2(fishLeft + 8f, fishTop - fishRect.sizeDelta.y - 8f);
            return;
        }
    }

    // The number sits just right of the meter, in the HUD font with a shadow.
    private void Beside(int size)
    {
        if (valueLabel == null)
            return;
        RectTransform label = valueLabel.rectTransform;
        if (label.parent == transform)
        {
            label.anchorMin = label.anchorMax = new Vector2(1f, 0.5f);
            label.pivot = new Vector2(0f, 0.5f);
            label.anchoredPosition = new Vector2(10f, 0f);
            label.sizeDelta = new Vector2(80f, size + 12f);
        }
        valueLabel.alignment = TextAnchor.MiddleLeft;
        valueLabel.fontSize = size;
        valueLabel.color = shape == Shape.Fish ? HudStyle.Text : HudStyle.Muted;
        valueLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (valueLabel.GetComponent<Shadow>() == null)
        {
            var shadow = valueLabel.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(1f, -1.5f);
        }
    }

    private Image NewImage(string name, Sprite sprite, Color color, bool sliced)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        Stretch(image.rectTransform, 0f);
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Inset(RectTransform rect, float inset)
    {
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
