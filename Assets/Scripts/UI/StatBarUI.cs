using UnityEngine;
using UnityEngine.UI;

// A HUD bar: give it the Fill rect and an optional label, then call SetValue(current, max).
public class StatBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Text valueLabel;
    [Tooltip("How quickly the bar visually catches up to its real value. 0 = instant.")]
    [SerializeField] private float smoothSpeed = 8f;

    private float targetPct = 1f;

    private void Awake()
    {
        if (fillRect == null)
            Debug.LogError($"{name}: StatBarUI has no Fill Rect assigned, the bar will not update.", this);
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
        if (fillRect == null || smoothSpeed <= 0f)
            return;

        float current = fillRect.anchorMax.x;
        if (Mathf.Approximately(current, targetPct))
            return;

        ApplyPct(Mathf.Lerp(current, targetPct, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime)));
    }

    private void ApplyPct(float pct)
    {
        if (fillRect != null)
            fillRect.anchorMax = new Vector2(pct, fillRect.anchorMax.y);
    }
}
