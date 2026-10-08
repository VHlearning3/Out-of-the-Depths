using System.Collections;
using UnityEngine;

// The finished stone tablet going into the podium's hole once the tablet puzzle is solved. When the board closes
// (the Puzzle Station's On Solved) the tablet pops up where the board was, in front of the camera, glides over to the
// podium turning to face out, lines up in front of the hole, slides in and seats itself with a click, a sound and a
// green flare of light. Solved already when the scene starts (a checkpoint): it simply sits in its hole.
// Tablet is the tablet at its resting spot in the hole; PodiumModelTools puts it there and fits it to the hole
// (Tools > Out of the Depths > Use Podium For Pedestals). It is hidden until the puzzle is solved.
public class PuzzleSolvedTablet : MonoBehaviour
{
    [Tooltip("The tablet, placed where it ends up (in the podium's hole). Hidden until the puzzle is solved.")]
    [SerializeField] private Transform tablet;
    [Tooltip("The station whose puzzle this is. Empty = the one on this object.")]
    [SerializeField] private PuzzleStation station;

    [Header("Appearing")]
    [Tooltip("Pause after the board closes before the tablet appears.")]
    [SerializeField, Min(0f)] private float startDelay = 0.1f;
    [Tooltip("How far in front of the camera it appears, in metres.")]
    [SerializeField] private float showDistance = 0.75f;
    [Tooltip("Its size there, compared to its size in the hole.")]
    [SerializeField, Range(0.1f, 1f)] private float showScale = 0.6f;
    [Tooltip("How long it shows itself in front of the camera before it sets off.")]
    [SerializeField, Min(0f)] private float showDuration = 0.5f;

    [Header("Flying to the podium")]
    [SerializeField, Min(0.05f)] private float flyDuration = 1.1f;
    [Tooltip("Height of the arc it travels on, in metres.")]
    [SerializeField] private float arcHeight = 0.25f;
    [Tooltip("Turns it makes round its face on the way, slowing as it lines up with the hole. 0 = none.")]
    [SerializeField] private float spins = 1f;
    [Tooltip("How far in front of the hole it lines up before sliding in, in metres.")]
    [SerializeField] private float approachDistance = 0.3f;
    [Tooltip("Further than this from the camera (a solve from the admin page) it skips the flight and appears in front of the hole.")]
    [SerializeField] private float maxFlyDistance = 8f;

    [Header("Going in")]
    [SerializeField, Min(0.05f)] private float insertDuration = 0.4f;
    [Tooltip("How far the click at the end pushes it in and back, in metres.")]
    [SerializeField] private float clickDepth = 0.008f;
    [SerializeField] private AudioClip seatSound;
    [SerializeField, Range(0f, 1f)] private float seatVolume = 0.8f;

    [Header("Glow when it is in")]
    [Tooltip("The flare's colour (the gem's green). Intensity 0 = no flare.")]
    [SerializeField] private Color glowColor = new Color(0.35f, 1f, 0.6f);
    [SerializeField, Min(0f)] private float glowIntensity = 3f;
    [SerializeField, Min(0f)] private float glowRange = 1.6f;
    [SerializeField, Min(0.05f)] private float glowDuration = 1.4f;

    public bool IsInPlace { get; private set; }

    private Vector3 restLocalPosition;
    private Quaternion restLocalRotation;
    private Vector3 restLocalScale;
    private bool started;   // in its hole or on its way there
    private GameObject glow;

    private void Awake()
    {
        if (station == null)
            station = GetComponentInParent<PuzzleStation>();
        if (tablet == null)
            tablet = transform.Find("Tablet");
        if (tablet == null)
            return;
        restLocalPosition = tablet.localPosition;
        restLocalRotation = tablet.localRotation;
        restLocalScale = tablet.localScale;
        tablet.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (station != null)
            station.onSolved.AddListener(OnSolved);
    }

    // Switched off halfway (a scene change): no tablet left hanging in the water, no flare left burning.
    private void OnDisable()
    {
        if (station != null)
            station.onSolved.RemoveListener(OnSolved);
        if (glow != null)
            Destroy(glow);
        if (started && !IsInPlace && tablet != null)
        {
            tablet.SetLocalPositionAndRotation(restLocalPosition, restLocalRotation);
            tablet.localScale = restLocalScale;
            IsInPlace = true;
        }
    }

    // A checkpoint brings it back solved without the event: straight into the hole.
    private void Update()
    {
        if (!started && station != null && station.IsSolved)
            PutInPlace();
    }

    private void OnSolved()
    {
        if (started || tablet == null)
            return;
        started = true;
        StartCoroutine(Place());
    }

    private void PutInPlace()
    {
        started = true;
        StopAllCoroutines();
        if (tablet == null)
            return;
        tablet.gameObject.SetActive(true);
        tablet.SetLocalPositionAndRotation(restLocalPosition, restLocalRotation);
        tablet.localScale = restLocalScale;
        IsInPlace = true;
    }

    private Vector3 RestPosition => tablet.parent != null ? tablet.parent.TransformPoint(restLocalPosition) : restLocalPosition;
    private Quaternion RestRotation => tablet.parent != null ? tablet.parent.rotation * restLocalRotation : restLocalRotation;

    private IEnumerator Place()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // The tablet's front (its +Z, the face with the gem) looks out of the hole.
        Vector3 rest = RestPosition;
        Quaternion restRotation = RestRotation;
        Vector3 outOfHole = restRotation * Vector3.forward;
        Vector3 approach = rest + outOfHole * approachDistance;

        Camera camera = Camera.main;
        bool fly = camera != null && Vector3.Distance(camera.transform.position, rest) <= maxFlyDistance;

        Vector3 from = approach;
        Quaternion fromRotation = restRotation;
        if (fly)
        {
            // Where the board was: in front of the camera, face towards you.
            Transform eye = camera.transform;
            from = eye.position + eye.forward * showDistance - eye.up * 0.03f;
            fromRotation = Quaternion.LookRotation(-eye.forward, eye.up);
        }

        tablet.gameObject.SetActive(true);
        tablet.SetPositionAndRotation(from, fromRotation);

        // Pops up, bobbing gently.
        float shownScale = fly ? showScale : 1f;
        for (float time = 0f; time < showDuration; time += Time.deltaTime)
        {
            float k = time / showDuration;
            tablet.localScale = restLocalScale * (shownScale * Ease.OutBack(k));
            tablet.position = from + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.02f);
            yield return null;
        }
        tablet.localScale = restLocalScale * shownScale;
        tablet.position = from;

        // Over to the podium on an arc, growing to full size, turning (and spinning down) to line up with the hole.
        if (fly)
        {
            for (float time = 0f; time < flyDuration; time += Time.deltaTime)
            {
                float k = Ease.InOutCubic(time / flyDuration);
                Vector3 position = Vector3.Lerp(from, approach, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * arcHeight);
                Quaternion rotation = Quaternion.Slerp(fromRotation, restRotation, k);
                if (spins != 0f)
                    rotation *= Quaternion.AngleAxis(spins * 360f * (1f - k), Vector3.forward);
                tablet.SetPositionAndRotation(position, rotation);
                tablet.localScale = restLocalScale * Mathf.Lerp(showScale, 1f, k);
                yield return null;
            }
        }
        tablet.SetPositionAndRotation(approach, restRotation);
        tablet.localScale = restLocalScale;

        // Straight in along the hole's axis, speeding up a little at the end so it lands with a click.
        for (float time = 0f; time < insertDuration; time += Time.deltaTime)
        {
            float k = Ease.InQuad(time / insertDuration);
            tablet.position = Vector3.Lerp(approach, rest, k);
            yield return null;
        }
        tablet.position = rest;

        if (seatSound != null)
            SoundVariety.PlayAt(seatSound, rest, seatVolume);
        if (glowIntensity > 0f && glowRange > 0f)
            StartCoroutine(Glow(rest + outOfHole * 0.15f));

        // The click: pushed in a touch and back.
        const float click = 0.15f;
        for (float time = 0f; time < click; time += Time.deltaTime)
        {
            tablet.position = rest - outOfHole * (clickDepth * Mathf.Sin(time / click * Mathf.PI));
            yield return null;
        }
        tablet.SetLocalPositionAndRotation(restLocalPosition, restLocalRotation);
        tablet.localScale = restLocalScale;
        IsInPlace = true;
    }

    // A soft flare in front of the tablet that fades away.
    private IEnumerator Glow(Vector3 at)
    {
        if (glow != null)
            Destroy(glow);
        var go = glow = new GameObject("TabletGlow");
        go.transform.SetParent(transform, true);
        go.transform.position = at;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = glowColor;
        light.range = glowRange;
        light.shadows = LightShadows.None;
        light.intensity = 0f;

        const float rise = 0.12f;
        for (float time = 0f; time < rise + glowDuration; time += Time.deltaTime)
        {
            light.intensity = time < rise
                ? glowIntensity * (time / rise)
                : glowIntensity * (1f - Ease.OutQuad((time - rise) / glowDuration));
            yield return null;
        }
        Destroy(go);
        glow = null;
    }
}
