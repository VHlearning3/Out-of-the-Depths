using UnityEngine;
using UnityEngine.Events;

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

    [Header("UI")]
    [SerializeField] private StatBarUI healthBar;
    [SerializeField] private ScreenFlash damageFlash;

    [Header("Events")]
    public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
    public UnityEvent onDeath = new UnityEvent();

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0f;

    private bool deathEventFired;
    private float nextStarveTickTime;

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
        if (IsDead || hungerSystem == null || hungerSystem.CurrentHunger > 0f)
            return;

        if (Time.time < nextStarveTickTime)
            return;

        nextStarveTickTime = Time.time + starveTickInterval;
        TakeDamage(starveDamagePerTick);
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        SetHealth(CurrentHealth - amount);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
            return;

        SetHealth(CurrentHealth + amount);
    }

    private void SetHealth(float value)
    {
        float previous = CurrentHealth;
        CurrentHealth = Mathf.Clamp(value, 0f, maxHealth);
        onHealthChanged.Invoke(CurrentHealth, maxHealth);

        if (healthBar != null)
            healthBar.SetValue(CurrentHealth, maxHealth);

        if (CurrentHealth < previous && damageFlash != null)
            damageFlash.Flash();

        if (CurrentHealth <= 0f && !deathEventFired)
        {
            deathEventFired = true;
            onDeath.Invoke();
        }
    }
}
