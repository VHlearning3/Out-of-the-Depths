using UnityEngine;
using UnityEngine.UI;

// A HUD meter: give it the Fill rect (a bar) or a Filled Image (any shape, e.g. the fish-shaped hunger gauge) and an
// optional label, then call SetValue(current, max). With Auto Hide it fades away while it is not needed: it shows
// while the value is under Show Below, and for a few seconds after it jumps (a bite, eating, a dash).
// Generated Look (on by default) dresses a meter that still shows the plain white placeholder in the HUD's own style
// (HudStyle): the hunger gauge (a Filled Image filling upwards) becomes a fish that fills from its tail, with the
// number beside it, and a bar becomes a slim rounded pill with a pale chunk showing what the last hit took, tucked
// under the fish. Under Low Share the fill turns coral and pulses. A meter with real art keeps it untouched.
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
            TuckUnderFish();
    }

    public void SetValue(float current, float max)
    {
        float was = targetPct;
        targetPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        if (fade != null && Mathf.Abs(targetPct - was) > jumpToShow)
            fade.Wake();
        if (targetPct < was - 0.001f)
            droppedAt = Time.time;

        if (valueLabel != null)
            valueLabel.text = shape != Shape.None ? $"{current:0}" : $"{current:0}/{max:0}";

        if (smoothSpeed <= 0f)
            ApplyPct(targetPct);
    }

    private void Update()
    {
        if (smoothSpeed > 0f && !Mathf.Approximately(currentPct, targetPct))
            ApplyPct(Mathf.Lerp(currentPct, targetPct, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)));
        if (shape == Shape.None || fill == null)
            return;

        // Low: coral, breathing.
        bool low = targetPct < lowShare;
        Color colour = low ? HudStyle.Danger : fillColor;
        float pulse = low ? 0.78f + 0.22f * Mathf.Sin(Time.time * 5f) : 1f;
        fill.color = new Color(colour.r * pulse, colour.g * pulse, colour.b * pulse, 1f);

        // The pale chunk behind the fill: waits a moment after a hit, then drains to the fill.
        if (lostRect != null)
        {
            if (currentPct >= trailing)
                trailing = currentPct;
            else if (Time.time - droppedAt > 0.4f)
                trailing = Mathf.MoveTowards(trailing, currentPct, Time.deltaTime * 0.6f);
            lostRect.anchorMax = new Vector2(trailing, lostRect.anchorMax.y);
            lost.enabled = trailing > currentPct + 0.003f;
        }
    }

    private void ApplyPct(float pct)
    {
        currentPct = pct;
        if (fillImage != null)
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
