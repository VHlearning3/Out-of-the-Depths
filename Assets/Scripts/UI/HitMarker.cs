using UnityEngine;
using UnityEngine.UI;

// The reticle. Punches out and tints for a moment on a hit (SlashAttack only calls Show()). With Generated Look, a
// reticle that is still the plain white square becomes a small round dot with a soft dark halo (it reads on bright and
// dark water alike), and a thin ring eases in round it while the dot is on something you can use (E). It fades out
// during the chase's reveal cutscene. Swap the visuals freely.
public class HitMarker : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField] private Graphic graphic;
    [SerializeField] private float punchScale = 2.5f;
    [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float duration = 0.15f;
    [Tooltip("A round dot with a halo instead of the white square, and a ring round it while you look at something you can use.")]
    [SerializeField] private bool generatedLook = true;
    [Tooltip("Dot size and the ring's size, in canvas units.")]
    [SerializeField] private float dotSize = 11f;
    [SerializeField] private float ringSize = 26f;

    private Vector3 restScale;
    private Color restColor;
    private float timer = -1f;
    private static HitMarker instance;

    private Image ring;
    private float ringShown;
    private float shown = 1f;   // 0 during the chase cutscene
    private PlayerInteractor interactor;

    private void Awake()
    {
        instance = this;
        if (target == null)
            target = GetComponent<RectTransform>();
        if (graphic == null)
            graphic = GetComponent<Graphic>();

        if (generatedLook && graphic is Image image && (image.sprite == null || image.sprite.name.StartsWith("UI_White")))
            Dress(image);

        restScale = target.localScale;
        if (graphic != null)
            restColor = graphic.color;
    }

    private void Dress(Image image)
    {
        image.sprite = HudStyle.Dot;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.color = Color.white;
        target.sizeDelta = new Vector2(dotSize, dotSize);

        var go = new GameObject("TargetRing", typeof(RectTransform));
        go.transform.SetParent(target, false);
        ring = go.AddComponent<Image>();
        ring.sprite = HudStyle.Ring;
        ring.raycastTarget = false;
        ring.color = new Color(1f, 1f, 1f, 0f);
        RectTransform rect = ring.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ringSize, ringSize);
    }

    public void Show()
    {
        timer = 0f;
    }

    private void Update()
    {
        // Hidden while the chase's reveal takes the camera.
        ChaseSequence chase = ChaseSequence.Active;
        bool cutscene = chase != null && chase.InCutscene;
        shown = Mathf.MoveTowards(shown, cutscene ? 0f : 1f, Time.unscaledDeltaTime / 0.25f);

        if (ring != null)
        {
            if (interactor == null)
                interactor = FindFirstObjectByType<PlayerInteractor>();
            bool targeted = interactor != null && interactor.CurrentTarget != null && !interactor.Busy && !interactor.PromptHidden;
            ringShown = Mathf.MoveTowards(ringShown, targeted ? 1f : 0f, Time.unscaledDeltaTime / (targeted ? 0.12f : 0.2f));
            float eased = ringShown * ringShown * (3f - 2f * ringShown);
            ring.color = new Color(1f, 1f, 1f, 0.6f * eased * shown);
            float size = Mathf.Lerp(0.6f, 1f, eased);
            ring.rectTransform.localScale = new Vector3(size, size, 1f);
        }

        if (timer < 0f)
        {
            if (graphic != null)
                graphic.color = new Color(restColor.r, restColor.g, restColor.b, restColor.a * shown);
            return;
        }

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / Mathf.Max(0.01f, duration));
        target.localScale = restScale * Mathf.Lerp(punchScale, 1f, t);
        if (graphic != null)
        {
            Color c = Color.Lerp(hitColor, restColor, t);
            graphic.color = new Color(c.r, c.g, c.b, c.a * shown);
        }

        if (t >= 1f)
            timer = -1f;
    }

    // Hide / show the reticle (a pickup being inspected). The hit punch keeps working underneath.
    public static void SetReticleVisible(bool visible)
    {
        if (instance == null)
            instance = FindFirstObjectByType<HitMarker>(FindObjectsInactive.Include);
        if (instance == null)
            return;
        if (instance.graphic != null)
            instance.graphic.enabled = visible;
        if (instance.ring != null)
            instance.ring.enabled = visible;
    }
}
