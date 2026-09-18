using UnityEngine;
using UnityEngine.UI;

// Red pulse over the screen that beats faster and harder the closer the chase pack is (reads the running Chase Sequence).
// Lives on a stretched RectTransform under the HUD canvas (Tools → Out of the Depths → Add Chase Danger HUD To Open Scene);
// the Image is made at runtime when none is set, so it needs no sprite.
public class ChaseDangerUI : MonoBehaviour
{
    [Tooltip("Empty = a plain full-screen Image is made under this object at start.")]
    [SerializeField] private Image image;
    [SerializeField] private Color color = new Color(0.75f, 0.05f, 0.05f);
    [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.3f;
    [Tooltip("Beats per second when the pack is far away / right behind you.")]
    [SerializeField] private float slowBeat = 0.9f;
    [SerializeField] private float fastBeat = 2.4f;

    private float danger;
    private float phase;

    private void Awake()
    {
        if (image == null)
        {
            var go = new GameObject("Tint", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            image = go.AddComponent<Image>();
        }
        image.raycastTarget = false;
        image.color = new Color(color.r, color.g, color.b, 0f);
        image.enabled = false;
    }

    private void Update()
    {
        float target = ChaseSequence.Active != null ? ChaseSequence.Active.Danger01 : 0f;
        danger = Mathf.Lerp(danger, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
        phase += Time.deltaTime * Mathf.Lerp(slowBeat, fastBeat, danger);

        // A thump rather than a sine: sharp rise, quiet gap.
        float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f)), 3f);
        float alpha = danger * maxAlpha * (0.35f + 0.65f * beat);
        image.enabled = alpha > 0.003f;
        image.color = new Color(color.r, color.g, color.b, alpha);
    }
}
