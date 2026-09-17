using UnityEngine;
using UnityEngine.UI;

// A HUD meter: give it the Fill rect (a bar) or a Filled Image (any shape, e.g. the fish-shaped hunger gauge) and an
// optional label, then call SetValue(current, max).
public class StatBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [Tooltip("Instead of Fill Rect: an Image with Image Type = Filled. Its Fill Amount is driven, so the meter can be any sprite shape.")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Text valueLabel;
    [Tooltip("How quickly the bar visually catches up to its real value. 0 = instant.")]
    [SerializeField] private float smoothSpeed = 8f;

    private float targetPct = 1f;
    private float currentPct = 1f;

    private void Awake()
    {
        if (fillRect == null && fillImage == null)
            Debug.LogError($"{name}: StatBarUI has neither a Fill Rect nor a Fill Image assigned, the meter will not update.", this);
    }

    public void SetValue(float current, float max)
    {
        targetPct = max > 0f ? Mathf.Clamp01(current / max) : 0f;

        if (valueLabel != null)
            valueLabel.text = $"{current:0}/{max:0}";

        if (smoothSpeed <= 0f)
            ApplyPct(targetPct);
    }

    private void Update()
    {
        if (smoothSpeed <= 0f || Mathf.Approximately(currentPct, targetPct))
            return;

        ApplyPct(Mathf.Lerp(currentPct, targetPct, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)));
    }

    private void ApplyPct(float pct)
    {
        currentPct = pct;
        if (fillImage != null)
            fillImage.fillAmount = pct;
        else if (fillRect != null)
            fillRect.anchorMax = new Vector2(pct, fillRect.anchorMax.y);
    }
}
