using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

// A door: Open()/Close() slide or swing the Visual child between its closed pose and an open one. Press E to use it
// (unless locked) or drive it from events (Item Socket → On Filled → Open). Can shut and lock behind the player once
// they've gone through (the GDD's first room).
// Heavy-hatch feel: an easing curve per direction, a small overshoot-and-settle when it hits the open stop and a
// little bounce when it lands shut. Sound: an unlatch clunk as it starts, a grinding loop that follows its speed, a
// thud at each end (again, quieter, on every bounce) and a rattle when it's locked - all 3D, muffled like underwater.
// Swap the Visual mesh freely; this object is the hinge for swing doors.
public class Door : MonoBehaviour, IInteractable, IPromptTone
{
    public enum Motion { Slide, Swing }

    [Header("Motion")]
    [Tooltip("The part that moves. Empty = first child.")]
    [SerializeField] private Transform visual;
    [SerializeField] private Motion motion = Motion.Slide;
    [Tooltip("Slide: how far the visual moves, in this object's local space. Default: straight up out of the way.")]
    [SerializeField] private Vector3 slideOffset = new Vector3(0f, 2.9f, 0f);
    [Tooltip("Swing: degrees around the hinge. This object is the hinge, so place it at the door's edge.")]
    [SerializeField] private float swingAngle = 100f;
    [Tooltip("Swing: the hinge axis in this object's local space. Up (0,1,0) for a normal door, Forward (0,0,1) for a trapdoor lying flat.")]
    [SerializeField] private Vector3 swingAxis = Vector3.up;
    [Tooltip("Swing: push open away from whoever opens it, so it never swings into the player's face. Off = always the same direction.")]
    [SerializeField] private bool swingAwayFromPlayer = true;
    [Tooltip("Seconds for the full travel.")]
    [SerializeField] private float duration = 1f;
    [Tooltip("Shape of the opening move (x = time 0..1, y = how far along 0..1). Default eases in and out, like something heavy being pushed.")]
    [SerializeField] private AnimationCurve openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Shape of the closing move. Default starts slow and picks up speed, like a hatch dropping under its own weight.")]
    [SerializeField] private AnimationCurve closeCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2.2f, 2.2f));

    [Header("Landing")]
    [Tooltip("How far the door rebounds when it lands shut, as a fraction of its travel. 0 = lands dead.")]
    [SerializeField, Range(0f, 0.3f)] private float bounce = 0.06f;
    [Tooltip("How many rebounds. Each is about a third the height and a bit quicker than the one before.")]
    [SerializeField, Range(0, 4)] private int bounces = 2;
    [Tooltip("Seconds the first rebound takes.")]
    [SerializeField] private float bounceTime = 0.22f;
    [Tooltip("How far it overshoots and settles back when it hits the open stop, as a fraction of its travel. 0 = stops dead.")]
    [SerializeField, Range(0f, 0.2f)] private float openSettle = 0.025f;

    [Header("Behaviour")]
    [SerializeField] private bool startOpen = false;
    [Tooltip("Locked doors ignore E (they rattle). Events (Open, Unlock) still work.")]
    [SerializeField] private bool locked = false;
    [Tooltip("The player can open and close it with E.")]
    [SerializeField] private bool interactable = true;
    [SerializeField] private string openPrompt = "open door";
    [SerializeField] private string closePrompt = "close door";
    [Tooltip("The door closes by itself when the player walks away from it. Needs a trigger collider on this object covering the doorway.")]
    [SerializeField] private bool closeBehindPlayer = false;
    [Tooltip("Close Behind Player: also lock it, and only once the player has actually gone through (the GDD's first room). " +
             "Off = it just shuts behind you and can be opened again from either side.")]
    [SerializeField] private bool lockBehind = true;
    [Tooltip("The local direction that counts as 'through' / the front of the door. Forward for a door, Down (0,-1,0) for a hatch.")]
    [SerializeField] private Vector3 throughAxis = Vector3.forward;

    [Header("Sounds")]
    [Tooltip("One complete opening sound (latch, hinge, travel, stop), played whole when the door opens. When set, Unlatch, Groan, Move Loop and Open Stop only play on closing. Empty = the layered set plays both ways.")]
    [SerializeField] private AudioClip openSound;
    [Tooltip("One complete closing sound (swing and thud), played whole when the door closes. When set, Unlatch, Groan, Move Loop and Close Stop stay silent on closing. Empty = the layered set plays.")]
    [SerializeField] private AudioClip closeSound;
    [Tooltip("Seconds into Close Sound where its thud is. Above 0 the door takes exactly that long to close, so the thud lands with it. 0 = the normal Duration.")]
    [SerializeField] private float closeSoundLandsAt = 0f;
    [Tooltip("The latch / bolt as the door starts to move, opening or closing.")]
    [FormerlySerializedAs("openSound")]
    [SerializeField] private AudioClip unlatchSound;
    [Tooltip("Grinding / scraping that loops while the door moves. Volume and pitch follow its speed.")]
    [SerializeField] private AudioClip moveLoop;
    [Tooltip("The clank when it reaches fully open.")]
    [SerializeField] private AudioClip openStopSound;
    [Tooltip("The thud when it lands shut. Plays again, quieter, on each bounce.")]
    [FormerlySerializedAs("closeSound")]
    [SerializeField] private AudioClip closeStopSound;
    [Tooltip("The rattle when someone tries it while it's locked.")]
    [SerializeField] private AudioClip lockedSound;
    [Tooltip("The tip shown when someone tries it while it is locked: what opens it.")]
    [SerializeField] private string lockedHint = "It's locked. Something nearby must open it.";
    [Tooltip("A long creak / groan that starts with the move and rings on after the door has stopped. The creepy part.")]
    [SerializeField] private AudioClip groanSound;
    [Tooltip("Random pitch range for the groan, so no two doors sound alike.")]
    [SerializeField] private Vector2 groanPitchRange = new Vector2(0.7f, 0.9f);
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [Tooltip("Pitch of every door sound. Below 1 = deeper and heavier.")]
    [SerializeField, Range(0.5f, 1.5f)] private float pitch = 0.95f;
    [Tooltip("Reverb on the door's sounds, so a slam rolls away down the corridor. Off = dry (best for natural recordings).")]
    [SerializeField] private AudioReverbPreset reverb = AudioReverbPreset.Off;
    [Tooltip("Low-pass cutoff in Hz for every door sound, so it sounds muffled through the water. 22000 = no muffling.")]
    [SerializeField] private float muffleCutoff = 3200f;
    [Tooltip("Metres beyond which the door can't be heard.")]
    [SerializeField] private float hearingRange = 30f;

    [Header("Events")]
    public UnityEvent onOpened = new UnityEvent();
    public UnityEvent onClosed = new UnityEvent();

    public bool IsOpen { get; private set; }
    public bool IsLocked => locked;
    public string Prompt => locked ? openPrompt + " (locked)" : IsOpen ? closePrompt : openPrompt;
    public PromptTone Tone => locked ? PromptTone.Blocked : PromptTone.Normal;

    private Vector3 closedPosition;
    private Quaternion closedRotation;
    private float openness;
    private float swingDirection = 1f;
    private Coroutine motionRoutine;
    private AudioSource oneShot;
    private AudioSource loop;
    private AudioSource groan;
    private bool wholeOpen;    // this move is an opening covered by Open Sound, so the layers stay quiet
    private bool wholeClose;   // same for a closing covered by Close Sound

    private void Awake()
    {
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);

        if (visual != null)
        {
            closedPosition = visual.localPosition;
            closedRotation = visual.localRotation;
        }

        oneShot = MakeSource(false);
        loop = MakeSource(true);
        groan = MakeSource(false);
        var muffle = gameObject.AddComponent<AudioLowPassFilter>();
        muffle.cutoffFrequency = muffleCutoff;
        if (reverb != AudioReverbPreset.Off)
            gameObject.AddComponent<AudioReverbFilter>().reverbPreset = reverb;

        IsOpen = startOpen;
        openness = startOpen ? 1f : 0f;
        ApplyPose();
    }

    private AudioSource MakeSource(bool looping)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = looping;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 2f;
        source.maxDistance = Mathf.Max(hearingRange, 3f);
        source.dopplerLevel = 0f;
        source.pitch = pitch;
        return source;
    }

    private void Play(AudioClip clip, float scale = 1f)
    {
        if (clip != null && oneShot != null)
            SoundVariety.OneShot(oneShot, clip, volume * scale, pitch);
    }

    public void Interact(GameObject interactor)
    {
        if (!interactable)
            return;

        if (locked)
        {
            Play(lockedSound);
            HintPopup.Show(lockedBehind ? "It won't open from this side." : lockedHint, 2.5f);
            return;
        }

        // Opening: swing toward the side the player is NOT on. Closing keeps the same arc.
        if (!IsOpen && swingAwayFromPlayer && motion == Motion.Swing)
        {
            Vector3 front = transform.TransformDirection(throughAxis.normalized);
            float side = Vector3.Dot(interactor.transform.position - transform.position, front);
            swingDirection = side >= 0f ? 1f : -1f;
        }
        Toggle();
    }

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);
    public void Toggle() => SetOpen(!IsOpen);
    public void Unlock()
    {
        locked = false;
        lockedBehind = false;
    }

    private bool lockedBehind;   // shut and locked behind the player (a one-way door), not locked by a puzzle
    public void Lock() => locked = true;

    // Locked, and trying it says why (a chase sealing the player in).
    public void Lock(string hint)
    {
        locked = true;
        lockedBehind = false;
        if (!string.IsNullOrEmpty(hint))
            lockedHint = hint;
    }

    public void SetOpen(bool open)
    {
        if (IsOpen == open)
            return;

        IsOpen = open;
        wholeOpen = open && openSound != null;
        wholeClose = !open && closeSound != null;
        if (wholeOpen)
        {
            Play(openSound);
        }
        else if (wholeClose)
        {
            Play(closeSound);
        }
        else
        {
            Play(unlatchSound);
            if (groanSound != null && groan != null)
            {
                groan.pitch = pitch * Random.Range(groanPitchRange.x, groanPitchRange.y);
                groan.PlayOneShot(groanSound, volume);
            }
        }

        if (motionRoutine != null)
            StopCoroutine(motionRoutine);
        motionRoutine = StartCoroutine(Animate(open ? 1f : 0f));
    }

    private IEnumerator Animate(float target)
    {
        float from = openness;
        float seconds = wholeClose && closeSoundLandsAt > 0f ? closeSoundLandsAt : duration;   // land with the recorded thud
        float time = Mathf.Abs(target - from) * seconds;
        AnimationCurve curve = target > from ? openCurve : closeCurve;

        if (moveLoop != null && !wholeOpen && !wholeClose)
        {
            loop.clip = moveLoop;
            loop.volume = 0f;
            loop.Play();
        }

        float previous = openness;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            openness = Mathf.Lerp(from, target, Mathf.Clamp01(curve.Evaluate(t / time)));
            ApplyPose();
            UpdateLoop(previous);
            previous = openness;
            yield return null;
        }

        openness = target;
        ApplyPose();
        StopLoop();

        if (target <= 0f)
        {
            // Lands shut: a thud (unless Close Sound has its own), then a couple of ever-smaller rebounds, each ending in a quieter thud.
            if (!wholeClose)
                Play(closeStopSound);
            for (int i = 0; i < bounces && bounce > 0f; i++)
            {
                float height = bounce * Mathf.Pow(0.35f, i);
                float span = bounceTime * Mathf.Pow(0.7f, i);
                for (float t = 0f; t < span; t += Time.deltaTime)
                {
                    openness = height * Mathf.Sin(Mathf.PI * t / span);
                    ApplyPose();
                    yield return null;
                }
                openness = 0f;
                ApplyPose();
                if (!wholeClose)
                    Play(closeStopSound, 0.6f * Mathf.Pow(0.35f, i));
            }
        }
        else
        {
            // Hits the open stop: a clank (unless Open Sound already has one), and it overshoots a touch and settles back.
            if (!wholeOpen)
                Play(openStopSound);
            if (openSettle > 0f)
            {
                const float span = 0.3f;
                for (float t = 0f; t < span; t += Time.deltaTime)
                {
                    float k = t / span;
                    openness = 1f + openSettle * Mathf.Sin(Mathf.PI * k) * (1f - k);
                    ApplyPose();
                    yield return null;
                }
            }
        }

        openness = target;
        ApplyPose();
        motionRoutine = null;

        if (IsOpen)
            onOpened.Invoke();
        else
            onClosed.Invoke();
    }

    // The grinding loop rises and falls with how fast the door is actually moving this frame.
    private void UpdateLoop(float previous)
    {
        if (moveLoop == null || Time.deltaTime <= 0f)
            return;

        float speed = Mathf.Abs(openness - previous) / Time.deltaTime;      // travel per second
        float nominal = duration > 0f ? 1f / duration : 1f;                 // the whole travel in Duration seconds
        float fraction = Mathf.Clamp01(speed / (nominal * 1.5f));
        float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
        loop.volume = Mathf.Lerp(loop.volume, fraction * volume, blend);
        loop.pitch = pitch * (0.85f + 0.3f * fraction);
    }

    private void StopLoop()
    {
        if (loop != null && loop.isPlaying)
            loop.Stop();
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
        Quaternion swing = Quaternion.AngleAxis(swingAngle * swingDirection * openness, swingAxis.normalized);
        visual.localPosition = swing * closedPosition;
        visual.localRotation = swing * closedRotation;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!closeBehindPlayer || !IsOpen)
            return;
        if (!PlayerBody.Is(other))
            return;

        if (lockBehind)
        {
            Vector3 through = transform.TransformDirection(throughAxis.normalized);
            bool wentThrough = Vector3.Dot(other.transform.position - transform.position, through) > 0f;
            if (!wentThrough)
                return;
            locked = true;
            lockedBehind = true;
        }

        Close();
    }
}
