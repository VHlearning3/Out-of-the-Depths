using UnityEngine;

public class HungerBarUI : MonoBehaviour
{
    [SerializeField] private HungerSystem hungerSystem;
    [SerializeField] private RectTransform fillRect;
    [SerializeField] private float maxWidth = 300f;

    private void Update()
    {
        float percent = hungerSystem.CurrentHunger / hungerSystem.MaxHunger;
        fillRect.sizeDelta = new Vector2(maxWidth * percent, fillRect.sizeDelta.y);
    }
}
