using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A crate the player pushes around the floor (the Box prefab). Every box is its own physics body: it falls, rests on the
// floor or on other boxes, and drops when what holds it up is pushed away. It never turns: it always stays upright and
// square, sliding flat when pushed (free to turn, the floor's grip on its bottom tipped it over and rolled it along).
//  - Push: swim at one of its sides and it slides straight along that side's axis at your own pace (Box Push on the
//    player finds it just ahead of you and calls PushWith, so it moves before you bump it: no stop-start); stop
//    swimming and it stops where it is.
//  - Grab: hold the right mouse button on it (Box Push) and it follows where you look, at the distance you grabbed it:
//    turn and it swings round with you, swim backwards and it comes along (pulled out of a corner). Let go and it stops.
//  - Plate: a Pressure Plate draws in a box that comes within Plate Reach of it (level with it, not one stacked on
//    another box), faster the closer it gets; once its middle is over the plate it drops into place, squares up and
//    stays there for good: it cannot be pushed off again. A plate that has its box draws in no other.
[RequireComponent(typeof(Rigidbody))]
public class PushableBox : MonoBehaviour
{
    [Tooltip("It moves at this share of the player's speed along its side (1 = right with them; less feels heavier, and the player leans on it).")]
    [Range(0.3f, 1.2f)] public float pace = 1f;
    [Tooltip("The fastest it slides (m/s).")]
    public float maxSpeed = 3.5f;
    [Tooltip("Drag from the water: how slowly it sinks.")]
    public float waterDrag = 2f;
    [Tooltip("How close its middle has to come to a pressure plate's middle (m, across) to drop into place on it.")]
    public float plateCatch = 0.4f;
    [Tooltip("How far from a pressure plate's middle (m, across) the plate starts drawing the box in (0 = never).")]
    public float plateReach = 1.6f;
    [Tooltip("How fast a plate draws the box in (m/s): Plate Pull Min at the edge of its reach, Plate Pull right by it.")]
    public float platePull = 2.2f;
    public float platePullMin = 0.6f;
    [Tooltip("Grabbed: how briskly it closes on the point you are looking at (1/s).")]
    public float followGain = 6f;

    private static readonly Dictionary<Collider, PushableBox> taken = new Dictionary<Collider, PushableBox>();
    private static PressurePlates[] plates;
    private static float platesFoundAt = -10f;

    private Rigidbody body;
    private Vector3 pushVelocity;
    private float pushedUntil;
    private bool moving;   // pushed last step: stopped dead the step it ends, so it stays where it was left
    private bool placed;

    public bool Placed => placed;

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

    // Grabbed: it slides flat towards `target` (any way, not only along a side), up to Max Speed. Called every step it
    // is held; true while it is still on its way.
    public bool HoldToward(Vector3 target)
    {
        if (placed)
            return false;
        Vector3 to = target - body.worldCenterOfMass;
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

    // Its flat speed brought to what the push (or grab) and a nearby plate's pull want (up and down stays physics:
    // falling, resting).
    private void FixedUpdate()
    {
        if (placed)
            return;
        bool pushed = Time.time <= pushedUntil;
        Vector3 want = pushed ? pushVelocity : Vector3.zero;
        Vector3 pull = PlatePull();
        if (pull != Vector3.zero)
            want = Vector3.ClampMagnitude(want + pull, maxSpeed);
        else if (!pushed && !moving)
            return;   // resting: left to physics
        moving = want != Vector3.zero;   // the player stopped pushing and no plate pulls: it stops dead
        Vector3 v = body.linearVelocity;
        body.AddForce(want - new Vector3(v.x, 0f, v.z), ForceMode.VelocityChange);
    }

    // Towards the nearest free plate within Plate Reach that is level with the box, or nothing.
    private Vector3 PlatePull()
    {
        if (plateReach <= 0f)
            return Vector3.zero;
        Vector3 middle = body.worldCenterOfMass;
        float bottom = middle.y - HalfHeight();
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
        if (plates == null || Time.time - platesFoundAt > 5f || Time.time < platesFoundAt)
        {
            plates = FindObjectsByType<PressurePlates>(FindObjectsSortMode.None);
            platesFoundAt = Time.time;
        }
        return plates;
    }

    private float HalfHeight()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        return (box != null ? box.size.y : 1f) * Mathf.Abs(transform.lossyScale.y) * 0.5f;
    }

    // Over a pressure plate: into place for good.
    private void OnTriggerStay(Collider other)
    {
        if (placed || other.GetComponentInParent<PressurePlates>() == null)
            return;
        if (taken.TryGetValue(other, out PushableBox owner) && owner != null && owner != this)
            return;   // that plate has its box
        Vector3 plate = other.bounds.center;
        Vector3 middle = body.worldCenterOfMass;
        if (new Vector2(plate.x - middle.x, plate.z - middle.z).magnitude > plateCatch)
            return;
        taken[other] = this;
        StartCoroutine(SettleOnto(other));
    }

    // Glides onto the plate's middle, square to it and upright, resting on the plate's bottom (the pit floor), and
    // stays: no longer physics.
    private IEnumerator SettleOnto(Collider plate)
    {
        placed = true;
        body.isKinematic = true;

        float halfHeight = HalfHeight();
        float plateYaw = plate.transform.eulerAngles.y;
        float yaw = plateYaw + Mathf.Round(Mathf.DeltaAngle(plateYaw, transform.eulerAngles.y) / 90f) * 90f;
        Vector3 fromPosition = transform.position;
        Quaternion fromRotation = transform.rotation;
        Vector3 toPosition = new Vector3(plate.bounds.center.x, plate.bounds.min.y + halfHeight, plate.bounds.center.z);
        Quaternion toRotation = Quaternion.Euler(0f, yaw, 0f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.25f)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            body.MovePosition(Vector3.Lerp(fromPosition, toPosition, s));
            body.MoveRotation(Quaternion.Slerp(fromRotation, toRotation, s));
            yield return null;
        }
        body.MovePosition(toPosition);
        body.MoveRotation(toRotation);
    }
}
