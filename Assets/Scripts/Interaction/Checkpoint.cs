using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Checkpoint pillar: press E on it to make it the active respawn point. The book on top hovers, always turned to face
// you; activating the pillar swings the book shut on its spine and sends it flying up into the water while the pillar lights up from
// the bottom: a glowing ring and the light climb from the base to the cap as the glow ramps up, and everything flares
// when they reach the top; the pillar pops and sparkles burst. While active it breathes light. When another checkpoint takes over, the book
// comes back. You respawn where you stood when you activated it (pushed out to Respawn Distance from the pillar, never
// inside it), unless Respawn Point is set. The active one can't be re-activated until another takes over.
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour, IInteractable
{
    [Header("Respawn")]
    [Tooltip("Where the player reappears. Empty = where they stood when they activated this, at least Respawn Distance from the pillar.")]
    [SerializeField] private Transform respawnPoint;
    [Tooltip("With no Respawn Point set: how far from the pillar's centre the player reappears at the least.")]
    [SerializeField] private float respawnDistance = 1.6f;
    [Tooltip("Treat this as already active at start (no sound, no book). Use for the initial spawn.")]
    [SerializeField] private bool startsActivated = false;
    [SerializeField] private string prompt = "activate checkpoint";

    [Header("Look")]
    [Tooltip("The model that pops on activation. Empty = the first child.")]
    [SerializeField] private Transform visual;
    [Tooltip("A light that comes on with the checkpoint (optional).")]
    [SerializeField] private Light glowLight;
    [SerializeField] private float activeLightIntensity = 2.5f;
    [SerializeField] private float inactiveLightIntensity = 0.2f;
    [SerializeField] private Color activeEmission = new Color(0.3f, 1.5f, 1.5f);
    [SerializeField] private Color inactiveEmission = new Color(0.05f, 0.25f, 0.25f);
    [Tooltip("While active the glow breathes by this much (0 = steady).")]
    [SerializeField, Range(0f, 1f)] private float breathe = 0.2f;
    [SerializeField] private float breatheSpeed = 1.2f;

    [Header("The book")]
    [Tooltip("The part that floats (the book on top). Empty = nothing hovers.")]
    [SerializeField] private Transform hover;
    [Tooltip("How far it bobs up and down, in the model's own units.")]
    [SerializeField] private float hoverAmount = 0.15f;
    [Tooltip("Bobs per second.")]
    [SerializeField] private float hoverSpeed = 0.5f;
    [Tooltip("Turn to face the player. Off = it just turns slowly on the spot.")]
    [SerializeField] private bool faceThePlayer = true;
    [Tooltip("Added to the facing: 180 = the model's front is its -Z side (the checkpoint book), 0 = its +Z side.")]
    [SerializeField] private float faceYawOffset = 0f;
    [Tooltip("Degrees per second it turns when not facing the player.")]
    [SerializeField] private float hoverSpin = 20f;

    [Header("The book flies away")]
    [Tooltip("On activation the book snaps shut and flies up into the water. Off = it stays and hovers.")]
    [SerializeField] private bool flyOnActivate = true;
    [Tooltip("Measure the halves at start (BookHinge) and work out the spine, the turn and the facing from their meshes; the numbers below are then only the fallback. Off = use the numbers below as typed.")]
    [SerializeField] private bool measureBook = true;
    [Tooltip("The book is two halves (children of the book named left / right) hinged on the spine: each turns this far to close.")]
    [SerializeField] private float bookCloseAngle = 70f;
    [Tooltip("Which way round the spine the halves turn. Use Checkpoint Model measures it; flip it if the book shuts inside out, pages on the outside.")]
    [SerializeField] private bool bookCloseFlip = false;
    [Tooltip("The spine, bottom and top, in the book model's own coordinates (the hinge the halves turn on).")]
    [SerializeField] private Vector3 bookSpineBottom = new Vector3(0f, 3.955f, -0.32f);
    [SerializeField] private Vector3 bookSpineTop = new Vector3(0.01f, 5.18f, 0.73f);
    [SerializeField] private float bookCloseTime = 0.35f;
    [SerializeField] private float bookFlyTime = 1.4f;
    [Tooltip("How high it flies before it is gone.")]
    [SerializeField] private float bookFlyHeight = 14f;
    [Tooltip("Degrees per second it spins on the way up.")]
    [SerializeField] private float bookFlySpin = 540f;

    [Header("Lighting up")]
    [Tooltip("On activation the glow climbs the pillar from the bottom over this long. 0 = as long as the book takes to close and fly.")]
    [SerializeField] private float lightUpTime = 0f;
    [Tooltip("Where the climb ends, in metres above the pillar's base. 0 = the top of the pillar (72% of the trigger's height, under the book).")]
    [SerializeField] private float lightUpTop = 0f;
    [Tooltip("A glowing ring that rides up the pillar with the light.")]
    [SerializeField] private bool lightUpRing = true;

    [Header("Activation")]
    [SerializeField] private AudioClip activateSound;
    [SerializeField, Range(0f, 1f)] private float activateVolume = 0.7f;
    [Tooltip("The glow flares to this many times the active colour on activation and settles over Flash Duration.")]
    [SerializeField] private float activateFlash = 3f;
    [SerializeField] private float flashDuration = 0.8f;
    [Tooltip("The pillar swells to this size and settles back.")]
    [SerializeField] private float popScale = 1.15f;
    [SerializeField] private float popDuration = 0.5f;
    [Tooltip("Sparkles that burst out on activation (the placeholder particle prefab). Optional.")]
    [SerializeField] private ParticleSystem activateVfx;
    [SerializeField] private int vfxBurst = 40;

    [Header("Events")]
    public UnityEvent onActivated = new UnityEvent();

    public static Checkpoint Current { get; private set; }
    public bool IsActive => Current == this;
    public string Prompt => prompt;

    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Vector3 visualScale = Vector3.one;
    private Vector3 hoverRestPosition;
    private Quaternion hoverRestRotation;
    private Vector3 hoverRestScale = Vector3.one;
    private Transform bookLeft;
    private Transform bookRight;
    private BookHinge hinge;        // the measured numbers (the slide needs the page directions and thickness)
    private bool hingeMeasured;
    private Vector3 leftRestPosition, rightRestPosition;
    private Quaternion leftRestRotation, rightRestRotation;
    private float hoverPhase;
    private bool bookGone;
    private bool bookBusy;   // closing, flying or coming back: the hover loop leaves it alone
    private Transform anchor;
    private Coroutine glow;
    private Coroutine ignite;
    private Vector3 lightRestPosition;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        if (visual != null)
            visualScale = visual.localScale;
        if (glowLight != null)
            lightRestPosition = glowLight.transform.localPosition;
        if (hover != null)
        {
            hoverRestPosition = hover.localPosition;
            hoverRestRotation = hover.localRotation;
            hoverRestScale = hover.localScale;
            hoverPhase = Random.value * 10f;
            bookLeft = FindPart(hover, "left");
            bookRight = FindPart(hover, "right");
            if (bookLeft != null)
            {
                leftRestPosition = bookLeft.localPosition;
                leftRestRotation = bookLeft.localRotation;
            }
            if (bookRight != null)
            {
                rightRestPosition = bookRight.localPosition;
                rightRestRotation = bookRight.localRotation;
            }
            if (measureBook && bookLeft != null && bookRight != null)
            {
                if (BookHinge.Measure(hover, out BookHinge measured, out string problem))
                {
                    hinge = measured;
                    hingeMeasured = true;
                    bookSpineBottom = measured.spineBottom;
                    bookSpineTop = measured.spineTop;
                    bookCloseAngle = measured.closeAngle;
                    bookCloseFlip = measured.flip;
                    faceYawOffset = measured.faceYawOffset;
                }
                else
                    Debug.LogWarning($"{name}: the book keeps its Inspector hinge numbers, it could not be measured: {problem}.", this);
            }
            StartCoroutine(Hover());
        }

        if (startsActivated && Current == null)
            Current = this;

        ApplyState(false);
    }

    public void Interact(GameObject interactor)
    {
        if (IsActive)
            return;

        var death = interactor.GetComponentInParent<DeathManager>();
        if (death == null)
            return;

        Activate(death, interactor.transform);
    }

    // Activate it from an event (the rune lock solved just before the chase): the player respawns where they are now.
    public void ActivateNow()
    {
        if (IsActive)
            return;
        var death = FindFirstObjectByType<DeathManager>();
        if (death != null)
            Activate(death, death.transform);
    }

    private void Activate(DeathManager death, Transform player)
    {
        Checkpoint previous = Current;
        Current = this;
        if (previous != null)
            previous.ApplyState(false);

        death.SetRespawnPoint(respawnPoint != null ? respawnPoint : RespawnAnchor(player));

        if (activateSound != null)
            SoundVariety.PlayAt(activateSound, transform.position, activateVolume);

        if (activateVfx != null)
        {
            ParticleSystem burst = Instantiate(activateVfx, transform.position + Vector3.up * 1f, Quaternion.identity);
            burst.Emit(vfxBurst);
            Destroy(burst.gameObject, 4f);
        }

        ApplyState(true);
        StartCoroutine(Pop());
        onActivated.Invoke();
    }

    // Where the player stood when they pressed E, facing the way they faced, pushed out of the pillar if they were
    // right on top of it. Kept as a child so the Death Manager can point at it.
    private Transform RespawnAnchor(Transform player)
    {
        if (anchor == null)
        {
            anchor = new GameObject("RespawnPoint").transform;
            anchor.SetParent(transform, false);
        }

        Vector3 flat = player.position - transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.01f)
            flat = -player.forward;
        Vector3 position = player.position;
        if (flat.magnitude < respawnDistance)
            position = transform.position + flat.normalized * respawnDistance + Vector3.up * (player.position.y - transform.position.y);

        anchor.SetPositionAndRotation(position, Quaternion.Euler(0f, player.eulerAngles.y, 0f));
        return anchor;
    }

    // The active pillar is disabled so the E prompt only shows on ones you can still activate; its glow and the book
    // run on coroutines, which keep going while the component is disabled. fresh = this is a real activation (with
    // the flash and the book flying), not the state being set up at start or handed over.
    private void ApplyState(bool fresh)
    {
        enabled = !IsActive;
        if (glow != null)
            StopCoroutine(glow);
        glow = null;
        if (ignite != null)
            StopCoroutine(ignite);
        ignite = null;
        if (glowLight != null)
            glowLight.transform.localPosition = lightRestPosition;

        if (IsActive)
        {
            if (fresh)
                ignite = StartCoroutine(Ignite());
            else
                glow = StartCoroutine(ActiveGlow(false));
            if (hover != null && flyOnActivate && !bookGone && !bookBusy)
            {
                if (fresh)
                    StartCoroutine(FlyAway());
                else
                {
                    hover.gameObject.SetActive(false);   // active from the start: the book is long gone
                    bookGone = true;
                }
            }
        }
        else
        {
            SetEmission(inactiveEmission);
            SetLight(inactiveLightIntensity);
            if (hover != null && bookGone && !bookBusy)
                StartCoroutine(BookReturn());
        }
    }

    // Lights up from the bottom: the ring and the light climb the pillar while the glow ramps from dim to lit, timed to
    // the book closing and flying off; at the top the usual flare takes over and the glow settles into breathing.
    private IEnumerator Ignite()
    {
        float duration = lightUpTime > 0f ? lightUpTime : bookCloseTime + bookFlyTime;
        var trigger = GetComponent<BoxCollider>();
        float triggerTop = trigger != null ? trigger.center.y + trigger.size.y * 0.5f : 3f;
        float top = lightUpTop > 0f ? lightUpTop : triggerTop * 0.72f;
        float width = trigger != null ? Mathf.Max(trigger.size.x, trigger.size.z) : 1.4f;
        Transform ring = lightUpRing ? MakeRing(width) : null;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = t / duration;
            float height = Mathf.Lerp(0.15f, top, Ease.InOutSine(k));
            if (glowLight != null)
                glowLight.transform.localPosition = new Vector3(lightRestPosition.x, height, lightRestPosition.z);
            SetLight(Mathf.Lerp(inactiveLightIntensity, activeLightIntensity * activateFlash, k));
            SetEmission(Color.Lerp(inactiveEmission, activeEmission, k));
            if (ring != null)
            {
                ring.localPosition = new Vector3(0f, height, 0f);
                float pulse = 1f + 0.06f * Mathf.Sin(t * 30f);
                ring.localScale = new Vector3(width * 1.15f * pulse, 0.03f, width * 1.15f * pulse);
            }
            yield return null;
        }

        if (ring != null)
            Destroy(ring.gameObject);
        if (glowLight != null)
            glowLight.transform.localPosition = lightRestPosition;
        ignite = null;
        glow = StartCoroutine(ActiveGlow(true));
    }

    // A thin glowing disc round the pillar, in the pillar's own glow material, that the sweep carries up.
    private Transform MakeRing(float width)
    {
        Material glowMaterial = null;
        foreach (Renderer r in renderers)
        {
            if (hover != null && (r.transform == hover || r.transform.IsChildOf(hover)))
                continue;
            glowMaterial = r.sharedMaterial;
            break;
        }

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "LightUpRing";
        Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        ring.transform.localScale = new Vector3(width * 1.15f, 0.03f, width * 1.15f);
        var renderer = ring.GetComponent<Renderer>();
        if (glowMaterial != null)
            renderer.sharedMaterial = glowMaterial;
        var ringBlock = new MaterialPropertyBlock();
        ringBlock.SetColor(EmissionId, activeEmission * 3f);
        ringBlock.SetColor(Shader.PropertyToID("_BaseColor"), Color.white);
        renderer.SetPropertyBlock(ringBlock);
        return ring.transform;
    }

    private IEnumerator ActiveGlow(bool flash)
    {
        if (flash)
        {
            for (float t = 0f; t < flashDuration; t += Time.deltaTime)
            {
                float k = Ease.OutCubic(t / flashDuration);
                SetEmission(Color.Lerp(activeEmission * activateFlash, activeEmission, k));
                SetLight(Mathf.Lerp(activeLightIntensity * activateFlash, activeLightIntensity, k));
                yield return null;
            }
        }

        while (true)
        {
            float wave = 1f + breathe * 0.5f * (Mathf.Sin(Time.time * breatheSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            SetEmission(activeEmission * wave);
            SetLight(activeLightIntensity * wave);
            yield return null;
        }
    }

    // The book: a slow bob, turned to face the player (or turning slowly on the spot).
    private IEnumerator Hover()
    {
        while (true)
        {
            if (bookGone || bookBusy)
            {
                yield return null;
                continue;
            }

            hoverPhase += Time.deltaTime * hoverSpeed;
            hover.localPosition = hoverRestPosition + Vector3.up * (Mathf.Sin(hoverPhase * Mathf.PI * 2f) * hoverAmount);

            Transform viewer = faceThePlayer && Camera.main != null ? Camera.main.transform : null;
            if (viewer != null)
            {
                Vector3 to = viewer.position - hover.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.01f)
                {
                    Quaternion facing = Quaternion.Euler(0f, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + faceYawOffset, 0f);
                    hover.rotation = Quaternion.Slerp(hover.rotation, facing, 1f - Mathf.Exp(-6f * Time.deltaTime));
                }
            }
            else
            {
                hover.localRotation = hoverRestRotation * Quaternion.Euler(0f, Time.time * hoverSpin % 360f, 0f);
            }
            yield return null;
        }
    }

    // The halves swing shut on the spine, then the closed book goes straight up, spinning, shrinking to nothing near the top.
    private IEnumerator FlyAway()
    {
        bookBusy = true;
        Vector3 open = hover.localScale;
        for (float t = 0f; t < bookCloseTime; t += Time.deltaTime)
        {
            SetClosed(Ease.InOutCubic(t / bookCloseTime));
            yield return null;
        }
        SetClosed(1f);

        Vector3 from = hover.position;
        for (float t = 0f; t < bookFlyTime; t += Time.deltaTime)
        {
            float k = t / bookFlyTime;
            hover.position = from + Vector3.up * (bookFlyHeight * Ease.InQuad(k));
            hover.Rotate(0f, bookFlySpin * Time.deltaTime, 0f, Space.World);
            hover.localScale = open * (k < 0.6f ? 1f : Mathf.Clamp01((1f - k) / 0.4f));
            yield return null;
        }
        hover.gameObject.SetActive(false);
        hover.localScale = open;
        SetClosed(0f);
        bookGone = true;
        bookBusy = false;
    }

    // 0 = open as modelled, 1 = shut: each half turned Book Close Angle about the spine, toward the other, and (once
    // the halves were measured) slid half a thickness outward, so the two slabs end up side by side, pages touching,
    // instead of inside each other.
    private void SetClosed(float amount)
    {
        Vector3 axis = bookSpineTop - bookSpineBottom;
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.up;
        float angle = bookCloseAngle * amount * (bookCloseFlip ? 1f : -1f);
        float slide = hingeMeasured ? hinge.thickness * 0.5f * amount : 0f;
        Hinge(bookLeft, leftRestPosition, leftRestRotation, axis.normalized, angle, hinge.pagesLeft, slide);
        Hinge(bookRight, rightRestPosition, rightRestRotation, axis.normalized, -angle, hinge.pagesRight, slide);
    }

    // Turns a half about the spine, then moves it away from its own (turned) page direction by the slide.
    private void Hinge(Transform half, Vector3 restPosition, Quaternion restRotation, Vector3 axis, float angle, Vector3 pages, float slide)
    {
        if (half == null)
            return;
        Quaternion turn = Quaternion.AngleAxis(angle, axis);
        half.localRotation = turn * restRotation;
        half.localPosition = bookSpineBottom + turn * (restPosition - bookSpineBottom) - turn * pages * slide;
    }

    private static Transform FindPart(Transform root, string key)
    {
        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            if (part != root && part.name.ToLowerInvariant().Contains(key))
                return part;
        return null;
    }

    // Back on its pillar with a little pop, once another checkpoint has taken over.
    private IEnumerator BookReturn()
    {
        bookBusy = true;
        hover.localPosition = hoverRestPosition;
        hover.localRotation = hoverRestRotation;
        SetClosed(0f);
        hover.gameObject.SetActive(true);
        bookGone = false;
        const float grow = 0.4f;
        for (float t = 0f; t < grow; t += Time.deltaTime)
        {
            hover.localScale = hoverRestScale * Ease.OutBack(t / grow);
            yield return null;
        }
        hover.localScale = hoverRestScale;
        bookBusy = false;
    }

    // A quick swell that settles back with a little overshoot.
    private IEnumerator Pop()
    {
        if (visual == null || popDuration <= 0f)
            yield break;
        for (float t = 0f; t < popDuration; t += Time.deltaTime)
        {
            float k = t / popDuration;
            float size = k < 0.25f ? Mathf.Lerp(1f, popScale, Ease.OutQuad(k / 0.25f)) : Mathf.Lerp(popScale, 1f, Ease.OutBack((k - 0.25f) / 0.75f));
            visual.localScale = visualScale * size;
            yield return null;
        }
        visual.localScale = visualScale;
    }

    private void SetEmission(Color color)
    {
        block.SetColor(EmissionId, color);
        foreach (var r in renderers)
            r.SetPropertyBlock(block);
    }

    private void SetLight(float intensity)
    {
        if (glowLight != null)
            glowLight.intensity = intensity;
    }
}
