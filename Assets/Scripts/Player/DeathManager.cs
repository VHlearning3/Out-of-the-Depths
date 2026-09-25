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
    [Tooltip("Dress the death screen in the HUD style: it fades in over the whole HUD, deep sea blue instead of black, the title in a soft coral with a shadow, the countdown quieter under it.")]
    [SerializeField] private bool styledDeathScreen = true;

    [Header("Events")]
    public UnityEvent onDied = new UnityEvent();
    public UnityEvent onRespawned = new UnityEvent();

    public bool IsDead { get; private set; }

    private CharacterController characterController;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        if (styledDeathScreen)
            StyleDeathScreen();
    }

    // The HUD style on the death screen's placeholder look (a black backdrop, a red label): only colours and sizes.
    private void StyleDeathScreen()
    {
        if (deathScreen == null)
            return;
        Transform background = deathScreen.transform.Find("Background");
        var backdrop = background != null ? background.GetComponent<Image>() : null;
        if (backdrop != null && backdrop.color.r < 0.05f && backdrop.color.g < 0.05f && backdrop.color.b < 0.05f)
            backdrop.color = new Color(0.01f, 0.04f, 0.06f, 0.9f);
        foreach (Text text in deathScreen.GetComponentsInChildren<Text>(true))
        {
            if (text == countdownLabel)
                continue;
            text.color = new Color(1f, 0.45f, 0.4f);
            AddShadow(text, new Color(0f, 0f, 0f, 0.6f), new Vector2(2f, -3f));
        }
        if (countdownLabel != null)
        {
            countdownLabel.color = HudStyle.Muted;
            countdownLabel.fontSize = Mathf.Min(countdownLabel.fontSize, 24);
            AddShadow(countdownLabel, new Color(0f, 0f, 0f, 0.5f), new Vector2(1f, -1.5f));
        }
    }

    private static void AddShadow(Text text, Color color, Vector2 offset)
    {
        var shadow = text.GetComponent<Shadow>();
        if (shadow == null)
            shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = offset;
    }

    // The death screen sinks in rather than snapping on.
    private IEnumerator FadeIn()
    {
        var group = deathScreen.GetComponent<CanvasGroup>();
        if (group == null)
            group = deathScreen.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        for (float t = 0f; t < 0.8f && IsDead; t += Time.unscaledDeltaTime)
        {
            group.alpha = Ease.OutCubic(t / 0.8f);
            yield return null;
        }
        group.alpha = 1f;
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

        if (deathScreen != null)
        {
            deathScreen.transform.SetAsLastSibling();   // over the whole HUD, reticle and all
            deathScreen.SetActive(true);
            if (styledDeathScreen)
                StartCoroutine(FadeIn());
        }

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
            hungerSystem.ResetHunger();

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
