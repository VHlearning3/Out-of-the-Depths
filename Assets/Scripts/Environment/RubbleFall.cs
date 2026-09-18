using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Rubble that comes down on Drop(): the rocks under this object are placed where they should LAND (design the pile in the
// Scene view); at start they are lifted out of sight and hidden, and on Drop() they fall one after another, tumbling, with
// a thud and a camera shake, and the Blocker collider seals the gaps between them. Wire Drop() to whatever should cause
// it (the trident pickup's On Picked Up, a Player Area Trigger...) and On Dropped to e.g. Chase Sequence → End.
public class RubbleFall : MonoBehaviour
{
    [Header("Rocks")]
    [Tooltip("Empty = every child except the Blocker.")]
    [SerializeField] private Transform[] rocks;
    [Tooltip("How far above their landing spots the rocks wait.")]
    [SerializeField] private float dropHeight = 6f;
    [SerializeField] private float fallDuration = 0.55f;
    [Tooltip("Pause between rocks starting to fall.")]
    [SerializeField] private float stagger = 0.09f;
    [Tooltip("Random extra pause per rock so they don't fall in a neat row.")]
    [SerializeField] private float scatter = 0.15f;
    [Tooltip("Enabled when the first rock lands; sized over the whole pile it seals the gaps between rocks.")]
    [SerializeField] private Collider blocker;

    [Header("Drop when (drag-in shortcuts; Drop() works from any event too)")]
    [Tooltip("Drop() when this is picked up (the trident).")]
    [SerializeField] private PickupItem dropOnPickup;
    [Tooltip("Drop() when the player enters this trigger.")]
    [SerializeField] private PlayerAreaTrigger dropOnTrigger;

    [Header("Feedback")]
    [SerializeField] private AudioClip rumbleSound;
    [SerializeField] private AudioClip thudSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.9f;
    [Tooltip("Camera shake per landing when the player is right next to it, fading out to Shake Distance.")]
    [SerializeField] private float shake = 0.7f;
    [SerializeField] private float shakeDistance = 25f;

    [Header("Events")]
    public UnityEvent onDropped = new UnityEvent();

    public bool Dropped { get; private set; }

    private Vector3[] landedPositions;
    private Quaternion[] landedRotations;
    private SwimController player;

    private void Awake()
    {
        if (rocks == null || rocks.Length == 0)
        {
            var list = new List<Transform>();
            foreach (Transform child in transform)
                if (blocker == null || child != blocker.transform)
                    list.Add(child);
            rocks = list.ToArray();
        }

        landedPositions = new Vector3[rocks.Length];
        landedRotations = new Quaternion[rocks.Length];
        for (int i = 0; i < rocks.Length; i++)
        {
            if (rocks[i] == null)
                continue;
            landedPositions[i] = rocks[i].position;
            landedRotations[i] = rocks[i].rotation;
            rocks[i].position += Vector3.up * dropHeight;
            rocks[i].gameObject.SetActive(false);
        }

        if (blocker != null)
            blocker.enabled = false;
        player = FindFirstObjectByType<SwimController>();
    }

    private void Start()
    {
        if (dropOnPickup != null)
            dropOnPickup.onPickedUp.AddListener(Drop);
        if (dropOnTrigger != null)
            dropOnTrigger.onPlayerEnter.AddListener(Drop);
    }

    private void OnDestroy()
    {
        if (dropOnPickup != null)
            dropOnPickup.onPickedUp.RemoveListener(Drop);
        if (dropOnTrigger != null)
            dropOnTrigger.onPlayerEnter.RemoveListener(Drop);
    }

    [ContextMenu("Drop")]
    public void Drop()
    {
        if (Dropped)
            return;
        Dropped = true;
        StartCoroutine(DropAll());
    }

    private IEnumerator DropAll()
    {
        Play(rumbleSound, transform.position);
        Shake(transform.position, 0.5f);
        for (int i = 0; i < rocks.Length; i++)
        {
            if (rocks[i] != null)
                StartCoroutine(Fall(i));
            yield return new WaitForSeconds(stagger + Random.value * scatter);
        }
        yield return new WaitForSeconds(fallDuration * 1.2f + 0.3f);
        onDropped.Invoke();
    }

    // Accelerating fall with a tumble that unwinds into the landed pose, then a squash on impact.
    private IEnumerator Fall(int i)
    {
        Transform rock = rocks[i];
        rock.gameObject.SetActive(true);
        Vector3 from = landedPositions[i] + Vector3.up * dropHeight;
        Vector3 axis = Random.onUnitSphere;
        float tumble = Random.Range(60f, 200f) * (Random.value < 0.5f ? -1f : 1f);
        float duration = fallDuration * Random.Range(0.9f, 1.15f);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float k = Ease.InQuad(t / duration);
            rock.SetPositionAndRotation(Vector3.Lerp(from, landedPositions[i], k), landedRotations[i] * Quaternion.AngleAxis(tumble * (1f - k), axis));
            yield return null;
        }
        rock.SetPositionAndRotation(landedPositions[i], landedRotations[i]);
        Land(rock);

        Vector3 scale = rock.localScale;
        const float squashTime = 0.12f;
        for (float t = 0f; t < squashTime; t += Time.deltaTime)
        {
            float s = 1f - Mathf.Sin(t / squashTime * Mathf.PI) * 0.12f;
            rock.localScale = new Vector3(scale.x * (2f - s), scale.y * s, scale.z * (2f - s));
            yield return null;
        }
        rock.localScale = scale;
    }

    private void Land(Transform rock)
    {
        if (blocker != null)
            blocker.enabled = true;
        Play(thudSound, rock.position);
        Shake(rock.position, 1f);
    }

    private void Shake(Vector3 at, float scale)
    {
        if (player == null)
            return;
        float distance = Vector3.Distance(player.transform.position, at);
        if (distance < shakeDistance)
            player.AddShake(shake * scale * (1f - distance / shakeDistance));
    }

    // Scene view: each rock's drop, from where it waits down to where it lands.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        foreach (Transform child in transform)
        {
            if (blocker != null && child == blocker.transform)
                continue;
            Vector3 landed = child.position;
            Gizmos.DrawLine(landed + Vector3.up * dropHeight, landed);
        }
        if (dropOnPickup != null)
            Gizmos.DrawLine(transform.position, dropOnPickup.transform.position);
        if (dropOnTrigger != null)
            Gizmos.DrawLine(transform.position, dropOnTrigger.transform.position);
    }

    private void Play(AudioClip clip, Vector3 at)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, at, volume);
    }
}
