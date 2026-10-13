using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A crate the player pushes around the floor (the Box prefab). Every box is its own physics body: it falls, rests on the
// floor or on other boxes, and drops when what holds it up is pushed away. It never turns: it always stays upright and
// square, sliding flat when pushed (free to turn, the floor's grip on its bottom tipped it over and rolled it along).
//  - Push: swim at one of its sides and it slides straight along that side's axis at your own pace (Box Push on the
//    player finds it just ahead of you and calls PushWith, so it moves before you bump it: no stop-start); stop
//    swimming and it stops where it is.
//  - Pull: press E on it (its Box Grip, added at start) and it comes along behind you wherever you swim, kept at the
//    distance you took hold of it (Box Push tows it); E again lets go.
//  - Grab: hold the right mouse button on it (Box Push) and it follows where you look, at the distance you grabbed it:
//    turn and it swings round with you, swim backwards and it comes along. Let go and it stops.
//  - Plate: a Pressure Plate draws in a box that comes within Plate Reach of it (level with it, not one stacked on
//    another box), faster the closer it gets; once its middle is within Plate Catch of the plate's it slides over the
//    plate's middle, drops into the pit, squares up and stays there for good: it cannot be pushed off again. A plate
//    that has its box draws in no other.
// It glides: it rests on whatever is under it by looking straight down (not by the physics contact), its collider lifted
// Floor Skin off its real bottom, so it never touches the floor's colliders at all. The floor round the plate pits is
// built of separate slabs, and a crate sliding on them caught on the seams between them and on the pits' far rims (stuck
// half sunk into a pit, held up by friction against the rim for as long as you kept pushing): now it slides over all of
// that, and drops into a pit only when a plate takes it. Frictionless too, so it slides along walls and other crates
// instead of sticking to them.
// Everything goes by the middle of its Box Collider, not by its pivot: the crate model's pivot is at one edge of the
// crate, not in its middle (settling by the pivot put a placed box half beside its pit, floating, off the plate).
[RequireComponent(typeof(Rigidbody))]
public class PushableBox : MonoBehaviour
{
    [Tooltip("It moves at this share of the player's speed along its side (1 = right with them; less feels heavier, and the player leans on it).")]
    [Range(0.3f, 1.2f)] public float pace = 1f;
    [Tooltip("The fastest it slides (m/s).")]
    public float maxSpeed = 3.5f;
    [Tooltip("Drag from the water: how slowly it sinks.")]
    public float waterDrag = 2f;
    [Tooltip("How close its middle has to come to a pressure plate's middle (m, across) to be taken by the plate and drop into place on it.")]
    public float plateCatch = 0.5f;
    [Tooltip("How far from a pressure plate's middle (m, across) the plate starts drawing the box in (0 = never).")]
    public float plateReach = 1.6f;
    [Tooltip("How fast a plate draws the box in (m/s): Plate Pull Min at the edge of its reach, Plate Pull right by it.")]
    public float platePull = 2.2f;
    public float platePullMin = 0.6f;
    [Tooltip("Grabbed or pulled: how briskly it closes on where it should be (1/s).")]
    public float followGain = 6f;

    // Its collider's bottom is lifted this far (m) off the crate's real bottom, and the crate is held with its real
    // bottom on what is under it, so the collider floats clear of the floor: more than the two contact offsets, so the
    // physics never even looks at the floor under it.
    private const float FloorSkin = 0.03f;
    private const float BoxContactOffset = 0.005f;
    // Within this (m) of what is under it, it rests on it; any further and it falls.
    private const float RestSnap = 0.08f;
    // How briskly (1/s) it eases onto what is under it when it lands or what holds it shifts.
    private const float RestGain = 12f;
    // How far below itself (m) it looks for something to stand on.
    private const float LookDown = 0.6f;

    private static readonly Dictionary<Collider, PushableBox> taken = new Dictionary<Collider, PushableBox>();
    private static readonly RaycastHit[] under = new RaycastHit[16];
    private static PressurePlates[] plates;
    private static float platesFoundAt = -10f;
    private static PhysicsMaterial slippery;

    private Rigidbody body;
    private BoxCollider shape;
    private BoxGrip grip;
    private Vector3 pushVelocity;
    private float pushedUntil;
    private bool moving;   // pushed last step: stopped dead the step it ends, so it stays where it was left
    private bool resting = true;
    private bool placed;
    private PressurePlates placedOn;

    public bool Placed => placed;
    // The middle of the crate (its collider), wherever the model's pivot is.
    public Vector3 Centre => shape != null ? transform.TransformPoint(shape.center) : body != null ? body.worldCenterOfMass : transform.position;
    // The crate's real bottom (its model's), under its lifted collider.
    private float Bottom => Centre.y - HalfHeight() - (shape != null ? FloorSkin : 0f);

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.FreezeRotation;   // free to slide, fall and stack, but never to roll
        body.angularVelocity = Vector3.zero;
        body.linearDamping = Mathf.Max(body.linearDamping, waterDrag);
        body.angularDamping = Mathf.Max(body.angularDamping, waterDrag);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        shape = GetComponent<BoxCollider>();
        if (shape != null)
        {
            float lift = FloorSkin / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y));
            shape.center += new Vector3(0f, lift * 0.5f, 0f);
            shape.size -= new Vector3(0f, lift, 0f);
            shape.contactOffset = BoxContactOffset;
            shape.sharedMaterial = Slippery();
        }

        grip = GetComponent<BoxGrip>();
        if (grip == null)
            grip = gameObject.AddComponent<BoxGrip>();
        grip.Setup(this);
    }

    private static PhysicsMaterial Slippery()
    {
        if (slippery == null)
            slippery = new PhysicsMaterial("Box (slippery)")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
        return slippery;
    }

    private void OnDestroy()
    {
        var mine = new List<Collider>();
        foreach (var pair in taken)
            if (pair.Value == this)
                mine.Add(pair.Key);
        foreach (Collider plate in mine)
            taken.Remove(plate);
    }

    // The player is swimming at it with this flat velocity: it goes along its side nearest that heading, at their pace.
    // True when it moves (Box Push shows the push drawing then).
    public bool PushWith(Vector3 playerVelocity)
    {
        if (placed)
            return false;
        playerVelocity.y = 0f;
        Vector3 axis = SideAxis(playerVelocity);
        float speed = Vector3.Dot(playerVelocity, axis) * pace;
        if (axis == Vector3.zero || speed < 0.15f)
            return false;
        pushVelocity = axis * Mathf.Min(speed, maxSpeed);
        pushedUntil = Time.time + 0.08f;   // renewed every step the player keeps swimming at it
        return true;
    }

    // Grabbed or pulled: it slides flat towards `target` (any way, not only along a side), up to Max Speed. Called every
    // step it is held; true while it is still on its way.
    public bool HoldToward(Vector3 target)
    {
        if (placed)
            return false;
        Vector3 to = target - Centre;
        to.y = 0f;
        pushVelocity = Vector3.ClampMagnitude(to * followGain, maxSpeed);
        if (pushVelocity.sqrMagnitude < 0.01f)
            pushVelocity = Vector3.zero;
        pushedUntil = Time.time + 0.08f;
        return pushVelocity != Vector3.zero;
    }

    // The box's own flat axis closest to `d`, so it slides square along a side, never off at an angle.
    private Vector3 SideAxis(Vector3 d)
    {
        d.y = 0f;
        if (d.sqrMagnitude < 1e-4f)
            return Vector3.zero;   // the top or bottom: nothing to push along
        Vector3 f = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 r = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        float df = Vector3.Dot(d, f), dr = Vector3.Dot(d, r);
        return Mathf.Abs(df) >= Mathf.Abs(dr) ? f * Mathf.Sign(df) : r * Mathf.Sign(dr);
    }

    // Up and down: resting on what is under it (held there, a skin above it) or falling. Across: what the push (or grab,
    // or pull) and a nearby plate's pull want; left alone, it drifts to a stop in the water.
    private void FixedUpdate()
    {
        if (placed)
        {
            // In its pit for good: its plate stays down (a resting kinematic body is not always reported to triggers).
            if (placedOn != null && !placedOn.isPressed)
                placedOn.isPressed = true;
            return;
        }

        bool wasResting = resting;
        resting = Support(out float gap);
        body.useGravity = !resting;
        if (wasResting && !resting)
            body.WakeUp();   // what held it up has gone: fall
        if (resting && Catch())
            return;   // over a free plate: it is dropping into it

        Vector3 v = body.linearVelocity;
        Vector3 change = Vector3.zero;
        if (resting)
            change.y = Mathf.Clamp(-gap * RestGain, -2f, 2f) - v.y;

        bool pushed = Time.time <= pushedUntil;
        Vector3 want = pushed ? pushVelocity : Vector3.zero;
        Vector3 pull = PlatePull();
        if (pull != Vector3.zero)
            want = Vector3.ClampMagnitude(want + pull, maxSpeed);
        if (pushed || moving || pull != Vector3.zero)
        {
            moving = want != Vector3.zero;   // the player stopped pushing and no plate pulls: it stops dead
            change.x = want.x - v.x;
            change.z = want.z - v.z;
        }
        if (change.sqrMagnitude > 1e-8f)
            body.AddForce(change, ForceMode.VelocityChange);
    }

    // What is under it: a flat box a little smaller than its footprint swept straight down from its middle. True when
    // its real bottom is on it or within Rest Snap of it; `gap` is how far above it the bottom is (below it: negative).
    private bool Support(out float gap)
    {
        gap = float.PositiveInfinity;
        if (shape == null)
            return false;
        Vector3 centre = Centre;
        Vector3 scale = transform.lossyScale;
        Vector3 half = new Vector3(shape.size.x * Mathf.Abs(scale.x), shape.size.y * Mathf.Abs(scale.y), shape.size.z * Mathf.Abs(scale.z)) * 0.5f;
        var slab = new Vector3(half.x * 0.9f, 0.04f, half.z * 0.9f);
        float bottom = centre.y - half.y - FloorSkin;
        float reach = centre.y - slab.y - bottom + LookDown;
        Quaternion turn = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        int count = Physics.BoxCastNonAlloc(centre, slab, Vector3.down, under, turn, reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = under[i].collider;
            if (hit == null || hit == shape || (hit.attachedRigidbody != null && hit.attachedRigidbody == body) || hit is CharacterController)
                continue;
            if (under[i].distance <= 0f && under[i].point == Vector3.zero)
                continue;   // something it starts inside (not under it)
            nearest = Mathf.Min(nearest, under[i].distance);
        }
        if (float.IsPositiveInfinity(nearest))
            return false;
        float ground = centre.y - slab.y - nearest;
        gap = bottom - ground;
        return gap <= RestSnap;
    }

    // Its middle near enough a free plate it is level with: the plate takes it.
    private bool Catch()
    {
        Vector3 middle = Centre;
        float bottom = Bottom;
        foreach (PressurePlates plate in Plates())
        {
            if (plate == null || !plate.isActiveAndEnabled)
                continue;
            Collider area = plate.GetComponentInChildren<Collider>();
            if (area == null || (taken.TryGetValue(area, out PushableBox owner) && owner != this && owner != null))
                continue;
            Bounds b = area.bounds;
            if (bottom > b.max.y + 0.45f || bottom < b.min.y - 0.1f)
                continue;   // up on another box, or below the plate
            if (new Vector2(b.center.x - middle.x, b.center.z - middle.z).magnitude > plateCatch)
                continue;
            taken[area] = this;
            StartCoroutine(SettleOnto(area));
            return true;
        }
        return false;
    }

    // Towards the nearest free plate within Plate Reach that is level with the box, or nothing.
    private Vector3 PlatePull()
    {
        if (plateReach <= 0f)
            return Vector3.zero;
        Vector3 middle = Centre;
        float bottom = Bottom;
        Collider best = null;
        float nearest = plateReach;
        foreach (PressurePlates plate in Plates())
        {
            if (plate == null || !plate.isActiveAndEnabled)
                continue;
            Collider area = plate.GetComponentInChildren<Collider>();
            if (area == null || (taken.TryGetValue(area, out PushableBox owner) && owner != this && owner != null))
                continue;
            Bounds b = area.bounds;
            if (bottom > b.max.y + 0.6f || bottom < b.min.y - 0.5f)
                continue;   // up on another box, or below the plate
            float d = new Vector2(b.center.x - middle.x, b.center.z - middle.z).magnitude;
            if (d < nearest)
            {
                nearest = d;
                best = area;
            }
        }
        if (best == null)
            return Vector3.zero;
        Vector3 to = best.bounds.center - middle;
        to.y = 0f;
        float speed = Mathf.Lerp(platePull, platePullMin, nearest / plateReach);
        return to.normalized * Mathf.Min(speed, nearest / Time.fixedDeltaTime);
    }

    // Every plate in the scene, looked up again now and then (plates can be added while the game runs).
    private static PressurePlates[] Plates()
    {
        if (plates == null || Time.time - platesFoundAt > 5f || Time.time < platesFoundAt || Missing(plates))
        {
            plates = FindObjectsByType<PressurePlates>(FindObjectsSortMode.None);
            platesFoundAt = Time.time;
        }
        return plates;
    }

    // One of them gone (the scene was loaded again from a checkpoint): look them up again now, not in 5 s.
    private static bool Missing(PressurePlates[] found)
    {
        foreach (PressurePlates plate in found)
            if (plate == null)
                return true;
        return false;
    }

    // Half the height of its (lifted) collider.
    private float HalfHeight()
    {
        return (shape != null ? shape.size.y : 1f) * Mathf.Abs(transform.lossyScale.y) * 0.5f;
    }

    // From its pivot to the middle of its collider, in the world, when it is turned to `rotation`.
    private Vector3 CentreOffset(Quaternion rotation)
    {
        Vector3 local = shape != null ? shape.center : Vector3.zero;
        return rotation * Vector3.Scale(local, transform.lossyScale);
    }

    // Fallen onto a pressure plate from above (off another box): into place for good, the same as being caught.
    private void OnTriggerStay(Collider other)
    {
        if (placed || other.GetComponentInParent<PressurePlates>() == null)
            return;
        if (taken.TryGetValue(other, out PushableBox owner) && owner != null && owner != this)
            return;   // that plate has its box
        Vector3 plate = other.bounds.center;
        Vector3 middle = Centre;
        if (new Vector2(plate.x - middle.x, plate.z - middle.z).magnitude > plateCatch)
            return;
        taken[other] = this;
        StartCoroutine(SettleOnto(other));
    }

    // Slides over the plate's middle, squaring up to it, then drops into the pit, its real bottom on the plate's bottom
    // (the pit floor), and stays: no longer physics, no longer something to pull. Its plate is pressed from then on.
    private IEnumerator SettleOnto(Collider plate)
    {
        placed = true;
        body.isKinematic = true;
        if (grip != null)
            grip.enabled = false;
        placedOn = plate.GetComponentInParent<PressurePlates>();
        if (placedOn != null)
            placedOn.isPressed = true;

        float halfHeight = HalfHeight();
        float plateYaw = plate.transform.eulerAngles.y;
        float yaw = plateYaw + Mathf.Round(Mathf.DeltaAngle(plateYaw, transform.eulerAngles.y) / 90f) * 90f;
        Vector3 fromPosition = transform.position;
        Quaternion fromRotation = transform.rotation;
        Quaternion toRotation = Quaternion.Euler(0f, yaw, 0f);
        // The collider's middle over the plate's middle and the crate's bottom on the pit floor; the pivot goes where that puts it.
        Vector3 toPosition = new Vector3(plate.bounds.center.x, plate.bounds.min.y + FloorSkin + halfHeight, plate.bounds.center.z)
            - CentreOffset(toRotation);
        Vector3 over = new Vector3(toPosition.x, Mathf.Max(fromPosition.y, toPosition.y), toPosition.z);

        // Across, still at the height it came in at (clear of the pit's rim)...
        const float slideSeconds = 0.22f, dropSeconds = 0.18f;
        for (float t = 0f; t < 1f; t += Time.deltaTime / slideSeconds)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            body.MovePosition(Vector3.Lerp(fromPosition, over, s));
            body.MoveRotation(Quaternion.Slerp(fromRotation, toRotation, s));
            yield return null;
        }
        body.MovePosition(over);
        body.MoveRotation(toRotation);
        // ...then down into the pit, picking up speed like something heavy dropping.
        for (float t = 0f; t < 1f; t += Time.deltaTime / dropSeconds)
        {
            body.MovePosition(Vector3.Lerp(over, toPosition, t * t));
            yield return null;
        }
        body.MovePosition(toPosition);
    }
}
