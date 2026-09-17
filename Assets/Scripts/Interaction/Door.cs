using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// A door: Open()/Close() slide or swing the Visual child between its closed pose and an open one. Press E to use it
// (unless locked) or drive it from events (Item Socket → On Filled → Open). Can shut and lock behind the player once
// they've gone through (the GDD's first room). Swap the Visual mesh freely; this object is the hinge for swing doors.
public class Door : MonoBehaviour, IInteractable
{
    public enum Motion { Slide, Swing }

    [Header("Motion")]
    [Tooltip("The part that moves. Empty = first child.")]
    [SerializeField] private Transform visual;
    [SerializeField] private Motion motion = Motion.Slide;
    [Tooltip("Slide: how far the visual moves, in this object's local space. Default: straight up out of the way.")]
    [SerializeField] private Vector3 slideOffset = new Vector3(0f, 2.9f, 0f);
    [Tooltip("Swing: degrees around this object's up axis. This object is the hinge, so place it at the door's edge.")]
    [SerializeField] private float swingAngle = 100f;
    [SerializeField] private float duration = 1f;

    [Header("Behaviour")]
    [SerializeField] private bool startOpen = false;
    [Tooltip("Locked doors ignore E. Events (Open, Unlock) still work.")]
    [SerializeField] private bool locked = false;
    [Tooltip("The player can open and close it with E.")]
    [SerializeField] private bool interactable = true;
    [Tooltip("Once the player passes through, the door closes and locks for good. Needs a trigger collider on this object " +
             "covering the doorway; 'through' means the side this object's blue arrow points to.")]
    [SerializeField] private bool closeBehindPlayer = false;

    [Header("Feedback")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("Events")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();

    public bool IsOpen { get; private set; }
    public bool IsLocked => locked;
    public string Prompt => locked ? "open door (locked)" : IsOpen ? "close door" : "open door";

    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private float openness;
    private Coroutine motionRoutine;

    private void Awake()
    {
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);

        if (visual != null)
        {
            closedPosition = visual.localPosition;
            closedRotation = visual.localRotation;
        }

        IsOpen = startOpen;
        openness = startOpen ? 1f : 0f;
        ApplyPose();
    }

    public void Interact(GameObject interactor)
    {
        if (!interactable || locked)
            return;
        Toggle();
    }

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);
    public void Toggle() => SetOpen(!IsOpen);
    public void Unlock() => locked = false;
    public void Lock() => locked = true;

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
            return;

        IsOpen = open;
        AudioClip clip = open ? openSound : closeSound;
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);

        if (motionRoutine != null)
            StopCoroutine(motionRoutine);
        motionRoutine = StartCoroutine(Animate(open ? 1f : 0f));
    }

    private IEnumerator Animate(float target)
    {
        float from = openness;
        float time = Mathf.Abs(target - from) * duration;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            openness = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / time));
            ApplyPose();
            yield return null;
        }

        openness = target;
        ApplyPose();
        motionRoutine = null;

        if (IsOpen)
            onOpened.Invoke();
        else
            onClosed.Invoke();
    }

    private void ApplyPose()
    {
        if (visual == null)
            return;

        if (motion == Motion.Slide)
        {
            visual.localPosition = closedPosition + slideOffset * openness;
            visual.localRotation = closedRotation;
            return;
        }

        // Rotate the closed pose around this object's origin, so the hinge is here whatever the mesh's own pivot is.
        Quaternion swing = Quaternion.AngleAxis(swingAngle * openness, Vector3.up);
        visual.localPosition = swing * closedPosition;
        visual.localRotation = swing * closedRotation;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!closeBehindPlayer || !IsOpen)
            return;
        if (other.GetComponentInParent<DeathManager>() == null)
            return;

        bool wentThrough = Vector3.Dot(other.transform.position - transform.position, transform.forward) > 0f;
        if (!wentThrough)
            return;

        locked = true;
        Close();
    }
}
