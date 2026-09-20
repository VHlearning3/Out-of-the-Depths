using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Press E to pick up: puts the item in the player's inventory, plays feedback and pops out of existence.
// Root = this script + collider, mesh in a child called Visual (it bobs and spins), so the model can be swapped without touching logic.
// If the item asset has a World Model, that model replaces the placeholder mesh automatically (in Play mode, or baked into
// the prefab with Tools > Out of the Depths > Apply Item Models To Pickups).
// Runs before the highlight/indicator scripts so they see the real model's renderers.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour, IInteractable
{
    public const string VisualName = "Visual";

    [Header("Item")]
    [Tooltip("Which item this is (Assets/Items). New ones: Assets > Create > Out of the Depths > Item.")]
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;

    [Header("Model")]
    [Tooltip("Show the item's World Model (set on the item asset) instead of the placeholder mesh.")]
    [SerializeField] private bool useItemModel = true;
    [Tooltip("Longest side of the model in metres after fitting. 0 = keep the model's own size.")]
    [SerializeField, Min(0f)] private float modelSize = 0.35f;
    // The item whose model the current Visual already is (set by the bake tools). A prefab baked for one item still
    // swaps correctly on an instance that overrides Item, and an already-baked pickup is never baked twice.
    [SerializeField, HideInInspector] private ItemDefinition bakedFor;

    public ItemDefinition BakedFor => bakedFor;

    [Header("Idle motion")]
    [Tooltip("The mesh that bobs and spins. Empty = first child.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobAmount = 0.08f;
    [SerializeField] private float bobSpeed = 1.6f;
    [Tooltip("Degrees per second. 0 = no spin.")]
    [SerializeField] private float spinSpeed = 40f;

    [Header("Feedback")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.6f;
    [SerializeField] private GameObject pickedUpVfx;
    [Tooltip("The little grow-then-shrink pop when it is taken. 0 = vanishes instantly.")]
    [SerializeField] private float collectDuration = 0.25f;

    [Header("Events")]
    public UnityEvent onPickedUp = new UnityEvent();

    public ItemDefinition Item => item;
    public int Amount => amount;
    public string Prompt => item != null ? "pick up " + item.DisplayName : "pick up";

    private Vector3 visualRestPosition;
    private Quaternion visualRestRotation;
    private float phase;
    private bool collected;

    private void Awake()
    {
        if (useItemModel && item != null && item.WorldModel != null && bakedFor != item)
        {
            visual = ApplyItemModel(transform, item, visual != null ? visual : FirstChild(transform), modelSize);
            bakedFor = item;
        }

        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        if (visual != null)
        {
            visualRestPosition = visual.localPosition;
            visualRestRotation = visual.localRotation;
        }
        phase = Random.value * 10f;
    }

    private static Transform FirstChild(Transform root) => root.childCount > 0 ? root.GetChild(0) : null;

    // Play mode: spawn the item's World Model under the root in place of the placeholder mesh.
    private static Transform ApplyItemModel(Transform root, ItemDefinition item, Transform placeholder, float size)
    {
        var model = Instantiate(item.WorldModel, root);
        return FitItemModel(root, item, model, placeholder, size);
    }

    // Makes `model` the pickup's visual: named Visual, rotated per the item, scaled so its longest side is `size` metres
    // (times the item's own multiplier), centred on the root, and the old placeholder removed. Returns the new visual.
    // Shared with the editor bake tool (Tools > Out of the Depths > Apply Item Models To Pickups) so both look identical.
    public static Transform FitItemModel(Transform root, ItemDefinition item, GameObject model, Transform placeholder, float size, bool destroyPlaceholder = true)
    {
        model.name = VisualName;
        model.transform.SetParent(root, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(item.WorldModelRotation);
        model.transform.localScale = Vector3.one;

        // The pickup's own collider is the one the player interacts with; any the model brought would just get in the way.
        foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            SafeDestroy(collider);

        Bounds bounds = RendererBounds(model.transform, out bool any);
        if (any)
        {
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = size > 0f && longest > 0.0001f ? size / longest : 1f;
            model.transform.localScale = Vector3.one * (scale * item.WorldModelScale);

            // Centre the model on the root so it bobs and spins about its middle, whatever pivot the artist exported.
            Vector3 centreLocal = root.InverseTransformPoint(RendererBounds(model.transform, out _).center);
            model.transform.localPosition -= centreLocal;
        }

        // A placeholder that belongs to a prefab instance in a scene can't be destroyed in the editor, only hidden.
        if (placeholder != null && placeholder != model.transform)
        {
            // Destroy() is deferred to the end of the frame; the highlight/indicator scripts on this object collect
            // renderers in their own Awake/Start before that, so pull the placeholder out of the hierarchy right now.
            placeholder.gameObject.SetActive(false);
            if (destroyPlaceholder)
            {
                if (Application.isPlaying)
                    placeholder.SetParent(null, false);
                SafeDestroy(placeholder.gameObject);
            }
        }

        return model.transform;
    }

    private static void SafeDestroy(Object target)
    {
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static Bounds RendererBounds(Transform root, out bool any)
    {
        var bounds = new Bounds(root.position, Vector3.zero);
        any = false;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer)
                continue;
            if (any)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            any = true;
        }
        return bounds;
    }

    private void Update()
    {
        if (visual == null || collected)
            return;

        visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmount);
        // Spin about the pickup's up axis (pre-multiply), so a model that rests tilted still turns upright like a showcase.
        if (spinSpeed != 0f)
            visual.localRotation = Quaternion.Euler(0f, (Time.time * spinSpeed + phase * 36f) % 360f, 0f) * visualRestRotation;
    }

    public void Interact(GameObject interactor)
    {
        if (collected)
            return;

        if (item == null)
        {
            Debug.LogWarning($"{name}: Pickup Item has no Item assigned, nothing to pick up.", this);
            return;
        }

        var inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning($"{name}: {interactor.name} has no Player Inventory component, can't pick up.", this);
            return;
        }

        int stored = inventory.Add(item, amount);
        if (stored == 0)
            return;

        // Inventory took only part of a stack: the rest stays here for later.
        amount -= stored;
        if (amount > 0)
            return;

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        if (pickedUpVfx != null)
            Instantiate(pickedUpVfx, transform.position, transform.rotation);

        collected = true;
        onPickedUp.Invoke();
        StartCoroutine(Collect());
    }

    // Quick grow, then shrink to nothing while rising a little, then gone.
    private IEnumerator Collect()
    {
        GetComponent<Collider>().enabled = false;
        if (visual != null && collectDuration > 0f)
        {
            Vector3 startScale = visual.localScale;
            Vector3 startPosition = visual.localPosition;
            for (float t = 0f; t < collectDuration; t += Time.deltaTime)
            {
                float k = t / collectDuration;
                float size = k < 0.3f ? Mathf.Lerp(1f, 1.25f, k / 0.3f) : Mathf.Lerp(1.25f, 0f, Ease.OutCubic((k - 0.3f) / 0.7f));
                visual.localScale = startScale * size;
                visual.localPosition = startPosition + Vector3.up * (0.5f * k);
                yield return null;
            }
            visual.localScale = startScale;
            visual.localPosition = startPosition;
        }
        gameObject.SetActive(false);
    }
}
