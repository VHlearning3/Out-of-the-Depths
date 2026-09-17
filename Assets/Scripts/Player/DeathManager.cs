using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Death and respawn: freezes the player, shows the death screen and countdown, then teleports to the active checkpoint.
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
    [SerializeField] private Text countdownLabel;
    [SerializeField] private string countdownFormat = "Respawning in {0}...";

    [Header("Events")]
    public UnityEvent onDied = new UnityEvent();
    public UnityEvent onRespawned = new UnityEvent();

    public bool IsDead { get; private set; }

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

    public void SetRespawnPoint(Transform point)
    {
        respawnPoint = point;
    }

    private void HandleDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        SetGameplayEnabled(false);

        if (hungerSystem != null)
            hungerSystem.WarningMuted = true;

        if (deathScreen != null)
            deathScreen.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        onDied.Invoke();
        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        float remaining = respawnDelay;
        while (remaining > 0f)
        {
            if (countdownLabel != null)
                countdownLabel.text = string.Format(countdownFormat, Mathf.CeilToInt(remaining));

            yield return null;
            remaining -= Time.deltaTime;
        }

        if (countdownLabel != null)
            countdownLabel.text = string.Empty;

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
        {
            hungerSystem.WarningMuted = false;
            hungerSystem.ResetHunger();
        }

        if (deathScreen != null)
            deathScreen.SetActive(false);

        SetGameplayEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        IsDead = false;
        onRespawned.Invoke();
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
