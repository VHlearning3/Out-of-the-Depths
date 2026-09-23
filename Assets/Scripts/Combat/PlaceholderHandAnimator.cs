using UnityEngine;

// Stand-in for the hand-drawn flipbook hands. Swap this component for one that
// plays sprite frames; SlashAttack only talks to IHandAnimator, so nothing else changes.
// Until then it turns the hand (and the weapon in it) along each slash: a left-right slash sweeps it from right to
// left, an up-down one chops it from high to low, each with a short wind-up and a return to rest.
public class PlaceholderHandAnimator : MonoBehaviour, IDirectionalHandAnimator
{
    [SerializeField] private Transform handTransform;
    [Tooltip("Degrees the hand turns each way of the middle: the left-right sweep and the up-down chop.")]
    [SerializeField] private float swingAngle = 70f;
    [Tooltip("The whole move, wind-up, strike and return, in seconds.")]
    [SerializeField] private float swingDuration = 0.25f;

    private const float WindUp = 0.2f;    // of Swing Duration
    private const float Strike = 0.45f;   // of Swing Duration; the rest is the return

    private Quaternion restRotation;
    private float swingTimer = -1f;
    private SlashAttack.Direction direction = SlashAttack.Direction.UpDown;

    private void Awake()
    {
        if (handTransform == null)
            handTransform = transform;

        restRotation = handTransform.localRotation;
    }

    public void PlaySlash() => PlaySlash(SlashAttack.Direction.UpDown);

    public void PlaySlash(SlashAttack.Direction slash)
    {
        direction = slash;
        swingTimer = 0f;
    }

    private void Update()
    {
        if (swingTimer < 0f)
            return;

        swingTimer += Time.deltaTime;
        float t = Mathf.Clamp01(swingTimer / Mathf.Max(0.01f, swingDuration));

        // -1 = the start of the arc (right, or high), +1 = its end (left, or low), 0 = rest.
        float along;
        if (t < WindUp)
            along = -Mathf.SmoothStep(0f, 1f, t / WindUp);
        else if (t < WindUp + Strike)
            along = Mathf.Lerp(-1f, 1f, Ease.OutCubic((t - WindUp) / Strike));
        else
            along = 1f - Mathf.SmoothStep(0f, 1f, (t - WindUp - Strike) / (1f - WindUp - Strike));

        float half = swingAngle * 0.6f;
        Quaternion turn = direction == SlashAttack.Direction.LeftRight
            ? Quaternion.Euler(0f, -along * half, along * 20f)   // right to left, rolling the edge into it
            : Quaternion.Euler(along * half, 0f, 0f);            // high to low
        handTransform.localRotation = restRotation * turn;

        if (t >= 1f)
        {
            handTransform.localRotation = restRotation;
            swingTimer = -1f;
        }
    }
}
