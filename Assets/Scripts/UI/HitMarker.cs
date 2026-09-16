using UnityEngine;
using UnityEngine.UI;

// Punches the reticle out and tints it for a moment. Swap the visuals freely; SlashAttack only calls Show().
public class HitMarker : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private Graphic graphic;
    [SerializeField] private float punchScale = 2.5f;
    [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float duration = 0.15f;

    private Vector3 restScale;
    private Color restColor;
    private float timer = -1f;

    private void Awake()
    {
        if (target == null)
            target = GetComponent<RectTransform>();
        if (graphic == null)
            graphic = GetComponent<Graphic>();

        restScale = target.localScale;
        if (graphic != null)
            restColor = graphic.color;
    }

    public void Show()
    {
        timer = 0f;
    }

    private void Update()
    {
        if (timer < 0f)
            return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
        target.localScale = restScale * Mathf.Lerp(punchScale, 1f, t);
        if (graphic != null)
            graphic.color = Color.Lerp(hitColor, restColor, t);

        if (t >= 1f)
            timer = -1f;
    }
}
