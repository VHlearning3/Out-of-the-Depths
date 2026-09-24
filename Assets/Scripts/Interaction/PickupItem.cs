using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

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
    // Testing switch (admin panel): no inspect at all, every pickup flies straight in.
    public static bool QuickPickups { get; set; }
    private static readonly System.Collections.Generic.HashSet<ItemDefinition> seenThisSession = new System.Collections.Generic.HashSet<ItemDefinition>();


    [Header("Item")]
    [Tooltip("Which item this is (Assets/Items). New ones: Assets > Create > Out of the Depths > Item.")]
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;

    [Header("Model")]
    [Tooltip("Show the item's World Model (set on the item asset) instead of the placeholder mesh.")]
    [SerializeField] private bool useItemModel = true;
    [Tooltip("Longest side of the model in metres after fitting. 0 = keep the model's own size.")]
    [SerializeField, Min(0f)] private float modelSize = 0.35f;
    [Tooltip("Which look of the item this pickup shows: 0 = the item's World Model, 1, 2... = its World Model Variants (the three bone key pieces).")]
    [SerializeField, Min(0)] private int modelVariant = 0;
    [Tooltip("For pickups without a World Model: one of these is picked at random for the placeholder plaque at start (the dog photos). Empty = keep the prefab material.")]
    [SerializeField] private Material[] placeholderMaterials;
    // The item whose model the current Visual already is (set by the bake tools). A prefab baked for one item still
    // swaps correctly on an instance that overrides Item, and an already-baked pickup is never baked twice.
    [SerializeField, HideInInspector] private ItemDefinition bakedFor;
    [SerializeField, HideInInspector] private int bakedVariant;

    public ItemDefinition BakedFor => bakedFor;
    public int BakedVariant => bakedVariant;
    public int ModelVariant => modelVariant;

    [Header("Idle motion")]
    [Tooltip("The mesh that bobs and spins. Empty = first child.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobAmount = 0.08f;
    [SerializeField] private float bobSpeed = 1.6f;
    [Tooltip("Degrees per second. 0 = no spin.")]
    [SerializeField] private float spinSpeed = 40f;

    [Header("Feedback")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.3f;
    [Tooltip("The chime when you press E and take the item for good (the jingle above plays when you first grab it).")]
    [SerializeField] private AudioClip takeSound;
    [SerializeField, Range(0f, 1f)] private float takeVolume = 0.5f;
    [SerializeField] private GameObject pickedUpVfx;
    [Tooltip("The pickup animation, in three beats: the item snaps up in front of your eyes (Grab), hangs there a moment so you see what you got (Hold), then dives into your torso and is gone (Absorb). E or a right click puts it away at any moment, even during the Grab; Hold is only a timer when Wait For Interact is off. All three at 0 = it just vanishes.")]
    [SerializeField] private float grabSeconds = 0.12f;
    [SerializeField] private float holdSeconds = 0.5f;
    [FormerlySerializedAs("collectDuration")]
    [SerializeField] private float absorbSeconds = 0.3f;
    [Tooltip("How far in front of the eyes it hangs during the hold, in metres.")]
    [SerializeField] private float holdDistance = 1.5f;
    [Tooltip("How big it shows during the hold, relative to its resting size.")]
    [SerializeField] private float holdScale = 1.2f;
    [Tooltip("Camera jolt as it is absorbed. 0 = none.")]
    [SerializeField, Range(0f, 1f)] private float absorbShake = 0.25f;

    [Header("Inspect")]
    [Tooltip("While it hangs in front of you, hold the left mouse button to grab it and turn it with the mouse. The camera stays put and the hold waits until you let go.")]
    [SerializeField] private bool inspectable = true;
    [Tooltip("Degrees of turn per pixel of mouse movement.")]
    [SerializeField] private float inspectSensitivity = 0.3f;
    [Tooltip("How fast a spin you gave it dies away once you let go (per second).")]
    [SerializeField] private float inspectDamping = 4f;
    [Tooltip("The item stays up until you press E (Interact) to take it; Hold Seconds is then the minimum before E counts. Off = it is taken by itself after Hold Seconds.")]
    [SerializeField] private bool waitForInteract = true;
    [Tooltip("Inspect only the first time an item is picked up in a session; later pickups of the same item just fly straight in.")]
    [SerializeField] private bool inspectOnlyFirstTime = true;
    [Tooltip("Keep E held this long after picking up and the item is taken without the inspect (tap E = inspect, hold E = grab and go). 0 = off.")]
    [SerializeField] private float holdToSkipSeconds = 0.25f;
    [Tooltip("Where it hangs vertically: metres above the eye line (negative = below).")]
    [SerializeField] private float holdHeight = 0.05f;
    [Tooltip("Scroll wheel while inspecting: metres closer / further per notch...")]
    [SerializeField] private float zoomStep = 0.15f;
    [Tooltip("...between these distances from the eyes.")]
    [SerializeField] private Vector2 zoomRange = new Vector2(0.45f, 2.5f);
    [Tooltip("How fast it turns by itself while it hangs there, in degrees per second (you can still grab and turn it).")]
    [SerializeField] private float holdTurnSpeed = 15f;
    [Tooltip("How far it floats up and down while it hangs there, in metres, and how quickly (cycles per second).")]
    [SerializeField] private float holdBobAmount = 0.006f;
    [SerializeField] private float holdBobSpeed = 0.5f;
    [Tooltip("How quickly the zoom glides to its new distance (per second). Higher = snappier.")]
    [SerializeField] private float zoomSmoothing = 8f;
    [Tooltip("You cannot swim while the item is up (looking around still works).")]
    [SerializeField] private bool freezeWhileInspecting = true;
    [Tooltip("Blur everything behind the item while it is up (a URP depth of field; the camera gets post-processing switched on meanwhile).")]
    [SerializeField] private bool blurBackground = true;
    [Tooltip("The camera cannot turn at all while the item is up.")]
    [SerializeField] private bool lockLookWhileInspecting = true;
    [Tooltip("The mouse cursor is shown and freed while the item is up, so you can see what you are dragging.")]
    [SerializeField] private bool showCursorWhileInspecting = true;
    [Tooltip("The centre-screen reticle hides while the item is up.")]
    [SerializeField] private bool hideReticleWhileInspecting = true;
    [Tooltip("Light the held item with its own key and fill lights (Inspect Light), so it reads as a shape in the dark.")]
    [SerializeField] private bool lightWhileInspecting = true;
    [Tooltip("Keep the white outline on the item while it is held, so it separates from the background.")]
    [SerializeField] private bool outlineWhileInspecting = true;
    [Tooltip("If something is in the way where the item would hang (looking down into a drawer, facing a wall close up), the view turns to open space at eye level while it is up and turns back once it is taken. The item also never hangs further out than the free space in front, so it is never inside the furniture.")]
    [SerializeField] private bool clearView = true;
    [Tooltip("How long that turn takes, each way, in seconds.")]
    [SerializeField] private float clearViewSeconds = 0.35f;
    [Tooltip("Where it flies to, as an offset from the torso (the middle of the Character Controller) in the body local space. Zero = straight into the torso.")]
    [SerializeField] private Vector3 collectOffset = Vector3.zero;

    [Header("Stand out")]
    [Tooltip("A soft glow and a small pulsing light round it, coloured by the kind of item (Pickup Beacon, added at start), so it stands out in the murk.")]
    [SerializeField] private bool beacon = true;

    [Header("Events")]
    public UnityEvent onPickedUp = new UnityEvent();

    public ItemDefinition Item => item;
    public int Amount => amount;
    public string Prompt => item != null ? "pick up " + item.DisplayName : "pick up";
    public bool Collected => collected;   // on its way into the inventory (or already in it)

    // For a pickup made at runtime (the bone key the tying minigame hands over): another item, its model swapped in
    // for whatever the prefab showed. Call before Interact.
    public void SetItem(ItemDefinition newItem, int newAmount = 1)
    {
        item = newItem;
        amount = Mathf.Max(1, newAmount);
        if (useItemModel && item != null && item.WorldModel != null)
        {
            visual = ApplyItemModel(transform, item, visual != null ? visual : FirstChild(transform), modelSize, modelVariant);
            bakedFor = item;
            bakedVariant = modelVariant;
        }
        if (visual != null)
        {
            visualRestPosition = visual.localPosition;
            visualRestRotation = visual.localRotation;
        }
    }

    private Vector3 visualRestPosition;
    private Quaternion visualRestRotation;
    private float phase;
    private bool collected;
    private GameObject collector;
    private PlayerInventory pendingInventory;
    private float holdNow;   // the current hold distance (the scroll wheel changes it)
    private bool inspectActive;
    private bool inspectAllowed;
    private SwimController inspectSwimmer;
    private PlayerInteractor inspectInteractor;
    private SlashAttack inspectSlash;
    private bool inspectLookWas;
    private bool inspectFrozenWas;
    private bool inspectAttackWas;
    private bool inspectCursorVisibleWas;
    private CursorLockMode inspectCursorLockWas;
    // The Clear View turn: from, to, when it started (-1 = not turning), and the view to go back to afterwards.
    private Vector2 viewFrom;
    private Vector2 viewTo;
    private float viewTurnStart = -1f;
    private Vector2 viewRestore;
    private bool viewTurned;
    private Transform roomIgnore;   // the player's body, which the free-space check looks past
    private static readonly RaycastHit[] roomHits = new RaycastHit[16];

    private void Awake()
    {
        if (useItemModel && item != null && item.WorldModel != null && (bakedFor != item || bakedVariant != modelVariant))
        {
            visual = ApplyItemModel(transform, item, visual != null ? visual : FirstChild(transform), modelSize, modelVariant);
            bakedFor = item;
            bakedVariant = modelVariant;
        }

        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        // A random dog photo on the placeholder plaque, so a shelf of pickups is not seven copies of one picture.
        if (visual != null && placeholderMaterials != null && placeholderMaterials.Length > 0 && (item == null || item.WorldModel == null))
        {
            Material chosen = placeholderMaterials[Random.Range(0, placeholderMaterials.Length)];
            if (chosen != null)
                foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
                    r.sharedMaterial = chosen;
        }
        if (visual != null)
        {
            visualRestPosition = visual.localPosition;
            visualRestRotation = visual.localRotation;
        }
        phase = Random.value * 10f;
        if (beacon && GetComponent<PickupBeacon>() == null)
            gameObject.AddComponent<PickupBeacon>();
    }

    private static Transform FirstChild(Transform root) => root.childCount > 0 ? root.GetChild(0) : null;

    // Play mode: spawn the item's World Model under the root in place of the placeholder mesh.
    private static Transform ApplyItemModel(Transform root, ItemDefinition item, Transform placeholder, float size, int variant)
    {
        var model = Instantiate(item.WorldModelFor(variant), root);
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

        int space = inventory.SpaceFor(item, amount);
        if (space == 0)
            return;

        // Room for only part of a stack: hand that part over now, the rest stays here for later.
        if (space < amount)
        {
            amount -= inventory.Add(item, space);
            return;
        }

        // The whole thing: it goes into the inventory only once the animation has absorbed it (Deliver), so a weapon
        // never shows up in your hand before it has arrived.
        pendingInventory = inventory;

        if (pickupSound != null)
            SlicedOneShot.Play(pickupSound, transform.position, pickupVolume, 1f, 0f, 0.05f, 0.5f, 20f);

        if (pickedUpVfx != null)
            Instantiate(pickedUpVfx, transform.position, transform.rotation);

        collected = true;
        collector = interactor;
        StartCoroutine(Collect());
    }

    // The pickup animation. Grab: the item snaps up to a point in front of the eyes, turning to one fixed showcase pose
    // (the same side toward you every time, whatever spin it was at) and growing. Hold: it hangs there, turning slowly,
    // so you see what you got; hold the left mouse button to grab it and turn it yourself (the camera stays put and the
    // clock waits until you let go). Absorb: it dives straight into the torso, shrinking to nothing, with a small jolt.
    // Every target point follows the player, so it all still lands if you move or turn.
    private IEnumerator Collect()
    {
        GetComponent<Collider>().enabled = false;
        if (visual == null || grabSeconds + holdSeconds + absorbSeconds <= 0f)
        {
            if (Deliver())
                gameObject.SetActive(false);
            yield break;
        }

        Camera eyeCamera = collector != null ? collector.GetComponentInChildren<Camera>() : null;
        if (eyeCamera == null)
            eyeCamera = Camera.main;
        Transform eye = eyeCamera != null ? eyeCamera.transform : null;
        CharacterController body = collector != null ? collector.GetComponentInParent<CharacterController>() : null;
        Transform bodyRoot = body != null ? body.transform : collector != null ? collector.transform : eye;
        SwimController swimmer = collector != null ? collector.GetComponentInParent<SwimController>() : null;
        PlayerInteractor interactor = collector != null ? collector.GetComponentInParent<PlayerInteractor>() : null;
        SlashAttack slash = collector != null ? collector.GetComponentInParent<SlashAttack>() : null;

        Vector3 startScale = visual.localScale;
        Vector3 startPosition = visual.position;
        Quaternion startRotation = visual.rotation;
        Quaternion showcase = transform.rotation * visualRestRotation;   // the upright resting pose, in world space
        Quaternion handled = Quaternion.identity;                        // whatever turn the player gives it
        float turned = 0f;
        holdNow = holdDistance;

        // Somewhere clear for it to hang: the view turns there first if the way ahead is blocked (Clear View).
        bool inspecting = ShouldInspect();
        roomIgnore = bodyRoot;
        viewTurned = false;
        viewTurnStart = -1f;
        float radius = HeldRadius();
        float roomAhead = float.MaxValue;
        if (inspecting && clearView && eye != null)
        {
            roomAhead = ClearView(eye, swimmer, radius);
            holdNow = Mathf.Clamp(roomAhead, zoomRange.x, holdDistance);
        }

        // Grab: up to the eyes, into the showcase pose, growing. E (or a right click) already here skips the inspect
        // and it goes straight in from wherever it has got to.
        bool closedEarly = false;
        for (float t = 0f; t < grabSeconds; t += Time.deltaTime)
        {
            float k = Ease.OutCubic(t / grabSeconds);
            visual.position = Vector3.Lerp(startPosition, HoldPoint(eye, startPosition), k);
            visual.rotation = Quaternion.Slerp(startRotation, Showcase(showcase, eye, 0f), k);
            visual.localScale = startScale * Mathf.Lerp(1f, holdScale, k);
            StepView(swimmer);
            yield return null;
            if (CloseRequested(interactor))
            {
                closedEarly = true;
                break;
            }
        }

        if (inspecting && !closedEarly)
        {
        // Hold: hang in front of the eyes, turning slowly. Left mouse held = grab it and turn it; the scroll wheel zooms;
        // E takes it (or the clock, with Wait For Interact off). Swimming and looking stop, the cursor shows, the
        // reticle hides, the background blurs, and the hotbar and the interactor stand back meanwhile. EndInspect puts
        // every one of those back, and also runs if this object is disabled or something throws mid-hold.
        BeginInspect(eye != null, eyeCamera, swimmer, interactor, slash);
        bool needsInteract = waitForInteract && interactor != null && interactor.InteractAction != null;
        try
        {
            Vector2 spin = Vector2.zero;   // degrees per second, from the mouse
            float holdTarget = holdNow;
            float held = 0f;
            float heldE = 0f;   // how long E has stayed down: hold it to skip the inspect
            while (true)
            {
                Mouse mouse = Mouse.current;
                bool dragging = inspectAllowed && mouse != null && mouse.leftButton.isPressed;
                if (dragging && Time.deltaTime > 0f)
                {
                    spin = Vector2.ClampMagnitude(mouse.delta.ReadValue() * (inspectSensitivity / Time.deltaTime), 900f);
                }
                else
                {
                    spin *= Mathf.Exp(-inspectDamping * Time.deltaTime);
                    held += Time.deltaTime;
                    turned += holdTurnSpeed * Time.deltaTime;
                }
                if (inspectAllowed && mouse != null)
                {
                    float wheel = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(wheel) > 0.01f)
                        holdTarget = Mathf.Clamp(holdTarget - Mathf.Sign(wheel) * zoomStep, zoomRange.x, zoomRange.y);
                }
                // Never further out than the free space ahead (checked again once the view has stopped turning).
                StepView(swimmer);
                if (clearView && eye != null && viewTurnStart < 0f)
                    roomAhead = Room(eye.position, eye.forward, radius);
                float allowed = Mathf.Min(holdTarget, Mathf.Max(zoomRange.x, roomAhead));
                holdNow = Mathf.Lerp(holdNow, allowed, 1f - Mathf.Exp(-zoomSmoothing * Time.deltaTime));
                if (inspectAllowed && blurBackground)
                    InspectFocus.Focus(holdNow);
                if (eye != null && spin.sqrMagnitude > 0.01f)
                    handled = Quaternion.AngleAxis(spin.x * Time.deltaTime, eye.up) * Quaternion.AngleAxis(-spin.y * Time.deltaTime, eye.right) * handled;
                if (swimmer != null && lockLookWhileInspecting == false)
                    swimmer.LookLocked = inspectLookWas || dragging;   // camera free, except while you are dragging the item

                visual.position = HoldPoint(eye, startPosition) + Vector3.up * (holdBobAmount * Mathf.Sin(Time.time * holdBobSpeed * Mathf.PI * 2f));
                visual.rotation = handled * Showcase(showcase, eye, turned);
                visual.localScale = startScale * holdScale;

                InputAction eKey = interactor != null ? interactor.InteractAction : null;
                heldE = eKey != null && eKey.IsPressed() ? heldE + Time.unscaledDeltaTime : 0f;
                if (holdToSkipSeconds > 0f && heldE >= holdToSkipSeconds)
                    break;
                // E (or a right click) closes it at once; with Wait For Interact off it goes by itself after Hold Seconds.
                if (needsInteract ? CloseRequested(interactor) : held >= holdSeconds)
                    break;
                yield return null;
            }
        }
        finally
        {
            EndInspect();
        }
        }


        if (takeSound != null)
            SlicedOneShot.Play(takeSound, visual.position, takeVolume, 1f, 0f, 0.05f, 0.3f, 20f);
        // Absorb: from where it hangs straight into the torso, easing in and out, shrinking to nothing. Both ends
        // follow the player, so it stays put in the view even if you turn on the way.
        Vector3 hangOffset = visual.position - HoldPoint(eye, startPosition);
        if (viewTurned && swimmer != null)
            TurnView(swimmer, viewRestore);   // back to where you were looking
        for (float t = 0f; t < absorbSeconds; t += Time.deltaTime)
        {
            StepView(swimmer);
            float k = t / absorbSeconds;
            turned += 240f * Time.deltaTime / Mathf.Max(0.01f, absorbSeconds);
            visual.position = Vector3.Lerp(HoldPoint(eye, startPosition) + hangOffset, Torso(body, bodyRoot), Ease.InOutCubic(k));
            visual.rotation = handled * Showcase(showcase, eye, turned);
            visual.localScale = startScale * (holdScale * (1f - Ease.InQuad(k)) + 0.02f);
            yield return null;
        }

        if (viewTurned && swimmer != null)
        {
            swimmer.SetLookAngles(viewRestore.x, viewRestore.y);
            viewTurned = false;
            viewTurnStart = -1f;
        }
        if (swimmer != null && absorbShake > 0f)
            swimmer.AddShake(absorbShake);
        if (pickedUpVfx != null)
            Instantiate(pickedUpVfx, Torso(body, bodyRoot), Quaternion.identity);

        visual.localScale = startScale;
        visual.position = startPosition;
        visual.rotation = startRotation;
        if (Deliver())
            gameObject.SetActive(false);
    }

    // Locks the player for the inspect and remembers what to put back.
    private void BeginInspect(bool hasEye, Camera camera, SwimController swimmer, PlayerInteractor interactor, SlashAttack slash)
    {
        inspectActive = true;
        inspectAllowed = inspectable && hasEye;
        inspectSwimmer = swimmer;
        inspectInteractor = interactor;
        inspectSlash = slash;
        inspectLookWas = swimmer != null && swimmer.LookLocked;
        inspectFrozenWas = swimmer != null && swimmer.Frozen;
        inspectAttackWas = slash != null && slash.CanAttack;
        inspectCursorVisibleWas = Cursor.visible;
        inspectCursorLockWas = Cursor.lockState;

        if (interactor != null)
            interactor.Busy = true;
        if (pendingInventory != null)
            pendingInventory.InputBlocked = true;
        if (swimmer != null)
        {
            if (freezeWhileInspecting)
                swimmer.Frozen = true;
            if (lockLookWhileInspecting)
                swimmer.LookLocked = true;
        }
        if (inspectAllowed)
        {
            InspectHintUI.Show(Caption(item), item != null ? item.Description : string.Empty);   // name + a line about it, over the mouse / wheel / E pictogram
            if (blurBackground)
                InspectFocus.Show(camera, holdNow);
            if (showCursorWhileInspecting)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (hideReticleWhileInspecting)
                HitMarker.SetReticleVisible(false);
            if (slash != null)
                slash.SetCanAttack(false);   // a click grabs the item; it must not swing the dagger
            if (lightWhileInspecting)
                InspectLight.Show(camera);
            if (outlineWhileInspecting && interactor != null && interactor.OutlineMaterial != null)
                OutlineHull.Show(gameObject, interactor.OutlineMaterial);
        }
    }

    // Puts the player back exactly as they were. Safe to call twice.
    private void EndInspect()
    {
        if (!inspectActive)
            return;
        inspectActive = false;
        if (inspectSwimmer != null)
        {
            inspectSwimmer.LookLocked = inspectLookWas;
            inspectSwimmer.Frozen = inspectFrozenWas;
        }
        if (inspectInteractor != null)
            inspectInteractor.Busy = false;
        if (pendingInventory != null)
            pendingInventory.InputBlocked = false;
        if (inspectAllowed)
        {
            InspectHintUI.Hide();
            if (blurBackground)
                InspectFocus.Hide();
            if (showCursorWhileInspecting)
            {
                Cursor.lockState = inspectCursorLockWas;
                Cursor.visible = inspectCursorVisibleWas;
            }
            if (hideReticleWhileInspecting)
                HitMarker.SetReticleVisible(true);
            if (inspectSlash != null)
                inspectSlash.SetCanAttack(inspectAttackWas);
            if (lightWhileInspecting)
                InspectLight.Hide();
            if (outlineWhileInspecting)
                OutlineHull.Hide(gameObject);
        }
    }

    // E, however it arrives: the Interact action (any of its states) or the key itself as a fallback.
    // Put the item away: E, or a right click.
    private static bool CloseRequested(PlayerInteractor interactor)
    {
        Mouse mouse = Mouse.current;
        return InteractPressed(interactor) || (mouse != null && mouse.rightButton.wasPressedThisFrame);
    }

    private static bool InteractPressed(PlayerInteractor interactor)
    {
        InputAction action = interactor != null ? interactor.InteractAction : null;
        if (action != null && (action.WasPressedThisFrame() || action.WasPerformedThisFrame() || action.triggered))
            return true;
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.wasPressedThisFrame;
    }

    private void OnDisable()
    {
        EndInspect();
    }

    // The item name as a title: first letter up.
    private static string Caption(ItemDefinition item)
    {
        if (item == null || string.IsNullOrEmpty(item.DisplayName))
            return string.Empty;
        string name = item.DisplayName;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    // Skip the inspect for testing (admin panel), or for an item already looked at this session.
    private bool ShouldInspect()
    {
        if (QuickPickups)
            return false;
        // Mid-chase nothing stops to be looked at: it flies straight in, and still counts as unseen, so the first pickup
        // of that item after the chase shows the inspect view.
        if (ChaseSequence.Active != null && ChaseSequence.Active.IsRunning)
            return false;
        bool seenBefore = item != null && seenThisSession.Contains(item);
        if (item != null)
            seenThisSession.Add(item);
        return !(inspectOnlyFirstTime && seenBefore);
    }

    // Hands the item over at the end of the animation. False if the inventory filled up while it was flying: what is
    // left stays here, ready to be picked up again.
    private bool Deliver()
    {
        int stored = pendingInventory != null ? pendingInventory.Add(item, amount) : 0;
        amount -= stored;
        if (amount <= 0)
        {
            onPickedUp.Invoke();
            return true;
        }
        collected = false;
        GetComponent<Collider>().enabled = true;
        return false;
    }

    // Clear View: if the item has no room to hang where you are looking, turn to where it has (the same way at eye
    // level, else the nearest heading at eye level with room, up to right round; else wherever has the most). Returns
    // how far out it can hang that way.
    private float ClearView(Transform eye, SwimController swimmer, float radius)
    {
        float need = Mathf.Max(zoomRange.x, holdDistance * 0.6f);
        float ahead = Room(eye.position, eye.forward, radius);
        if (ahead >= need || swimmer == null)
            return ahead;
        Vector2 look = swimmer.LookAngles;
        Vector2 best = look;
        float bestRoom = ahead;
        float[] turns = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f, 180f };
        foreach (float turn in turns)
        {
            var angles = new Vector2(look.x + turn, 0f);
            float room = Room(eye.position, Quaternion.Euler(angles.y, angles.x, 0f) * Vector3.forward, radius);
            if (room > bestRoom + 0.05f)
            {
                bestRoom = room;
                best = angles;
            }
            if (room >= need)
                break;
        }
        if (best == look)
            return ahead;
        viewRestore = look;
        viewTurned = true;
        TurnView(swimmer, best);
        return bestRoom;
    }

    private void TurnView(SwimController swimmer, Vector2 to)
    {
        if (swimmer == null)
            return;
        viewFrom = swimmer.LookAngles;
        viewTo = to;
        viewTurnStart = Time.time;
    }

    // One frame of the Clear View turn, eased.
    private void StepView(SwimController swimmer)
    {
        if (swimmer == null || viewTurnStart < 0f)
            return;
        float k = clearViewSeconds > 0f ? Mathf.Clamp01((Time.time - viewTurnStart) / clearViewSeconds) : 1f;
        float e = Ease.InOutCubic(k);
        swimmer.SetLookAngles(Mathf.LerpAngle(viewFrom.x, viewTo.x, e), Mathf.Lerp(viewFrom.y, viewTo.y, e));
        if (k >= 1f)
            viewTurnStart = -1f;
    }

    // How far out from `from` the held item (a ball of `radius`) can go along `direction` before touching something
    // solid, other than itself and the player; up to the far end of the zoom range.
    private float Room(Vector3 from, Vector3 direction, float radius)
    {
        float reach = zoomRange.y;
        int count = Physics.SphereCastNonAlloc(from, radius, direction, roomHits, reach, ~0, QueryTriggerInteraction.Ignore);
        float nearest = reach;
        for (int i = 0; i < count; i++)
        {
            Collider hit = roomHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) || PlayerBody.Is(hit) || (roomIgnore != null && hit.transform.IsChildOf(roomIgnore)))
                continue;
            nearest = Mathf.Min(nearest, roomHits[i].distance);
        }
        return Mathf.Max(0f, nearest - 0.05f);
    }

    // About how big the item is while held, as a ball.
    private float HeldRadius()
    {
        Bounds bounds = RendererBounds(visual, out bool any);
        float half = any ? Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) : 0.15f;
        return Mathf.Clamp(half * holdScale * 0.8f, 0.05f, 0.4f);
    }

    // A point in front of the eyes, at Hold Distance. With no camera, just above where the item was.
    private Vector3 HoldPoint(Transform eye, Vector3 fallback)
    {
        return eye != null ? eye.position + eye.forward * holdNow + eye.up * holdHeight : fallback + Vector3.up * 0.4f;
    }

    // The middle of the body (the Character Controller centre), plus Collect Offset in body space.
    private Vector3 Torso(CharacterController body, Transform bodyRoot)
    {
        Vector3 torso = body != null ? body.transform.TransformPoint(body.center) : bodyRoot != null ? bodyRoot.position + Vector3.up : transform.position;
        return bodyRoot != null ? torso + bodyRoot.TransformDirection(collectOffset) : torso;
    }

    // The showcase pose turned to present the same side to the viewer every time, plus `turn` degrees about world up.
    private static Quaternion Showcase(Quaternion rest, Transform eye, float turn)
    {
        float yaw = 0f;
        if (eye != null)
        {
            Vector3 toEye = -eye.forward;   // from the hold point back to the viewer
            toEye.y = 0f;
            if (toEye.sqrMagnitude > 0.001f)
                yaw = Mathf.Atan2(toEye.x, toEye.z) * Mathf.Rad2Deg;   // the rest pose front turned to face the viewer
        }
        return Quaternion.AngleAxis(yaw + turn, Vector3.up) * rest;
    }
}
