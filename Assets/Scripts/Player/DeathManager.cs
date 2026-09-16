using System.Collections;
using UnityEngine;

public class DeathManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HealthSystem healthSystem;
    [SerializeField] private HungerSystem hungerSystem;
    [SerializeField] private Behaviour[] componentsToDisableOnDeath;
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private Transform respawnPoint;

    [Header("Respawn")]
    [SerializeField] private float respawnDelay = 3f;

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

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
        SetGameplayEnabled(false);

        if (deathScreen != null)
            deathScreen.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(respawnDelay);
        Respawn();
    }

    private void Respawn()
    {
        if (respawnPoint != null && characterController != null)
        {
            characterController.enabled = false;
            transform.SetPositionAndRotation(respawnPoint.position, respawnPoint.rotation);
            characterController.enabled = true;
        }

        if (healthSystem != null)
            healthSystem.ResetHealth();

        if (hungerSystem != null)
            hungerSystem.ResetHunger();

        if (deathScreen != null)
            deathScreen.SetActive(false);

        SetGameplayEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetGameplayEnabled(bool value)
    {
        foreach (var component in componentsToDisableOnDeath)
        {
            if (component != null)
                component.enabled = value;
        }
    }
}
