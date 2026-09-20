using UnityEngine;

// How panicked the player is, 0..1, and everything that shows it. Panic rises fast (a chase, the pack closing in, a
// bite, low health) and ebbs slowly, so you are still shaking for a while after the danger has passed. It drives:
// panicked breathing, an audible heartbeat that quickens (the HUD vignette beats in time with it), heavier camera sway
// and a tremble, a small adrenaline speed boost, and the ocean ambience dropping away under it. Lives on the Player.
[RequireComponent(typeof(SwimController))]
public class PlayerPanic : MonoBehaviour
{
    public static PlayerPanic Instance { get; private set; }
    // Current panic 0..1, for anything else that wants to react. 0 when there is no PlayerPanic in the scene.
    public static float Level => Instance != null ? Instance.Panic01 : 0f;

    [Header("What causes panic")]
    [Tooltip("Panic the moment a chase is running, before the pack is even close...")]
    [SerializeField, Range(0f, 1f)] private float chaseBase = 0.3f;
    [Tooltip("...rising to this as the nearest hunter closes in (the Chase Sequence's danger).")]
    [SerializeField, Range(0f, 1f)] private float chaseMax = 1f;
    [Tooltip("Panic jumps to at least this when you take damage.")]
    [SerializeField, Range(0f, 1f)] private float damageSpike = 0.6f;
    [Tooltip("Steady panic while health is below the Health System's warning level.")]
    [SerializeField, Range(0f, 1f)] private float lowHealth = 0.45f;
    [Tooltip("Seconds to rise to a new, higher level...")]
    [SerializeField] private float riseSeconds = 1.5f;
    [Tooltip("...and to calm all the way back down once the danger has passed.")]
    [SerializeField] private float calmSeconds = 10f;

    [Header("Breathing")]
    [Tooltip("Looping breathing (bubbles through a regulator). Silent when calm, faster and louder as panic rises.")]
    [SerializeField] private AudioClip breathingLoop;
    [SerializeField, Range(0f, 1f)] private float breathingVolume = 0.35f;
    [Tooltip("Breathing pitch when barely panicked (x) and in full panic (y). Higher = faster, shallower breaths.")]
    [SerializeField] private Vector2 breathingPitch = new Vector2(0.95f, 1.35f);
    [Tooltip("Panic below this is not heard as breathing.")]
    [SerializeField, Range(0f, 1f)] private float breathingFrom = 0.12f;

    [Header("Heartbeat")]
    [Tooltip("A single heartbeat (one lub-dub). Repeats faster the more panicked you are.")]
    [SerializeField] private AudioClip heartbeat;
    [SerializeField, Range(0f, 1f)] private float heartbeatVolume = 0.4f;
    [Tooltip("Beats per minute when calm (x) and in full panic (y).")]
    [SerializeField] private Vector2 heartRate = new Vector2(70f, 150f);
    [Tooltip("Panic below this is not heard as a heartbeat.")]
    [SerializeField, Range(0f, 1f)] private float heartbeatFrom = 0.2f;

    [Header("Body")]
    [Tooltip("How much heavier the camera sway and bob get in full panic (1 = double).")]
    [SerializeField] private float swayBoost = 1.5f;
    [Tooltip("Camera tremble in full panic. 0 = none.")]
    [SerializeField, Range(0f, 1f)] private float tremble = 0.3f;
    [Tooltip("Adrenaline: extra swim speed in full panic (0.1 = 10% faster).")]
    [SerializeField, Range(0f, 0.5f)] private float adrenaline = 0.1f;
    [Tooltip("How far the ocean ambience drops away in full panic, so all you hear is yourself.")]
    [SerializeField, Range(0f, 1f)] private float ambienceDuck = 0.6f;

    public float Panic01 { get; private set; }
    // 0..1 through the current heartbeat, for anything that pulses with it (the HUD vignette).
    public float HeartPhase { get; private set; }

    private SwimController swim;
    private HealthSystem health;
    private DamageManager damage;
    private DeathManager death;
    private OceanAmbience ambience;
    private AudioSource breath;
    private AudioSource heart;

    private void Awake()
    {
        swim = GetComponent<SwimController>();
        health = GetComponent<HealthSystem>();
        damage = GetComponent<DamageManager>();
        death = GetComponent<DeathManager>();

        breath = gameObject.AddComponent<AudioSource>();
        breath.clip = breathingLoop;
        breath.loop = true;
        breath.playOnAwake = false;
        breath.spatialBlend = 0f;
        breath.volume = 0f;

        heart = gameObject.AddComponent<AudioSource>();
        heart.playOnAwake = false;
        heart.spatialBlend = 0f;
    }

    private void OnEnable()
    {
        Instance = this;
        if (damage != null)
            damage.onDamageTaken.AddListener(OnDamage);
        if (death != null)
            death.onRespawned.AddListener(ResetPanic);
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
        if (damage != null)
            damage.onDamageTaken.RemoveListener(OnDamage);
        if (death != null)
            death.onRespawned.RemoveListener(ResetPanic);
        Panic01 = 0f;
        ApplyBody();
        if (breath != null && breath.isPlaying)
            breath.Stop();
    }

    private void Start()
    {
        ambience = FindFirstObjectByType<OceanAmbience>();
    }

    private void OnDamage(float amount)
    {
        Panic01 = Mathf.Max(Panic01, damageSpike);
    }

    // Back to calm at once (a respawn).
    public void ResetPanic()
    {
        Panic01 = 0f;
    }

    private void Update()
    {
        float target = 0f;
        ChaseSequence chase = ChaseSequence.Active;
        if (chase != null && chase.IsRunning)
            target = Mathf.Lerp(chaseBase, chaseMax, chase.Danger01);
        if (health != null && health.IsLow && !health.IsDead)
            target = Mathf.Max(target, lowHealth);

        float seconds = target > Panic01 ? riseSeconds : calmSeconds;
        Panic01 = seconds > 0f ? Mathf.MoveTowards(Panic01, target, Time.deltaTime / seconds) : target;

        UpdateHeart();
        UpdateBreathing();
        ApplyBody();
    }

    private void UpdateHeart()
    {
        float bpm = Mathf.Lerp(heartRate.x, heartRate.y, Panic01);
        HeartPhase += Time.deltaTime * bpm / 60f;
        if (HeartPhase < 1f)
            return;
        HeartPhase -= Mathf.Floor(HeartPhase);

        if (heartbeat == null || Panic01 < heartbeatFrom)
            return;
        float level = Mathf.InverseLerp(heartbeatFrom, 1f, Panic01);
        heart.pitch = Random.Range(0.96f, 1.04f) * Mathf.Lerp(0.95f, 1.15f, Panic01);
        heart.PlayOneShot(heartbeat, heartbeatVolume * Mathf.Lerp(0.3f, 1f, level));
    }

    private void UpdateBreathing()
    {
        if (breathingLoop == null)
            return;
        float level = Mathf.InverseLerp(breathingFrom, 1f, Panic01);
        breath.volume = Mathf.MoveTowards(breath.volume, level * breathingVolume, Time.deltaTime * 0.5f);
        breath.pitch = Mathf.Lerp(breathingPitch.x, breathingPitch.y, Panic01);
        if (breath.volume > 0.001f && !breath.isPlaying)
            breath.Play();
        else if (breath.volume <= 0.001f && breath.isPlaying)
            breath.Pause();
    }

    private void ApplyBody()
    {
        if (swim != null)
        {
            swim.PanicSway = swayBoost * Panic01;
            swim.SpeedMultiplier = 1f + adrenaline * Panic01;
            if (tremble > 0f && Panic01 > 0f)
                swim.AddShake(tremble * Panic01 * Panic01);
        }
        if (ambience != null)
            ambience.Duck = ambienceDuck * Panic01;
    }
}
