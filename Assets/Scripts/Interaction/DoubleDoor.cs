using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// A big double door (the symbol room's): two leaves that swing open together, away from whoever opens them, and one
// lock plate where the key goes. This object is the doorway: Leaf_Left and Leaf_Right under it are the hinges at the
// outer edges of the opening, each with a Visual panel to swap for the real art (the frame round it is built by the
// scene builders). Press E on it, or drive it from an Item Socket (On Filled -> Open): that unlocks it and opens
// both leaves. The lock plate sits on the right leaf, so it swings with the door.
public class DoubleDoor : MonoBehaviour, IInteractable, IPromptTone
{
    [Header("Leaves")]
    [Tooltip("The hinge at the left edge of the opening; its Visual child is the panel.")]
    [SerializeField] private Transform leftLeaf;
    [Tooltip("The hinge at the right edge; its Visual child is the panel and Lock is the plate the key goes into.")]
    [SerializeField] private Transform rightLeaf;
    [Tooltip("The plate the key goes into (the builders put the Item Socket on it).")]
    [SerializeField] private GameObject lockPlate;
    [Tooltip("Degrees each leaf swings.")]
    [SerializeField] private float swingAngle = 95f;
    [Tooltip("Seconds for a full swing.")]
    [SerializeField] private float duration = 1.8f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Swing away from whoever opens it, so the leaves never come at the player. Off = always toward -Z.")]
    [SerializeField] private bool swingAwayFromPlayer = true;

    [Header("Lock")]
    [SerializeField] private bool locked = true;
    [SerializeField] private string openPrompt = "open the doors";
    [SerializeField] private string closePrompt = "close the doors";

    [Header("Sounds")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioClip lockedSound;
    [Tooltip("The tip shown when someone tries it while it is locked: what opens it.")]
    [SerializeField] private string lockedHint = "It's locked. Something nearby must open it.";
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();

    public bool IsOpen { get; private set; }
    public bool IsLocked => locked;
    public GameObject LockPlate => lockPlate;
    // While locked, the whole door stands in for its lock plate: the doorway's trigger is what the reticle finds, so
    // the plate's socket would never be reached on its own. Its prompt and colour show, and E with the key uses it.
    public string Prompt => LockSocket != null ? LockSocket.Prompt : locked ? openPrompt + " (locked)" : IsOpen ? closePrompt : openPrompt;
    public PromptTone Tone => LockSocket != null ? LockSocket.Tone : locked ? PromptTone.Blocked : PromptTone.Normal;

    // The lock plate's Item Socket while the door is locked and the key is not in yet, else null.
    private ItemSocket LockSocket
    {
        get
        {
            if (!locked || lockPlate == null)
                return null;
            if (lockSocket == null)
                lockSocket = lockPlate.GetComponent<ItemSocket>();
            return lockSocket != null && lockSocket.isActiveAndEnabled && !lockSocket.IsFilled ? lockSocket : null;
        }
    }
    private ItemSocket lockSocket;

    private Quaternion leftRest = Quaternion.identity;
    private Quaternion rightRest = Quaternion.identity;
    private float openness;          // 0 = shut, 1 = open
    private float direction = 1f;    // +1 = the leaves swing toward -Z
    private Coroutine motion;
    private AudioSource source;

    private void Awake()
    {
        if (leftLeaf != null)
            leftRest = leftLeaf.localRotation;
        if (rightLeaf != null)
            rightRest = rightLeaf.localRotation;
    }

    public void Interact(GameObject interactor)
    {
        ItemSocket socket = LockSocket;
        if (socket != null && socket.RequiredItem != null)
        {
            // Carrying the key: it goes into the lock plate (which opens the door through its On Filled).
            var inventory = interactor != null ? interactor.GetComponentInParent<PlayerInventory>() : null;
            if (inventory != null && inventory.Count(socket.RequiredItem) > 0)
            {
                socket.Interact(interactor);
                return;
            }
        }
        if (locked)
        {
            Play(lockedSound);
            HintPopup.Show(lockedHint, 2.5f);
            return;
        }
        if (!IsOpen && swingAwayFromPlayer && interactor != null)
            direction = SideOf(interactor.transform.position);
        SetOpen(!IsOpen);
    }

    // From an Item Socket (On Filled) or a puzzle: the key is in, so unlock and open, away from the player.
    public void Open()
    {
        locked = false;
        if (!IsOpen && swingAwayFromPlayer)
        {
            Camera camera = Camera.main;
            if (camera != null)
                direction = SideOf(camera.transform.position);
        }
        SetOpen(true);
    }

    public void Close() => SetOpen(false);
    public float Direction => direction;

    // Checkpoint Save: straight into this state, no sound, no events.
    public void RestoreState(bool open, bool isLocked, float swingDirection)
    {
        if (motion != null)
            StopCoroutine(motion);
        motion = null;
        direction = swingDirection == 0f ? 1f : swingDirection;
        IsOpen = open;
        openness = open ? 1f : 0f;
        Pose();
        locked = isLocked;
    }
    public void Unlock() => locked = false;
    public void Lock() => locked = true;

    // +1 when the point is on the +Z side of the doorway: the leaves then swing toward -Z, away from it.
    private float SideOf(Vector3 point)
    {
        return Vector3.Dot(point - transform.position, transform.forward) >= 0f ? 1f : -1f;
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
            return;
        IsOpen = open;
        Play(open ? openSound : closeSound);
        if (motion != null)
            StopCoroutine(motion);
        motion = StartCoroutine(Swing(open ? 1f : 0f));
    }

    private IEnumerator Swing(float target)
    {
        float from = openness;
        float time = Mathf.Abs(target - from) * Mathf.Max(0.01f, duration);
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            openness = Mathf.Lerp(from, target, curve.Evaluate(t / time));
            Pose();
            yield return null;
        }
        openness = target;
        Pose();
        motion = null;
        (target > 0.5f ? onOpened : onClosed).Invoke();
    }

    // The left leaf turns one way round its hinge, the right leaf the other, so both end up on the same side.
    private void Pose()
    {
        float angle = swingAngle * openness * direction;
        if (leftLeaf != null)
            leftLeaf.localRotation = leftRest * Quaternion.AngleAxis(angle, Vector3.up);
        if (rightLeaf != null)
            rightLeaf.localRotation = rightRest * Quaternion.AngleAxis(-angle, Vector3.up);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
            return;
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.minDistance = 2f;
            source.maxDistance = 30f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
        SoundVariety.OneShot(source, clip, volume);
    }
}
