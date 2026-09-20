using UnityEngine;

// The sound of moving through the water. A looping water-rush swells with your speed and dies away when you stop,
// and single swim strokes play in rhythm while you move - faster swimming, faster strokes. Strokes can be short
// one-shots or long hydrophone recordings: a long clip plays a random faded slice each time, so one real recording
// gives endless different strokes. Lives on the Player next to SwimController. Swap the clips in the Inspector.
[RequireComponent(typeof(SwimController))]
public class SwimAudio : MonoBehaviour
{
    [Header("Movement loop")]
    [Tooltip("Looping water-rush sound. Silent when still, full volume at top speed.")]
    [SerializeField] private AudioClip movementLoop;
    [SerializeField, Range(0f, 1f)] private float loopMaxVolume = 0.1f;
    [Tooltip("How loud the loop is at each speed (x = speed 0..1, y = volume 0..1). Bowed down = quiet until you really get going.")]
    [SerializeField] private AnimationCurve loopVolumeBySpeed = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Loop pitch when barely moving (x) and at top speed (y).")]
    [SerializeField] private Vector2 loopPitchRange = new Vector2(0.9f, 1.1f);
    [Tooltip("Seconds for the loop to swell up when you start moving, and to die away when you stop.")]
    [SerializeField] private float loopFadeIn = 0.5f;
    [SerializeField] private float loopFadeOut = 1.2f;

    [Header("Strokes")]
    [Tooltip("Played in rhythm while swimming. Short clips play whole; anything longer than Stroke Slice Seconds plays a random slice. Leave empty for none.")]
    [SerializeField] private AudioClip[] strokeClips;
    [SerializeField, Range(0f, 1f)] private float strokeVolume = 0.12f;
    [Tooltip("Seconds between strokes at top speed. Slower swimming spaces them out.")]
    [SerializeField] private float strokeInterval = 1.4f;
    [SerializeField] private Vector2 strokePitchRange = new Vector2(0.9f, 1.1f);
    [Tooltip("Speed (0..1 of top speed) you must be doing before strokes play at all.")]
    [SerializeField, Range(0f, 1f)] private float strokeMinSpeed = 0.2f;
    [Tooltip("A clip longer than this plays a random slice this many seconds long instead of the whole thing. 0 = always play whole clips.")]
    [SerializeField] private float strokeSliceSeconds = 1.2f;
    [Tooltip("Fade at each end of a slice, in seconds, so it never clicks or cuts.")]
    [SerializeField] private float strokeFade = 0.25f;

    private SwimController swim;
    private AudioSource loopSource;
    private AudioSource strokeSource;
    private float loopLevel;          // 0..1, smoothed
    private float strokeTimer;
    private bool wasStroking;
    private int lastStroke = -1;
    private bool slicing;
    private float sliceStart;
    private float sliceEnd;

    // Current loop level, 0..1, for anything that wants to react (a bubble particle burst, say).
    public float Level => loopLevel;

    private void Awake()
    {
        swim = GetComponent<SwimController>();

        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.clip = movementLoop;
        loopSource.loop = true;
        loopSource.playOnAwake = false;
        loopSource.spatialBlend = 0f;
        loopSource.volume = 0f;

        strokeSource = gameObject.AddComponent<AudioSource>();
        strokeSource.playOnAwake = false;
        strokeSource.spatialBlend = 0f;
    }

    private void Update()
    {
        // While the swim controller is switched off (admin panel open, dead) you are not moving, whatever it last thought.
        float speed = swim != null && swim.isActiveAndEnabled && !swim.Frozen ? swim.Speed01 : 0f;

        UpdateLoop(speed);
        UpdateStrokes(speed);
        UpdateSlice();
    }

    private void UpdateLoop(float speed)
    {
        float target = Mathf.Clamp01(loopVolumeBySpeed.Evaluate(speed));
        float seconds = target > loopLevel ? loopFadeIn : loopFadeOut;
        loopLevel = seconds > 0f ? Mathf.MoveTowards(loopLevel, target, Time.deltaTime / seconds) : target;

        if (movementLoop == null)
            return;

        loopSource.volume = loopLevel * loopMaxVolume;
        loopSource.pitch = Mathf.Lerp(loopPitchRange.x, loopPitchRange.y, speed);

        bool audible = loopLevel > 0.001f;
        if (audible && !loopSource.isPlaying)
            loopSource.Play();
        else if (!audible && loopSource.isPlaying)
            loopSource.Pause();
    }

    private void UpdateStrokes(float speed)
    {
        bool stroking = speed >= strokeMinSpeed && strokeClips != null && strokeClips.Length > 0;
        if (stroking && !wasStroking)
            strokeTimer = 0.15f;   // first stroke lands just after you set off
        wasStroking = stroking;
        if (!stroking)
            return;

        strokeTimer -= Time.deltaTime;
        if (strokeTimer > 0f)
            return;

        PlayStroke();
        strokeTimer = strokeInterval / Mathf.Clamp(speed, 0.35f, 1f);
    }

    private void PlayStroke()
    {
        int index = Random.Range(0, strokeClips.Length);
        if (strokeClips.Length > 1 && index == lastStroke)
            index = (index + 1) % strokeClips.Length;
        lastStroke = index;
        AudioClip clip = strokeClips[index];
        if (clip == null)
            return;

        strokeSource.pitch = Random.Range(strokePitchRange.x, strokePitchRange.y);

        if (strokeSliceSeconds > 0f && clip.length > strokeSliceSeconds + 0.1f)
        {
            // A random window of a long recording, faded in and out by UpdateSlice.
            strokeSource.Stop();
            strokeSource.clip = clip;
            strokeSource.time = Random.Range(0f, clip.length - strokeSliceSeconds);
            strokeSource.volume = 0f;
            strokeSource.Play();
            sliceStart = Time.time;
            sliceEnd = Time.time + strokeSliceSeconds;
            slicing = true;
            return;
        }

        if (slicing)
            EndSlice();
        strokeSource.PlayOneShot(clip, strokeVolume);
    }

    private void UpdateSlice()
    {
        if (!slicing)
            return;

        if (Time.time >= sliceEnd || !strokeSource.isPlaying)
        {
            EndSlice();
            return;
        }

        float fade = Mathf.Max(0.01f, strokeFade);
        float envelope = Mathf.Min((Time.time - sliceStart) / fade, (sliceEnd - Time.time) / fade);
        strokeSource.volume = Mathf.Clamp01(envelope) * strokeVolume;
    }

    private void EndSlice()
    {
        strokeSource.Stop();
        strokeSource.volume = 1f;
        slicing = false;
    }
}
