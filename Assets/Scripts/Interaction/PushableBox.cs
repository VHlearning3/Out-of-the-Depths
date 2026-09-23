using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// A crate you push with both hands, as the GDD storyboard draws it (POV: pushing box): look at it and press E to put
// your hands on the face you are at; the view settles on that face, straight ahead and a little down, where the hands
// go. Then W pushes it straight away from you and S pulls it back (so a crate in a corner is never stuck for good),
// at Push Speed, along the room's axes; it stops short of walls, other crates and anything solid, and you move with
// it. E again (or swimming up / down) lets go. No hand art yet: the Hands object is where it would show. Needs a solid
// collider; it gets a kinematic Rigidbody so Pressure Plates and triggers notice it.
[RequireComponent(typeof(Collider))]
public class PushableBox : MonoBehaviour, IInteractable
{
    [Tooltip("How fast it slides while pushed or pulled, in metres per second.")]
    [SerializeField] private float pushSpeed = 1.4f;
    [Tooltip("S drags it back towards you.")]
    [SerializeField] private bool canPull = true;
    [Tooltip("How far from the crate's face you hold it, in metres (about an arm).")]
    [SerializeField] private float holdDistance = 0.7f;
    [Tooltip("How far below straight ahead the view settles while holding it, in degrees: the hands sit on the crate.")]
    [SerializeField] private float lookDown = 12f;
    [Tooltip("Gap kept from walls and other crates, in metres.")]
    [SerializeField] private float skin = 0.03f;
    [SerializeField] private string prompt = "push";
    [SerializeField] private string holdingPrompt = "let go   (W push, S pull)";
    [Tooltip("A scrape while it moves (optional), looped.")]
    [SerializeField] private AudioClip slideSound;
    [SerializeField, Range(0f, 1f)] private float slideVolume = 0.5f;

    private static readonly List<PushableBox> all = new List<PushableBox>();
    public static IReadOnlyList<PushableBox> All => all;

    public bool Held => holder != null;
    public bool Moving { get; private set; }
    public string Prompt => Held ? holdingPrompt : prompt;

    private Rigidbody body;
    private Collider solid;
    private AudioSource scrape;

    private SwimController holder;
    private CharacterController holderBody;
    private InputAction moveAction;
    private InputAction upAction;
    private InputAction downAction;
    private Vector3 face;          // the face held: outward, along an axis
    private float settle;          // 0..1: gliding into place after grabbing
    private bool frozenWas;
    private bool lookWas;
    private SlashAttack slash;
    private bool attackWas;

    private void Awake()
    {
        solid = GetComponent<Collider>();
        body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void OnEnable() => all.Add(this);

    private void OnDisable()
    {
        all.Remove(this);
        LetGo();
    }

    public void Interact(GameObject who)
    {
        if (Held)
            LetGo();
        else
            Grab(who);
    }

    // Hands on the face nearest the player.
    private void Grab(GameObject who)
    {
        SwimController swimmer = who != null ? who.GetComponentInParent<SwimController>() : null;
        if (swimmer == null)
            return;
        Vector3 fromBox = swimmer.transform.position - solid.bounds.center;
        fromBox.y = 0f;
        face = Mathf.Abs(fromBox.x) >= Mathf.Abs(fromBox.z) ? new Vector3(Mathf.Sign(fromBox.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(fromBox.z));

        holder = swimmer;
        holderBody = swimmer.GetComponent<CharacterController>();
        InputActionMap map = swimmer.InputActions != null ? swimmer.InputActions.FindActionMap("Player") : null;
        moveAction = map?.FindAction("Move");
        upAction = map?.FindAction("Jump");
        downAction = map?.FindAction("Crouch");
        frozenWas = swimmer.Frozen;
        lookWas = swimmer.LookLocked;
        swimmer.Frozen = true;       // normal swimming stops: W and S push and pull instead
        swimmer.LookLocked = true;   // the view stays on the crate
        slash = swimmer.GetComponentInChildren<SlashAttack>();
        if (slash != null)
        {
            attackWas = slash.CanAttack;
            slash.SetCanAttack(false);   // both hands are on the crate
        }
        settle = 0f;
    }

    public void LetGo()
    {
        if (holder == null)
            return;
        holder.Frozen = frozenWas;
        holder.LookLocked = lookWas;
        if (slash != null)
            slash.SetCanAttack(attackWas);
        slash = null;
        holder = null;
        holderBody = null;
        Moving = false;
        UpdateSound();
    }

    private void Update()
    {
        if (holder == null)
            return;
        if (!holder.isActiveAndEnabled || PauseMenu.IsOpen)
            return;
        if ((upAction != null && upAction.WasPressedThisFrame()) || (downAction != null && downAction.WasPressedThisFrame()))
        {
            LetGo();
            return;
        }

        // Glide into place at arm's length from the face, and settle the view on it.
        Bounds shape = solid.bounds;
        float halfDepth = Mathf.Abs(Vector3.Dot(shape.extents, face));
        Vector3 spot = shape.center + face * (halfDepth + holdDistance);
        spot.y = holder.transform.position.y;
        settle = Mathf.MoveTowards(settle, 1f, Time.deltaTime / 0.25f);
        Vector3 toSpot = spot - holder.transform.position;
        if (toSpot.sqrMagnitude > 0.0001f)
            MoveHolder(toSpot * (settle >= 1f ? 1f : 1f - Mathf.Exp(-12f * Time.deltaTime)));
        Vector2 look = holder.AnglesToward(shape.center + face * halfDepth);
        Vector2 now = holder.LookAngles;
        float blend = 1f - Mathf.Exp(-10f * Time.deltaTime);
        holder.SetLookAngles(Mathf.LerpAngle(now.x, look.x, blend), Mathf.Lerp(now.y, look.y + lookDown, blend));

        // W pushes it away from you, S pulls it back.
        float input = moveAction != null ? moveAction.ReadValue<Vector2>().y : 0f;
        Moving = false;
        if (settle >= 1f && Mathf.Abs(input) > 0.3f && (input > 0f || canPull))
        {
            Vector3 direction = input > 0f ? -face : face;
            float step = Clear(direction, pushSpeed * Time.deltaTime);
            if (input < 0f && step > 0f)
            {
                // Pulling: the player backs up first; the crate follows as far as they got.
                Vector3 before = holder.transform.position;
                MoveHolder(direction * step);
                step = Mathf.Min(step, Vector3.Dot(holder.transform.position - before, direction));
                if (step > 0.0001f)
                    MoveBox(direction * step);
            }
            else if (step > 0.0001f)
            {
                MoveBox(direction * step);
                MoveHolder(direction * step);
            }
            Moving = step > 0.0001f;
        }
        UpdateSound();
    }

    // How far it can slide this way, up to want: its own shape cast ahead, a hair smaller all round so the floor it
    // rests on never counts, stopping short of the first solid thing that is not itself or the player.
    private float Clear(Vector3 direction, float want)
    {
        const float inset = 0.05f;
        Bounds shape = solid.bounds;
        float step = want;
        foreach (RaycastHit hit in Physics.BoxCastAll(shape.center, shape.extents - Vector3.one * inset, direction, Quaternion.identity, want + skin + inset, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == solid || PlayerBody.Is(hit.collider))
                continue;
            if (hit.distance <= 0f && hit.point == Vector3.zero)
                continue;   // already overlapping at the start: not ahead of it
            step = Mathf.Min(step, Mathf.Max(0f, hit.distance - inset - skin));
        }
        return step;
    }

    private void MoveBox(Vector3 delta)
    {
        transform.position += delta;
        body.position = transform.position;
    }

    private void MoveHolder(Vector3 delta)
    {
        if (holderBody != null && holderBody.enabled)
            holderBody.Move(delta);
        else
            holder.transform.position += delta;
    }

    private void UpdateSound()
    {
        if (slideSound == null)
            return;
        if (scrape == null)
        {
            scrape = gameObject.AddComponent<AudioSource>();
            scrape.clip = slideSound;
            scrape.loop = true;
            scrape.spatialBlend = 1f;
            scrape.playOnAwake = false;
            scrape.volume = 0f;
        }
        scrape.volume = Mathf.MoveTowards(scrape.volume, Moving ? slideVolume : 0f, Time.deltaTime * 4f);
        if (Moving && !scrape.isPlaying)
            scrape.Play();
        else if (!Moving && scrape.isPlaying && scrape.volume <= 0.001f)
            scrape.Stop();
    }

    // Is its middle over this footprint (a plate's area), within the height given?
    public bool Over(Vector3 centre, Vector2 halfSize, float height)
    {
        Vector3 at = solid != null ? solid.bounds.center : transform.position;
        return Mathf.Abs(at.x - centre.x) <= halfSize.x && Mathf.Abs(at.z - centre.z) <= halfSize.y && Mathf.Abs(at.y - centre.y) <= height;
    }
}
