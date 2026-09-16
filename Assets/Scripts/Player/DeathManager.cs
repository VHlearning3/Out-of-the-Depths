using UnityEngine;

public class DeathManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private Behaviour[] componentsToDisableOnDeath;
    [SerializeField] private GameObject deathScreen;

    private void OnEnable()
    {
        if (healthSystem != null)
            healthSystem.onDeath.AddListener(HandleDeath);
    }

    private void OnDisable()
    {
        if (healthSystem != null)
            healthSystem.onDeath.RemoveListener(HandleDeath);
    }

    private void HandleDeath()
    {
        foreach (var component in componentsToDisableOnDeath)
        {
            if (component != null)
                component.enabled = false;
        }

        if (deathScreen != null)
            deathScreen.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
