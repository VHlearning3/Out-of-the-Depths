using System.Collections;
using UnityEngine;

// Press E to eat: refills hunger, plays the eat sound, hides the fish and optionally respawns it.
[RequireComponent(typeof(Collider))]
public class EdibleFish : MonoBehaviour, IInteractable
{
    [Header("Hunger")]
    [SerializeField] private float hungerRestoreAmount = 25f;
    [SerializeField] private string prompt = "eat";

    [Header("Respawn")]
    [Tooltip("0 = eaten for good. Otherwise the fish reappears after this many seconds.")]
    [SerializeField] private float respawnTime = 0f;

    [Header("Feedback")]
    [SerializeField] private AudioClip eatSound;
    [SerializeField, Range(0f, 1f)] private float eatVolume = 0.6f;
    [SerializeField] private GameObject consumedVfx;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onEaten = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent onRespawned = new UnityEngine.Events.UnityEvent();

    private Collider fishCollider;
    private Renderer[] renderers;
    private bool consumed;
    // Eaten or faded away and not back yet: out of the world (hidden, no collider).
    public bool Consumed => consumed;

    public string Prompt => prompt;

    private void Awake()
    {
        fishCollider = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        if (GetComponent<FishController>() == null)
            FishColors.Paint(gameObject, FishColors.Food);   // testing colour: a dead fish lying about to eat
    }

    public void Interact(GameObject interactor)
    {
        if (consumed)
            return;

        var hunger = interactor.GetComponentInParent<HungerSystem>();
        if (hunger == null)
            return;

        consumed = true;
        hunger.Eat(hungerRestoreAmount);

        if (eatSound != null)
            SoundVariety.PlayAt(eatSound, transform.position, eatVolume);

        if (consumedVfx != null)
            Instantiate(consumedVfx, transform.position, transform.rotation);

        onEaten.Invoke();

        if (respawnTime > 0f)
            StartCoroutine(RespawnAfterDelay());
        else
            gameObject.SetActive(false);
    }

    // Take the fish out of the world without anyone eating it (it drifted into a barrier). Respawns just like an eaten one.
    public void Despawn()
    {
        if (consumed)
            return;

        consumed = true;
        if (respawnTime > 0f)
            StartCoroutine(RespawnAfterDelay());
        else
            gameObject.SetActive(false);
    }

    private IEnumerator RespawnAfterDelay()
    {
        SetVisible(false);
        yield return new WaitForSeconds(respawnTime);
        SetVisible(true);
        consumed = false;
        onRespawned.Invoke();
    }

    private void SetVisible(bool visible)
    {
        fishCollider.enabled = visible;
        foreach (var r in renderers)
            r.enabled = visible;
    }
}
