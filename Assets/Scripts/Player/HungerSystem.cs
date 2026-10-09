using UnityEngine;
using UnityEngine.Events;

// Hunger meter. Only the dash uses it up (Swim Controller -> Dash Hunger Cost): it no longer drains by itself as time
// goes by (Passive Drain, off). Eating fish refills it; hitting zero lets HealthSystem start starvation damage.
public class HungerSystem : MonoBehaviour
{
    [System.Serializable]
    public class HungerChangedEvent : UnityEvent<float, float> { }

    [Header("Hunger")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float startingHunger = 100f;
    [Tooltip("Off: hunger only goes down when you dash. On: it also drains by itself, Depletion Rate a second.")]
    [SerializeField] private bool passiveDrain = false;
    [Tooltip("With Passive Drain on: hunger lost per second. 0.4 = a full fish empties in about four minutes.")]
    [SerializeField] private float depletionRate = 0.4f;

    [Header("Warning")]
    [Tooltip("Plays the warning once when hunger drops to this fraction of max (0.25 = 25%). Re-arms once hunger is back above it.")]
    [SerializeField, Range(0f, 1f)] private float warningThreshold01 = 0.25f;
    [SerializeField] private AudioClip warningSound;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.5f;

    [Header("UI")]
    [SerializeField] private StatBarUI hungerBar;
    [SerializeField] private ScreenFlash foodFlash;

    [Header("Events")]
    public HungerChangedEvent onHungerChanged = new HungerChangedEvent();
    public UnityEvent onHungerWarning = new UnityEvent();
    public UnityEvent onHungerDepleted = new UnityEvent();

    public float CurrentHunger { get; private set; }
    public float MaxHunger => maxHunger;
    public float HungerPercent01 => maxHunger <= 0f ? 0f : CurrentHunger / maxHunger;
    public bool IsLow => HungerPercent01 <= warningThreshold01;
    public bool IsStarving => CurrentHunger <= 0f;

    // Admin panel: hunger stops going down while this is on, and actions that cost hunger (the dash) are free.
    public bool DrainPaused { get; set; }

    private bool depletedEventFired;
    private bool warningFired;
    private AudioSource audioSource;

    private void Awake()
    {
        CurrentHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        onHungerChanged.Invoke(CurrentHunger, maxHunger);
        if (hungerBar != null)
            hungerBar.SetValue(CurrentHunger, maxHunger);
    }

    private void Update()
    {
        if (!passiveDrain || IsStarving || DrainPaused)
            return;

        SetHunger(CurrentHunger - depletionRate * Time.deltaTime);
    }

    public void Eat(float amount)
    {
        if (amount <= 0f)
            return;

        depletedEventFired = false;
        SetHunger(CurrentHunger + amount);

        if (foodFlash != null)
            foodFlash.Flash();
    }

    // Pay for an action (the dash): false, and nothing taken, when there is not that much left. An action never
    // empties the meter: with only its cost left (or less) it is refused, so dashing can never start you starving;
    // the last few points stay until you eat.
    public bool Spend(float amount)
    {
        if (amount <= 0f || DrainPaused)
            return true;   // free while the admin panel has hunger switched off
        if (CurrentHunger - amount <= 0.001f)
            return false;
        SetHunger(CurrentHunger - amount);
        return true;
    }

    public void ResetHunger()
    {
        depletedEventFired = false;
        warningFired = false;
        SetHunger(maxHunger);
    }

    // Straight to empty (admin panel): starvation damage starts on the next tick.
    public void Starve()
    {
        depletedEventFired = false;
        SetHunger(0f);
    }

    private void SetHunger(float value)
    {
        CurrentHunger = Mathf.Clamp(value, 0f, maxHunger);
        onHungerChanged.Invoke(CurrentHunger, maxHunger);

        if (hungerBar != null)
            hungerBar.SetValue(CurrentHunger, maxHunger);

        if (IsLow && !warningFired)
        {
            warningFired = true;
            onHungerWarning.Invoke();
            if (warningSound != null)
                SoundVariety.OneShot(audioSource, warningSound, warningVolume);
        }
        else if (!IsLow)
        {
            warningFired = false;
        }

        if (IsStarving && !depletedEventFired)
        {
            depletedEventFired = true;
            onHungerDepleted.Invoke();
        }
    }
}
