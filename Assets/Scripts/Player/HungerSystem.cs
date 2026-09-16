using UnityEngine;
using UnityEngine.Events;

public class HungerSystem : MonoBehaviour
{
    [System.Serializable]
    public class HungerChangedEvent : UnityEvent<float, float> { }

    [Header("Hunger")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float startingHunger = 100f;
    [SerializeField] private float depletionRate = 1f;

    [Header("UI")]
    [SerializeField] private StatBarUI hungerBar;
    [SerializeField] private ScreenFlash foodFlash;

    [Header("Events")]
    public HungerChangedEvent onHungerChanged = new HungerChangedEvent();
    public UnityEvent onHungerDepleted = new UnityEvent();

    public float CurrentHunger { get; private set; }
    public float MaxHunger => maxHunger;
    public float HungerPercent01 => maxHunger <= 0f ? 0f : CurrentHunger / maxHunger;

    private bool depletedEventFired;

    private void Awake()
    {
        CurrentHunger = Mathf.Clamp(startingHunger, 0f, maxHunger);
    }

    private void Start()
    {
        onHungerChanged.Invoke(CurrentHunger, maxHunger);
        if (hungerBar != null)
            hungerBar.SetValue(CurrentHunger, maxHunger);
    }

    private void Update()
    {
        if (CurrentHunger <= 0f)
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
        SetHunger(maxHunger);
    }

    private void SetHunger(float value)
    {
        CurrentHunger = Mathf.Clamp(value, 0f, maxHunger);
        onHungerChanged.Invoke(CurrentHunger, maxHunger);

        if (hungerBar != null)
            hungerBar.SetValue(CurrentHunger, maxHunger);

        if (CurrentHunger <= 0f && !depletedEventFired)
        {
            depletedEventFired = true;
            onHungerDepleted.Invoke();
        }
    }
}
