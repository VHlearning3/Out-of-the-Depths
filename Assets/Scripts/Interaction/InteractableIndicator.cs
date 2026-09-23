using UnityEngine;

// Spawns a looping particle effect around any interactable (pickup, dead fish...) so the player can spot it, and
// flares up when they look at it. Kept subtle: a low idle rate, only near the player, only while the interactable is enabled.
public class InteractableIndicator : MonoBehaviour, IInteractTargetListener
{
    [Header("Effect")]
    [Tooltip("A looping Particle System prefab, e.g. Prefabs/Placeholders/Particle_Placeholder.")]
    [SerializeField] private ParticleSystem effectPrefab;
    [Tooltip("Scale the emitter shape to the meshes under this object, so it fits whatever model is used.")]
    [SerializeField] private bool fitToModel = true;
    [SerializeField] private float fitPadding = 1.2f;
    [Tooltip("Idle particle rate as a fraction of the prefab's. Keep it low: the sparkle is a hint, not a fountain.")]
    [SerializeField, Range(0f, 1f)] private float idleRateScale = 0.35f;
    [Tooltip("Particle size as a fraction of the prefab's.")]
    [SerializeField, Range(0.2f, 2f)] private float sizeScale = 0.8f;

    [Header("When looked at")]
    [Tooltip("Emission rate multiplier while the player is looking at this.")]
    [SerializeField] private float targetedRateMultiplier = 3f;
    [Tooltip("Particles fired the moment the player looks at this. 0 = none.")]
    [SerializeField] private int targetedBurst = 6;

    [Header("Visibility")]
    [Tooltip("Only emit while the IInteractable on this object is enabled (e.g. a fish only once it is dead).")]
    [SerializeField] private bool onlyWhileInteractable = true;
    [Tooltip("Only sparkle while the player is within this distance. 0 = always.")]
    [SerializeField] private float showWithinDistance = 10f;

    private ParticleSystem effect;
    private Behaviour interactable;
    private Transform player;
    private float idleRateMultiplier;
    private bool emitting = true;
    private bool targeted;

    private void Awake()
    {
        interactable = GetComponent<IInteractable>() as Behaviour;
    }

    private void Start()
    {
        if (Camera.main != null)
            player = Camera.main.transform;

        if (effectPrefab == null)
            return;

        Bounds bounds = ModelBounds();
        effect = Instantiate(effectPrefab, bounds.center, Quaternion.identity, transform);

        var emission = effect.emission;
        idleRateMultiplier = emission.rateOverTimeMultiplier * idleRateScale;
        emission.rateOverTimeMultiplier = idleRateMultiplier;
        var main = effect.main;
        main.startSizeMultiplier *= sizeScale;

        SetEmitting(ShouldEmit());

        if (!fitToModel)
            return;

        // Local scaling ignores the parent's scale, so the emitter matches the world-space size of the model.
        main.scalingMode = ParticleSystemScalingMode.Local;
        var shape = effect.shape;
        shape.scale = bounds.size * fitPadding;
    }

    private void Update()
    {
        if (effect != null)
            SetEmitting(ShouldEmit());
    }

    private bool ShouldEmit()
    {
        if (onlyWhileInteractable && interactable != null && !interactable.isActiveAndEnabled)
            return false;
        if (showWithinDistance > 0f && player != null
            && (player.position - transform.position).sqrMagnitude > showWithinDistance * showWithinDistance)
            return false;
        return true;
    }

    public void OnTargeted(bool value)
    {
        targeted = value;
        if (effect == null)
            return;

        var emission = effect.emission;
        emission.rateOverTimeMultiplier = idleRateMultiplier * (targeted ? targetedRateMultiplier : 1f);

        if (targeted && emitting && targetedBurst > 0)
            effect.Emit(targetedBurst);
    }

    private void SetEmitting(bool value)
    {
        if (emitting == value)
            return;

        emitting = value;
        var emission = effect.emission;
        emission.enabled = value;
    }

    private Bounds ModelBounds()
    {
        var bounds = new Bounds(transform.position, Vector3.one * 0.5f);
        bool found = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer)
                continue;

            if (found)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            found = true;
        }
        return bounds;
    }
}
