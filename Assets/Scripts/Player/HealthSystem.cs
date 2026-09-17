using UnityEngine;
using UnityEngine.Events;

// Player health pool: takes damage, regenerates when well fed, ticks starvation damage, fires onDeath.
public class HealthSystem : MonoBehaviour
{
    [System.Serializable]
    public class HealthChangedEvent : UnityEvent<float, float> { }

    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float startingHealth = 100f;

    [Header("Starvation")]
    [SerializeField] private HungerSystem hungerSystem;
    [SerializeField] private float starveDamagePerTick = 5f;
    [SerializeField] private float starveTickInterval = 1f;

    [Header("Regeneration")]
    [Tooltip("Health regained per second while well fed and not recently hurt.")]
    [SerializeField] private float regenPerSecond = 2f;
    [Tooltip("Hunger must be above this fraction for regen to run.")]
    [SerializeField, Range(0f, 1f)] private float regenHungerThreshold01 = 0.5f;
    [SerializeField] private float regenDelayAfterDamage = 3f;

    [Header("Warning")]
    [Tooltip("The warning runs while health is at or below this fraction of max (0.3 = 30%) and stops above it.")]
    [SerializeField, Range(0f, 1f)] private float warningThreshold01 = 0.3f;
    [SerializeField] private AudioClip warningSound;
    [SerializeField, Range(0f, 1f)] private float warningVolume = 0.5f;
    [Tooltip("Seconds between warning plays while low. 0 = loop the clip without gaps.")]
    [SerializeField] private float warningInterval = 3f;

    [Header("UI")]
    [SerializeField] private StatBarUI healthBar;
    [SerializeField] private ScreenFlash damageFlash;

    [Header("Events")]
    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
    public UnityEvent onHealthWarning = new UnityEvent();
    public UnityEvent onDeath = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public float HealthPercent01 => maxHealth <= 0f ? 0f : CurrentHealth / maxHealth;
    public bool IsLow => HealthPercent01 <= warningThreshold01;
    public bool IsDead => CurrentHealth <= 0f;

    private bool deathEventFired;
    private bool warningFired;
    private float nextStarveTickTime;
    private float nextWarningTime;
    private float lastDamageTime = float.NegativeInfinity;
    private AudioSource audioSource;

    private void Awake()
    {
        CurrentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void OnDisable()
    {
        if (audioSource != null)
            audioSource.Stop();
    }

    private void Start()
    {
        onHealthChanged.Invoke(CurrentHealth, maxHealth);
        if (healthBar != null)
            healthBar.SetValue(CurrentHealth, maxHealth);
    }

    private void Update()
    {
        UpdateWarningSound();

        if (IsDead)
            return;

        if (hungerSystem != null && hungerSystem.IsStarving)
        {
            if (Time.time >= nextStarveTickTime)
            {
                nextStarveTickTime = Time.time + starveTickInterval;
                TakeDamage(starveDamagePerTick);
            }
            return;
        }

        bool wellFed = hungerSystem == null || hungerSystem.HungerPercent01 >= regenHungerThreshold01;
        bool recentlyHurt = Time.time - lastDamageTime < regenDelayAfterDamage;
        if (wellFed && !recentlyHurt && CurrentHealth < maxHealth && regenPerSecond > 0f)
            SetHealth(CurrentHealth + regenPerSecond * Time.deltaTime);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        lastDamageTime = Time.time;
        SetHealth(CurrentHealth - amount);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        SetHealth(CurrentHealth + amount);
    }

    public void ResetHealth()
    {
        deathEventFired = false;
        warningFired = false;
        lastDamageTime = float.NegativeInfinity;
        SetHealth(maxHealth);
    }

    // Plays only while low and alive: looped when the interval is 0, otherwise repeated every warningInterval seconds. Stops as soon as health is above the threshold.
    private void UpdateWarningSound()
    {
        if (!IsLow || IsDead || warningSound == null)
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

    private void SetHealth(float value)
    {
        float previous = CurrentHealth;
        CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
        onHealthChanged.Invoke(CurrentHealth, maxHealth);

        if (healthBar != null)
            healthBar.SetValue(CurrentHealth, maxHealth);

        if (CurrentHealth < previous && damageFlash != null)
            damageFlash.Flash(Mathf.Clamp01((previous - CurrentHealth) / maxHealth * 4f));

        if (IsLow && !IsDead && !warningFired)
        {
            warningFired = true;
            onHealthWarning.Invoke();
        }
        else if (!IsLow)
        {
            warningFired = false;
        }

        if (CurrentHealth <= 0f && !deathEventFired)
        {
            deathEventFired = true;
            onDeath.Invoke();
        }
    }
}
