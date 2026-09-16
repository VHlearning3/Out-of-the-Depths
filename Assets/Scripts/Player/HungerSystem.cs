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
        if (IsStarving)
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
            if (warningSound != null)
                audioSource.PlayOneShot(warningSound, warningVolume);
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
