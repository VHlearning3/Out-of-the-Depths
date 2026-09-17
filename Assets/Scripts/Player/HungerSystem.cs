using UnityEngine;
using UnityEngine.Events;

// Hunger meter that drains over time. Eating fish refills it; hitting zero lets HealthSystem start starvation damage.
public class HungerSystem : MonoBehaviour
{
    [System.Serializable]
    public class HungerChangedEvent : UnityEvent<float, float> { }

    [Header("Hunger")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float startingHunger = 100f;
    [SerializeField] private float depletionRate = 1f;

    [Header("Warning")]
    [Tooltip("The warning runs while hunger is at or below this fraction of max (0.3 = 30%) and stops above it.")]
    [SerializeField, Range(0f, 1f)] private float warningThreshold01 = 0.3f;
    [SerializeField] private AudioClip warningSound;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.5f;
    [Tooltip("Seconds between warning plays while low. 0 = loop the clip without gaps.")]
    [SerializeField] private float warningInterval = 3f;

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
    public bool WarningMuted { get; set; }

    private bool depletedEventFired;
    private bool warningFired;
    private float nextWarningTime;
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
        UpdateWarningSound();

        if (IsStarving)
            return;

        SetHunger(CurrentHunger - depletionRate * Time.deltaTime);
    }

    private void OnDisable()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    // Plays only while low: looped when the interval is 0, otherwise repeated every warningInterval seconds. Stops as soon as hunger is above the threshold.
    private void UpdateWarningSound()
    {
        if (!IsLow || WarningMuted || warningSound == null)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();
            nextWarningTime = 0f;
            return;
        }

        if (warningInterval <= 0f)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = warningSound;
                audioSource.loop = true;
                audioSource.volume = warningVolume;
                audioSource.Play();
            }
            return;
        }

        if (Time.time >= nextWarningTime)
        {
            nextWarningTime = Time.time + warningInterval;
            audioSource.PlayOneShot(warningSound, warningVolume);
        }
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

    public void ResetHunger()
    {
        depletedEventFired = false;
        warningFired = false;
        SetHunger(maxHunger);
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
