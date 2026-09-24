using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// One drawer that E opens and closes. Either an Animator does the sliding (E flips its Open Parameter bool: "Auki" on
// the VetoLaatikko dresser, whose clips slide Vetolaatikko.001 out and back) or, with no Animator, the Drawer part
// itself glides by Slide over Animation Seconds (the dresser's other drawers). A press while it is still moving is
// ignored. On Opened / On Closed fire as it starts to move.
// Things put in it with Stash (the first-room key, by Drawer Loot) sit at Stash Point, ride out and back with it, and
// are switched off while it is shut, so nothing inside can be seen, lit or picked up through the front. Something
// taken out (a picked-up key switches itself off) stays gone: only what the drawer hid itself comes back.
// Put it on the drawer part, with colliders for E to find (the builder adds one on the front and one on the bottom).
public class AnimatedDrawer : MonoBehaviour, IInteractable
{
    [Tooltip("The part that slides. Empty = this object.")]
    [SerializeField] private Transform drawer;
    [Tooltip("Slide it with this Animator's Open Parameter. Empty = slide the Drawer part by code instead.")]
    [SerializeField] private Animator animator;
    [Tooltip("The Animator's bool that opens it (true) and closes it (false).")]
    [SerializeField] private string openParameter = "Auki";
    [Tooltip("Without an Animator: how far it slides out, in its parent's space (the dresser's drawers: 0.497 m forward, as in the Animator clips).")]
    [SerializeField] private Vector3 slide = new Vector3(0f, 0f, 0.497f);
    [Tooltip("How long the open / close takes, in seconds: presses during it are ignored.")]
    [SerializeField] private float animationSeconds = 1f;
    [Tooltip("Where stashed things sit, in the drawer's local space.")]
    [SerializeField] private Vector3 stashPoint;
    [Tooltip("Can it be closed again once open.")]
    [SerializeField] private bool canClose = true;
    [SerializeField] private string openPrompt = "open drawer";
    [SerializeField] private string closePrompt = "close drawer";
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;
    [Tooltip("Opened with something stashed in it (the key): it stays open and E takes what is inside; once that is taken the drawer is done (no more E). Off = it opens and closes like the empty ones.")]
    [SerializeField] private bool keepOpenWhileFull = true;

    [Header("Events")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();

    public bool IsOpen { get; private set; }
    public string Prompt
    {
        get
        {
            PickupItem inside = IsOpen && keepOpenWhileFull ? Inside() : null;
            return inside != null ? inside.Prompt : IsOpen ? closePrompt : openPrompt;
        }
    }

    private bool openedFull;   // it was opened with something in it: done once that is taken

    // Something stashed in it that is still there to take.
    private PickupItem Inside()
    {
        foreach (Transform thing in stashed)
        {
            if (thing == null || !thing.gameObject.activeInHierarchy)
                continue;
            var pickup = thing.GetComponent<PickupItem>();
            if (pickup != null && !pickup.Collected)
                return pickup;
        }
        return null;
    }
    public Transform Part => drawer != null ? drawer : transform;

    private readonly List<Transform> stashed = new List<Transform>();
    private readonly HashSet<Transform> hiddenByMe = new HashSet<Transform>();
    private Vector3 closedPosition;
    private float moveStart = -10f;
    private float busyUntil;
    private float hideAt = -1f;

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        closedPosition = Part.localPosition;
    }

    public void Interact(GameObject who)
    {
        if (Time.time < busyUntil)
            return;
        // Open with something in it: E takes it out (much easier than aiming at a key inside a drawer).
        PickupItem inside = IsOpen && keepOpenWhileFull ? Inside() : null;
        if (inside != null)
        {
            inside.Interact(who);
            return;
        }
        if (IsOpen && (!canClose || openedFull))
            return;
        SetOpen(!IsOpen);
    }

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);

    public void SetOpen(bool open)
    {
        if (open == IsOpen)
            return;
        IsOpen = open;
        busyUntil = Time.time + animationSeconds;
        moveStart = Time.time;
        if (animator != null && !string.IsNullOrEmpty(openParameter))
            animator.SetBool(openParameter, open);
        if (open)
        {
            hideAt = -1f;
            ShowStashed();
            if (keepOpenWhileFull && Inside() != null)
                openedFull = true;
        }
        else
        {
            hideAt = Time.time + animationSeconds;   // once it is shut
        }
        AudioClip clip = open ? openSound : closeSound;
        if (clip != null)
            SoundVariety.PlayAt(clip, transform.position, volume);
        (open ? onOpened : onClosed).Invoke();
    }

    // Puts something inside: at Stash Point, riding with the drawer, and hidden while it is shut.
    public void Stash(Transform thing)
    {
        if (thing == null || stashed.Contains(thing))
            return;
        stashed.Add(thing);
        thing.position = Part.TransformPoint(stashPoint);
        if (!IsOpen)
            Hide(thing);
    }

    private void Update()
    {
        // Opened with something in it and that has been taken: the drawer is done, it stays open and E ignores it.
        if (openedFull && Time.time >= busyUntil && Inside() == null)
        {
            openedFull = false;
            enabled = false;   // nothing for E here any more (the interactor skips a switched-off one)
        }
        if (hideAt >= 0f && Time.time >= hideAt)
        {
            hideAt = -1f;
            if (!IsOpen)
                foreach (Transform thing in stashed)
                    Hide(thing);
        }
        if (animator == null)
        {
            float k = animationSeconds > 0f ? Mathf.Clamp01((Time.time - moveStart) / animationSeconds) : 1f;
            float open = Mathf.SmoothStep(0f, 1f, IsOpen ? k : 1f - k);
            Part.localPosition = closedPosition + slide * open;
        }
    }

    // After the Animator has moved the drawer this frame: what is inside goes with it.
    private void LateUpdate()
    {
        for (int i = stashed.Count - 1; i >= 0; i--)
        {
            if (stashed[i] == null)
            {
                stashed.RemoveAt(i);
                continue;
            }
            if (stashed[i].gameObject.activeSelf)
                stashed[i].position = Part.TransformPoint(stashPoint);
        }
    }

    private void Hide(Transform thing)
    {
        if (thing == null || !thing.gameObject.activeSelf)
            return;   // already gone (picked up)
        thing.gameObject.SetActive(false);
        hiddenByMe.Add(thing);
    }

    private void ShowStashed()
    {
        foreach (Transform thing in stashed)
        {
            if (thing == null || !hiddenByMe.Remove(thing))
                continue;
            thing.position = Part.TransformPoint(stashPoint);
            thing.gameObject.SetActive(true);
        }
    }
}
