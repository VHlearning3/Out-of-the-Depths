using UnityEngine;

// Creepy deep-sea ambience, Minecraft-cave style: a quiet looping bed plus one-shot "stingers" (groans, creaks, clanks)
// that play at random intervals from a random spot around the player, muffled and echoing like something far off in
// the dark. Drop any clips into Stingers to change the mood; everything else is Inspector numbers.
public class OceanAmbience : MonoBehaviour
{
    [Header("Bed")]
    [Tooltip("Looping background sound. Plays as 2D, always around you. Keep it low: it should sit under everything, not on top.")]
    [SerializeField] private AudioClip bedLoop;
    [SerializeField, Range(0f, 1f)] private float bedVolume = 0.12f;
    [Tooltip("Seconds to fade the bed in at start.")]
    [SerializeField] private float bedFadeIn = 4f;

    [Header("Stingers")]
    [Tooltip("Random one-shots. Add more clips for more variety.")]
    [SerializeField] private AudioClip[] stingers;
    [SerializeField, Range(0f, 1f)] private float stingerVolume = 0.3f;
    [Tooltip("Seconds between stingers, chosen at random in this range.")]
    [SerializeField] private float minInterval = 30f;
    [SerializeField] private float maxInterval = 80f;
    [SerializeField] private float firstDelay = 20f;
    [Tooltip("How far away a stinger sounds from, in metres.")]
    [SerializeField] private float minDistance = 10f;
    [SerializeField] private float maxDistance = 30f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.8f, 1.1f);

    [Header("Muffling")]
    [Tooltip("Low-pass cutoff for stingers, in Hz. Lower = more muffled and far away.")]
    [SerializeField] private float lowPassCutoff = 1000f;
    [SerializeField] private AudioReverbPreset reverb = AudioReverbPreset.Cave;

    // How far the bed drops away, 0..1 (PlayerPanic turns the ocean down while you are panicking).
    public float Duck { get; set; }
    // 1 = the ocean falls silent, fast, and no stingers play (the chase reveal's hush). 0 = normal.
    public float Hush { get; set; }

    private AudioSource bed;
    private AudioSource stinger;
    private float nextStingerTime;
    private int lastClip = -1;

    private void Awake()
    {
        bed = gameObject.AddComponent<AudioSource>();
        bed.clip = bedLoop;
        bed.loop = true;
        bed.spatialBlend = 0f;
        bed.volume = 0f;
        bed.playOnAwake = false;

        var emitter = new GameObject("Stinger");
        emitter.transform.SetParent(transform, false);
        stinger = emitter.AddComponent<AudioSource>();
        stinger.playOnAwake = false;
        stinger.spatialBlend = 1f;
        stinger.rolloffMode = AudioRolloffMode.Linear;
        stinger.minDistance = 4f;
        stinger.maxDistance = maxDistance + 30f;
        stinger.dopplerLevel = 0f;
        var lowPass = emitter.AddComponent<AudioLowPassFilter>();
        lowPass.cutoffFrequency = lowPassCutoff;
        var echo = emitter.AddComponent<AudioReverbFilter>();
        echo.reverbPreset = reverb;

        nextStingerTime = Time.time + firstDelay;
    }

    private void Start()
    {
        if (bedLoop != null)
            bed.Play();
    }

    private void Update()
    {
        if (bedLoop != null)
        {
            float target = bedVolume * (1f - Mathf.Clamp01(Duck)) * (1f - Mathf.Clamp01(Hush));
            float rate = (bedFadeIn > 0f ? bedVolume / bedFadeIn : 1000f) * (Hush > 0f ? 4f : 1f);
            bed.volume = Mathf.MoveTowards(bed.volume, target, rate * Time.unscaledDeltaTime);
        }

        if (Hush > 0.5f || stingers == null || stingers.Length == 0 || Time.time < nextStingerTime)
            return;

        PlayStinger();
        nextStingerTime = Time.time + Random.Range(minInterval, maxInterval);
    }

    // Also handy to wire to events (a door opening, entering a room) for a scripted scare.
    public void PlayStinger()
    {
        if (stingers == null || stingers.Length == 0)
            return;

        int index = Random.Range(0, stingers.Length);
        if (stingers.Length > 1 && index == lastClip)
            index = (index + 1) % stingers.Length;
        lastClip = index;
        AudioClip clip = stingers[index];
        if (clip == null)
            return;

        Transform listener = Camera.main != null ? Camera.main.transform : transform;
        Vector3 direction = Random.onUnitSphere;
        direction.y = Mathf.Clamp(direction.y, -0.6f, 0.3f);   // mostly around and slightly below, like it comes from the deep
        stinger.transform.position = listener.position + direction.normalized * Random.Range(minDistance, maxDistance);
        stinger.pitch = Random.Range(pitchRange.x, pitchRange.y);
        stinger.PlayOneShot(clip, stingerVolume);
    }
}
