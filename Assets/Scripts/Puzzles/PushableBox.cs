using System.Collections;
using UnityEngine;

// A crate the player pushes around the floor (the Box prefab). Every box is its own physics body: it falls, rests on the
// floor or on other boxes, and drops when what holds it up is pushed away. It never turns: it always stays upright and
// square, sliding flat when pushed (free to turn, the floor's grip on its bottom tipped it over and rolled it along).
//  - Push: swim at one of its sides and it slides straight along that side's axis at your own pace (Box Push on the
//    player finds it just ahead of you and calls PushWith, so it moves before you bump it: no stop-start); stop
//    swimming and it stops where it is.
//  - Plate: once its middle is over a Pressure Plate (the plates sit in shallow pits in the box room) it drops into
//    place, squares up and stays there for good: it cannot be pushed off again.
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

    // Its flat speed brought to what the push wants (up and down stays physics: falling, resting).
    private void FixedUpdate()
    {
        if (placed)
            return;
        Vector3 want;
        if (Time.time <= pushedUntil)
            want = pushVelocity;
        else if (moving)
            want = Vector3.zero;   // the player stopped pushing: so does it
        else
            return;
        moving = want != Vector3.zero;
        Vector3 v = body.linearVelocity;
        body.AddForce(want - new Vector3(v.x, 0f, v.z), ForceMode.VelocityChange);
    }

    // Over a pressure plate: into place for good.
    private void OnTriggerStay(Collider other)
    {
        if (placed || other.GetComponentInParent<PressurePlates>() == null)
            return;
        Vector3 plate = other.bounds.center;
        Vector3 middle = body.worldCenterOfMass;
        if (new Vector2(plate.x - middle.x, plate.z - middle.z).magnitude > plateCatch)
            return;
        StartCoroutine(SettleOnto(other));
    }

    // Glides onto the plate's middle, square to it and upright, resting on the plate's bottom (the pit floor), and
    // stays: no longer physics.
    private IEnumerator SettleOnto(Collider plate)
    {
        placed = true;
        body.isKinematic = true;

        BoxCollider box = GetComponent<BoxCollider>();
        float halfHeight = (box != null ? box.size.y : 1f) * Mathf.Abs(transform.lossyScale.y) * 0.5f;
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
