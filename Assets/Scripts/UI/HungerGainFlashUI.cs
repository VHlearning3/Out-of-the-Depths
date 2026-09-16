using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HungerGainFlashUI : MonoBehaviour
{
    [SerializeField] private HungerSystem hungerSystem;
    [SerializeField] private Image fillImage;
    [SerializeField] private Color flashColor = Color.white;
    [SerializeField] private float flashDuration = 0.25f;

    private Color normalColor;
    private Coroutine flashRoutine;

    private void Awake()
    {
        normalColor = fillImage.color;
    }

    private void OnEnable()
    {
        hungerSystem.OnFed += HandleFed;
    }

    private void OnDisable()
    {
        hungerSystem.OnFed -= HandleFed;
    }

    private void HandleFed(float amount)
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        fillImage.color = flashColor;
        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            fillImage.color = Color.Lerp(flashColor, normalColor, elapsed / flashDuration);
            yield return null;
        }
        fillImage.color = normalColor;
    }
}
