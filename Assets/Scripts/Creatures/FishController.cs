using System.Collections;
using UnityEngine;

// Alive: wanders, can be slashed. Dead: flips belly-up and drifts upward until it meets a Dead Fish Barrier or times
// out, then fades away and respawns like an eaten fish would (EdibleFish → Respawn Time). Not every kill is food (the
// GDD: one slash cuts a whole school, so for every Kills Per Food fish killed, counted across all fish, one is left
// as food): that one is edible the whole way up; the rest fade after No Food Fade After.
[RequireComponent(typeof(Damageable), typeof(EdibleFish))]
public class FishController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FishWander wander;
    [Tooltip("Extra behaviours (e.g. FishAggression) switched off while dead and back on when revived.")]
    [SerializeField] private Behaviour[] disabledWhileDead;
    [Tooltip("The mesh child. Swap this object for real fish art; nothing else needs to change.")]
    [SerializeField] private Transform visual;

    [Header("Death")]
    [SerializeField] private float deathFlipDuration = 0.9f;
    [SerializeField] private float deathFloatSpeed = 0.35f;
    [Tooltip("Slow roll while drifting up, degrees per second. 0 = none.")]
    [SerializeField] private float deathDriftSpin = 10f;
    [Tooltip("Sideways wobble while drifting up, metres per second.")]
    [SerializeField] private float deathDriftSway = 0.12f;
    [Tooltip("How much each death varies: flip time, rise speed, spin and sway are scaled by a random factor in 1 ± this.")]
    [SerializeField, Range(0f, 0.9f)] private float deathVariation = 0.45f;
    [Tooltip("Fade anyway after drifting this long without reaching a barrier. 0 = never.")]
    [SerializeField] private float driftTimeout = 40f;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Food")]
    [Tooltip("Every this many kills (all fish together), the fish killed is left as food: edible while it drifts up. 1 = every kill is food.")]
    [SerializeField, Min(1)] private int killsPerFood = 2;
    [Tooltip("A kill that is not food fades after drifting this many seconds.")]
    [SerializeField] private float noFoodFadeAfter = 6f;

    private static int kills;
    private bool isFood;

    public bool IsAlive { get; private set; } = true;
    public bool IsFading { get; private set; }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Collider[] overlaps = new Collider[8];

    private Damageable damageable;
    private EdibleFish edible;
    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Quaternion visualRestRotation;
    private Vector3 visualRestScale = Vector3.one;
    private float drifted;
    private float riseSpeed;
    private float spinSpeed;
    private Vector3 swayAxis;
    private float swayPhase;
    private float flipTime;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        edible = GetComponent<EdibleFish>();
        if (GetComponent<FishKnockback>() == null)
            gameObject.AddComponent<FishKnockback>();   // shoved back and dazed by a hit
        if (GetComponent<FishHealthDisplay>() == null)
            gameObject.AddComponent<FishHealthDisplay>();   // pips over a hurt fish and the damage each hit does
        renderers = GetComponentsInChildren<Renderer>();
        // No mesh child assigned (a fish placed by hand): roll the first child that draws something, so it still
        // turns belly-up when it dies.
        if (visual == null)
            foreach (Renderer r in renderers)
                if (r.transform != transform && !(r is ParticleSystemRenderer))
                {
                    visual = r.transform;
                    while (visual.parent != transform)
                        visual = visual.parent;
                    break;
                }
        block = new MaterialPropertyBlock();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        if (visual != null)
        {
            visualRestRotation = visual.localRotation;
            visualRestScale = visual.localScale;
        }

        edible.enabled = false;
    }

    private void OnEnable()
    {
        damageable.onDeath.AddListener(OnKilled);
        edible.onRespawned.AddListener(Revive);
    }

    private void OnDisable()
    {
        damageable.onDeath.RemoveListener(OnKilled);
        edible.onRespawned.RemoveListener(Revive);
    }

    private void Update()
    {
        if (IsAlive || IsFading)
            return;

        drifted += Time.deltaTime;
        float radius = wander != null ? wander.BodyRadius : 0.3f;
        LayerMask mask = wander != null ? wander.ObstacleMask : (LayerMask)~0;

        // Each corpse rises at its own pace, with its own roll and a lazy sideways wobble.
        Vector3 move = Vector3.up * riseSpeed + swayAxis * Mathf.Sin(Time.time * 0.8f + swayPhase);
        transform.position += FishSteering.ClampMove(transform.position, move * Time.deltaTime, radius, mask);
        if (spinSpeed != 0f)
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        if (TouchingBarrier(radius) || (driftTimeout > 0f && drifted >= driftTimeout) || (!isFood && drifted >= noFoodFadeAfter))
            StartCoroutine(FadeOut());
    }

    private void OnKilled()
    {
        IsAlive = false;
        drifted = 0f;
        riseSpeed = deathFloatSpeed * Vary();
        spinSpeed = deathDriftSpin * Vary() * (Random.value < 0.5f ? -1f : 1f);
        flipTime = deathFlipDuration * Vary();
        Vector2 side = Random.insideUnitCircle.normalized * (deathDriftSway * Vary());
        swayAxis = new Vector3(side.x, 0f, side.y);
        swayPhase = Random.value * 10f;
        SetBehavioursEnabled(false);
        if (wander != null)
            wander.enabled = false;
        kills++;
        isFood = killsPerFood <= 1 || kills % killsPerFood == 0;
        edible.enabled = isFood;
        StartCoroutine(FlipBellyUp());
    }

    private IEnumerator FlipBellyUp()
    {
        if (visual == null)
            yield break;

        // Like a real dead fish: it rolls over onto its back (either way), the nose dipping a little as it goes,
        // overshoots and rocks back to rest belly-up, head slightly down.
        Quaternion from = visual.localRotation;
        float roll = Random.value < 0.5f ? 180f : -180f;
        float noseDown = Random.Range(8f, 18f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, flipTime);
            float k = Mathf.Clamp01(t);
            float over = 1f + 0.12f * Mathf.Sin(k * Mathf.PI) * (1f - k) * 2f;   // a little past, then back
            float eased = Mathf.SmoothStep(0f, 1f, k) * over;
            Quaternion rolled = visualRestRotation * Quaternion.Euler(noseDown * Mathf.Sin(k * Mathf.PI * 0.5f), 0f, roll * eased);
            visual.localRotation = Quaternion.Slerp(from, rolled, Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, k * 3f)));
            yield return null;
        }
    }

    private float Vary() => Random.Range(1f - deathVariation, 1f + deathVariation);

    // Static triggers don't send events to colliders without a rigidbody, so the fish looks for the barrier itself.
    private bool TouchingBarrier(float radius)
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, radius, overlaps, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            if (overlaps[i].GetComponentInParent<DeadFishBarrier>() != null)
                return true;
        }
        return false;
    }

    private IEnumerator FadeOut()
    {
        IsFading = true;
        edible.enabled = false;

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            SetVisibility(1f - Ease.InOutSine(t / fadeDuration));
            yield return null;
        }

        SetVisibility(0f);
        edible.Despawn();
    }

    // Alpha where the material allows it, plus a shrink so it disappears with the opaque placeholder material too.
    private void SetVisibility(float visibility)
    {
        if (visual != null)
            visual.localScale = visualRestScale * visibility;

        foreach (Renderer r in renderers)
        {
            r.GetPropertyBlock(block);
            SetAlpha(r, BaseColorId, visibility);
            SetAlpha(r, ColorId, visibility);
            r.SetPropertyBlock(block);
        }
    }

    private void SetAlpha(Renderer r, int id, float alpha)
    {
        Color color;
        if (block.HasColor(id))
            color = block.GetColor(id);
        else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty(id))
            color = r.sharedMaterial.GetColor(id);
        else
            color = Color.white;

        color.a = alpha;
        block.SetColor(id, color);
    }

    private void Revive()
    {
        StopAllCoroutines();
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (visual != null)
        {
            visual.localRotation = visualRestRotation;
            visual.localScale = visualRestScale;
        }

        foreach (Renderer r in renderers)
        {
            block.Clear();
            r.SetPropertyBlock(block);
        }
        var tint = GetComponent<RendererTint>();
        if (tint != null)
            tint.Apply();

        damageable.ResetHealth();
        edible.enabled = false;
        if (wander != null)
        {
            wander.ResetHome(spawnPosition);
            wander.enabled = true;
        }

        SetBehavioursEnabled(true);
        IsFading = false;
        IsAlive = true;
    }

    private void SetBehavioursEnabled(bool value)
    {
        if (disabledWhileDead == null)
            return;

        foreach (var behaviour in disabledWhileDead)
        {
            if (behaviour != null)
                behaviour.enabled = value;
        }
    }
}
