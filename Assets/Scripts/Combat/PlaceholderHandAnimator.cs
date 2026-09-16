using UnityEngine;

// Stand-in for the hand-drawn flipbook hands. Swap this component for one that
// plays sprite frames; SlashAttack only talks to IHandAnimator, so nothing else changes.
public class PlaceholderHandAnimator : MonoBehaviour, IHandAnimator
{
    [SerializeField] private Transform handTransform;
    [SerializeField] private float swingAngle = 70f;
    [SerializeField] private float swingDuration = 0.25f;

    private Quaternion restRotation;
    private float swingTimer = -1f;

    private void Awake()
    {
        if (handTransform == null)
            handTransform = transform;

        restRotation = handTransform.localRotation;
    }

    public void PlaySlash()
    {
        swingTimer = 0f;
    }

    private void Update()
    {
        if (swingTimer < 0f)
            return;

        swingTimer += Time.deltaTime;
        float t = Mathf.Clamp01(swingTimer / swingDuration);
        float angle = Mathf.Sin(t * Mathf.PI) * swingAngle;
        handTransform.localRotation = restRotation * Quaternion.Euler(angle, 0f, 0f);

        if (t >= 1f)
        {
            handTransform.localRotation = restRotation;
            swingTimer = -1f;
        }
    }
}
