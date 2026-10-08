using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// The treasure chest (Prefabs/TreasureChest, made by the builder from Art/Models/objects/treasure_chest: the base and the
// lid as one object). The lid sits on a hinge (Lid) along the chest's back edge and swings up by Open Euler when it
// opens. One thing to look at and press E on:
//  - With an Item Socket on the chest (the chest room's, wanting the symbol key) E goes to the socket, its prompt and
//    colour too, and the builder wires the socket's On Filled to Open: put the key in and the lid swings up.
//  - Without one, E simply opens it.
// Once open it stays open and stops being a target, so E reaches what lies in it. Start Open = open from the start
// (the box room's chest, with the bone fragment showing). On Opened fires as the lid starts to move.
// Contents: what is in it (the pickups), placed inside the chest (so nothing shows through a shut one, in the editor
// too). Hidden while it is shut; as the lid goes up they rise out of it one after another, straight up from where they
// lie to Float Above over the top of the chest, clear of it. A chest that starts open has them floating there from the
// start.
public class TreasureChest : MonoBehaviour, IInteractable, IPromptTone
{
    [Tooltip("The hinge the lid turns on (its child is the lid model).")]
    [SerializeField] private Transform lid;
    [Tooltip("The hinge's turn when open, on top of its closed pose (degrees; the builder works out which way is up).")]
    [SerializeField] private Vector3 openEuler = new Vector3(-105f, 0f, 0f);
    [SerializeField] private float openSeconds = 0.9f;
    [SerializeField] private bool startOpen;
    [SerializeField] private string openPrompt = "open chest";
    [SerializeField] private AudioClip openSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

    [Header("Contents")]
    [Tooltip("What is in it, placed inside the chest where each one lies.")]
    [SerializeField] private Transform[] contents = new Transform[0];
    [Tooltip("Where they float once out: this far over the top of the shut chest (its collider), metres.")]
    [SerializeField] private float floatAbove = 0.45f;
    [Tooltip("Seconds after the lid starts up before the first thing rises out.")]
    [SerializeField] private float riseDelay = 0.35f;
    [Tooltip("How long each takes to rise out.")]
    [SerializeField] private float riseSeconds = 0.9f;
    [Tooltip("Pause between one thing and the next.")]
    [SerializeField] private float riseStagger = 0.2f;

    [Header("Events")]
    public UnityEvent onOpened = new UnityEvent();

    [SerializeField, HideInInspector] private int madeVersion;   // which make of the builder's prefab this is

    public int MadeVersion => madeVersion;

    private Quaternion closedPose;
    private bool posed;
    private Vector3[] restAt = new Vector3[0];

    public bool IsOpen { get; private set; }

    // The lock on the chest, if it has one (an Item Socket on this object).
    private ItemSocket Lock => GetComponent<ItemSocket>();

    public string Prompt => Lock != null && !Lock.IsFilled ? Lock.Prompt : openPrompt;
    public PromptTone Tone => Lock != null && !Lock.IsFilled ? Lock.Tone : PromptTone.Normal;

    private void Awake()
    {
        Pose();
        restAt = new Vector3[contents != null ? contents.Length : 0];
        for (int i = 0; i < restAt.Length; i++)
            if (contents[i] != null)
            {
                Vector3 lies = contents[i].position;
                restAt[i] = new Vector3(lies.x, TopY() + floatAbove, lies.z);   // straight above where it lies
                if (startOpen)
                    contents[i].position = restAt[i];
                else
                    contents[i].gameObject.SetActive(false);   // shut in until it opens
            }
        if (startOpen)
        {
            IsOpen = true;
            if (lid != null)
                lid.localRotation = closedPose * Quaternion.Euler(openEuler);
            enabled = false;
        }
    }

    private void Pose()
    {
        if (posed || lid == null)
            return;
        closedPose = lid.localRotation;
        posed = true;
    }

    public void Interact(GameObject interactor)
    {
        if (IsOpen)
            return;
        ItemSocket socket = Lock;
        if (socket != null && !socket.IsFilled)
        {
            socket.Interact(interactor);   // the key goes in; its On Filled opens the lid
            return;
        }
        Open();
    }

    // Swings the lid up (once). Wired to the lock socket's On Filled by the builder.
    public void Open()
    {
        if (IsOpen)
            return;
        IsOpen = true;
        Pose();
        enabled = false;   // open for good: no longer a target, so E reaches what is inside
        if (openSound != null)
            SoundVariety.PlayAt(openSound, transform.position, volume);
        onOpened.Invoke();
        if (lid != null && gameObject.activeInHierarchy)
            StartCoroutine(Swing());   // runs on with the component switched off
        else if (lid != null)
            lid.localRotation = closedPose * Quaternion.Euler(openEuler);
        if (gameObject.activeInHierarchy)
            StartCoroutine(RiseOut());
        else
            for (int i = 0; i < restAt.Length; i++)
                if (contents[i] != null)
                {
                    contents[i].position = restAt[i];
                    contents[i].gameObject.SetActive(true);
                }
    }

    // The top of the shut chest (its collider), in the world.
    private float TopY()
    {
        var box = GetComponent<BoxCollider>();
        return box != null ? transform.TransformPoint(box.center + Vector3.up * (box.size.y * 0.5f)).y : transform.position.y + 1f;
    }

    // Deep inside: the middle of the chest, low down, under each thing's spot (across) so it comes straight up.
    private Vector3 Inside(Vector3 rest)
    {
        var box = GetComponent<BoxCollider>();
        Vector3 middle = box != null ? transform.TransformPoint(box.center) : transform.position;
        float low = box != null ? box.bounds.min.y + box.bounds.size.y * 0.3f : transform.position.y;
        Vector3 local = transform.InverseTransformPoint(rest);
        Vector3 inside = transform.InverseTransformPoint(middle);
        if (box != null)
        {
            // Under its spot, kept well inside the walls.
            inside.x = Mathf.Clamp(local.x, box.center.x - box.size.x * 0.3f, box.center.x + box.size.x * 0.3f);
            inside.z = Mathf.Clamp(local.z, box.center.z - box.size.z * 0.25f, box.center.z + box.size.z * 0.25f);
        }
        Vector3 at = transform.TransformPoint(inside);
        at.y = low;
        return at;
    }

    // One after another, each comes up out of the chest, quick at first and easing into its spot.
    private IEnumerator RiseOut()
    {
        if (restAt.Length == 0)
            yield break;
        yield return new WaitForSeconds(riseDelay);
        for (int i = 0; i < restAt.Length; i++)
        {
            if (contents[i] == null)
                continue;
            StartCoroutine(Rise(contents[i], Inside(restAt[i]), restAt[i]));
            yield return new WaitForSeconds(riseStagger);
        }
    }

    private IEnumerator Rise(Transform thing, Vector3 from, Vector3 to)
    {
        var grab = thing.GetComponent<Collider>();
        thing.position = from;
        thing.gameObject.SetActive(true);
        if (grab != null)
            grab.enabled = false;   // not to be taken half way out
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, riseSeconds))
        {
            if (thing == null)
                yield break;
            thing.position = Vector3.LerpUnclamped(from, to, Ease.OutBack(t));
            yield return null;
        }
        if (thing == null)
            yield break;
        thing.position = to;
        if (grab != null)
            grab.enabled = true;
    }

    private IEnumerator Swing()
    {
        Quaternion open = closedPose * Quaternion.Euler(openEuler);
        for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(0.01f, openSeconds))
        {
            // Quick to start, settling at the top with a little overshoot, like a heavy lid thrown back.
            float s = 1f - Mathf.Pow(1f - t, 3f);
            s += Mathf.Sin(t * Mathf.PI) * 0.06f * t;
            lid.localRotation = Quaternion.SlerpUnclamped(closedPose, open, s);
            yield return null;
        }
        lid.localRotation = open;
    }
}
