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

    [Header("UI")]
    [SerializeField] private StatBarUI healthBar;
    [SerializeField] private ScreenFlash damageFlash;

    [Header("Events")]
    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
    public UnityEvent onDeath = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public float HealthPercent01 => maxHealth <= 0f ? 0f : CurrentHealth / maxHealth;
    public bool IsDead => CurrentHealth <= 0f;

    private bool deathEventFired;
    private float nextStarveTickTime;
    private float lastDamageTime = float.NegativeInfinity;

    private void Awake()
    {
        CurrentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
    }

    private void Start()
    {
        onHealthChanged.Invoke(CurrentHealth, maxHealth);
        if (healthBar != null)
            healthBar.SetValue(CurrentHealth, maxHealth);
    }

    private void Update()
    {
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
        lastDamageTime = float.NegativeInfinity;
        SetHealth(maxHealth);
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

        if (CurrentHealth <= 0f && !deathEventFired)
        {
            deathEventFired = true;
            onDeath.Invoke();
        }
    }
}
