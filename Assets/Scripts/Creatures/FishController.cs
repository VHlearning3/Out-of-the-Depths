using System.Collections;
using UnityEngine;

// Alive: wanders, can be slashed. Dead: flips belly-up, drifts, and becomes edible.
// Respawn timing lives on the EdibleFish component (Respawn Time); this brings it back alive.
[RequireComponent(typeof(Damageable), typeof(EdibleFish))]
public class FishController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FishWander wander;
    [Tooltip("Extra behaviours (e.g. FishAggression) switched off while dead and back on when revived.")]
    [SerializeField] private Behaviour[] disabledWhileDead;
    [Tooltip("The mesh child. Swap this object for real fish art; nothing else needs to change.")]
    [SerializeField] private Transform visual;

    [Header("Death")]
    [SerializeField] private float deathFlipDuration = 0.5f;
    [SerializeField] private float deathFloatSpeed = 0.25f;
    [SerializeField] private float deathFloatDistance = 0.8f;

    public bool IsAlive { get; private set; } = true;

    private Damageable damageable;
    private EdibleFish edible;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Quaternion visualRestRotation;
    private float floated;

    private void Awake()
    {
        damageable = GetComponent<Damageable>();
        edible = GetComponent<EdibleFish>();
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        if (visual != null)
            visualRestRotation = visual.localRotation;

        edible.enabled = false;
    }

    private void OnEnable()
    {
        damageable.onDeath.AddListener(OnKilled);
        edible.onRespawned.AddListener(Revive);
    }

    private void OnDisable()
    {
        damageable.onDeath.RemoveListener(OnKilled);
        edible.onRespawned.RemoveListener(Revive);
    }

    private void Update()
    {
        if (IsAlive || floated >= deathFloatDistance)
            return;

        float step = deathFloatSpeed * Time.deltaTime;
        transform.position += Vector3.up * step;
        floated += step;
    }

    private void OnKilled()
    {
        IsAlive = false;
        floated = 0f;
        SetBehavioursEnabled(false);
        if (wander != null)
            wander.enabled = false;
        edible.enabled = true;
        StartCoroutine(FlipBellyUp());
    }

    private IEnumerator FlipBellyUp()
    {
        if (visual == null)
            yield break;

        Quaternion from = visual.localRotation;
        Quaternion to = visualRestRotation * Quaternion.Euler(0f, 0f, 180f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, deathFlipDuration);
            visual.localRotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
    }

    private void Revive()
    {
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (visual != null)
            visual.localRotation = visualRestRotation;

        damageable.ResetHealth();
        edible.enabled = false;
        if (wander != null)
        {
            wander.ResetHome(spawnPosition);
            wander.enabled = true;
        }

        SetBehavioursEnabled(true);
        IsAlive = true;
    }

    private void SetBehavioursEnabled(bool value)
    {
        if (disabledWhileDead == null)
            return;

        foreach (var behaviour in disabledWhileDead)
        {
            if (behaviour != null)
                behaviour.enabled = value;
        }
    }
}
