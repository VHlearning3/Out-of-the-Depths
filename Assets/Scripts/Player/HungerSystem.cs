using UnityEngine;

public class HungerSystem : MonoBehaviour
{
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float depletionPerSecond = 1f;

    public float CurrentHunger { get; private set; }
    public float MaxHunger => maxHunger;

    public event System.Action<float> OnFed;

    private void Awake()
    {
        CurrentHunger = maxHunger;
    }

    private void Update()
    {
        CurrentHunger = Mathf.Max(0f, CurrentHunger - depletionPerSecond * Time.deltaTime);
    }

    public void Feed(float amount)
    {
        CurrentHunger = Mathf.Min(maxHunger, CurrentHunger + amount);
        OnFed?.Invoke(amount);
    }
}
