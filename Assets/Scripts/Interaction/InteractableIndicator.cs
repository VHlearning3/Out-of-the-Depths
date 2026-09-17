using UnityEngine;

// Spawns a looping particle effect around any interactable (pickup, dead fish...) so the player can spot it, and
// flares up when they look at it. Sizes the effect to the child meshes and only emits while the interactable is enabled.
public class InteractableIndicator : MonoBehaviour, IInteractTargetListener
{
    [Header("Effect")]
    [Tooltip("A looping Particle System prefab, e.g. Art/Prefabs/PickupSparkle. Keep its idle rate low; this boosts it on look.")]
    [SerializeField] private ParticleSystem effectPrefab;
    [Tooltip("Scale the emitter shape to the meshes under this object, so it fits whatever model is used.")]
    [SerializeField] private bool fitToModel = true;
    [SerializeField] private float fitPadding = 1.2f;

    [Header("When looked at")]
    [Tooltip("Emission rate multiplier while the player is looking at this.")]
    [SerializeField] private float targetedRateMultiplier = 4f;
    [Tooltip("Particles fired the moment the player looks at this. 0 = none.")]
    [SerializeField] private int targetedBurst = 12;

    [Header("Visibility")]
    [Tooltip("Only emit while the IInteractable on this object is enabled (e.g. a fish only once it is dead).")]
    [SerializeField] private bool onlyWhileInteractable = true;

    private ParticleSystem effect;
    private Behaviour interactable;
    private float idleRateMultiplier;
    private bool emitting = true;
    private bool targeted;

    private void Awake()
    {
        interactable = GetComponent<IInteractable>() as Behaviour;
    }

    private void Start()
    {
        if (effectPrefab == null)
            return;

        Bounds bounds = ModelBounds();
        effect = Instantiate(effectPrefab, bounds.center, Quaternion.identity, transform);
        idleRateMultiplier = effect.emission.rateOverTimeMultiplier;

        if (onlyWhileInteractable && interactable != null)
            SetEmitting(interactable.isActiveAndEnabled);

        if (!fitToModel)
            return;

        // Local scaling ignores the parent's scale, so the emitter matches the world-space size of the model.
        var main = effect.main;
        main.scalingMode = ParticleSystemScalingMode.Local;
        var shape = effect.shape;
        shape.scale = bounds.size * fitPadding;
    }

    private void Update()
    {
        if (effect == null || !onlyWhileInteractable || interactable == null)
            return;

        SetEmitting(interactable.isActiveAndEnabled);
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
