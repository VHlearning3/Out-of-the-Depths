using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private float maxWidth = 300f;

    private void Update()
    {
        float percent = healthSystem.CurrentHealth / healthSystem.MaxHealth;
        fillRect.sizeDelta = new Vector2(maxWidth * percent, fillRect.sizeDelta.y);
    }
}
