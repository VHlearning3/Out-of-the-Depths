using UnityEngine;
using UnityEngine.UI;

public class StatBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private Text valueLabel;

    private void Awake()
    {
        if (fillRect == null)
            Debug.LogError($"{name}: StatBarUI has no Fill Rect assigned, the bar will not update.", this);
    }

    public void SetValue(float current, float max)
    {
        if (fillRect != null)
        {
            float pct = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            fillRect.anchorMax = new Vector2(pct, fillRect.anchorMax.y);
        }

        if (valueLabel != null)
            valueLabel.text = $"{current:0}/{max:0}";
    }
}
