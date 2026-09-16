using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DamageFlashUI : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private Image flashImage;
    [SerializeField] private float flashAlpha = 0.35f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Coroutine flashRoutine;

    private void OnEnable()
    {
        healthSystem.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        healthSystem.OnDamaged -= HandleDamaged;
    }

    private void HandleDamaged(float amount)
    {
        if (flashRoutine != null)
            StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        Color color = flashImage.color;
        color.a = flashAlpha;
        flashImage.color = color;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(flashAlpha, 0f, elapsed / fadeDuration);
            flashImage.color = color;
            yield return null;
        }
    }
}
