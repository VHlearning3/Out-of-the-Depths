using UnityEngine;

public class DeathManager : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private SwimController swimController;
    [SerializeField] private PlayerInteractor playerInteractor;

    private void OnEnable()
    {
        healthSystem.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        healthSystem.OnDeath -= HandleDeath;
    }

    private void HandleDeath()
    {
        if (deathScreen != null)
            deathScreen.SetActive(true);

        if (swimController != null)
            swimController.enabled = false;

        if (playerInteractor != null)
            playerInteractor.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
