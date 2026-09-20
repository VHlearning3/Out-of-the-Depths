using UnityEngine;
using UnityEngine.Events;

// Press E on this and it makes a sound (the mascot photo barks). Any object with a collider: set the prompt, drop in
// one or more clips (a random one plays each time, at a random pitch), and optionally let it wiggle. On Interacted
// fires too, for anything else you want to happen (a light, a door, an achievement).
public class SoundOnInteract : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "pet the dog";
    [Tooltip("One of these plays each time, never the same one twice in a row.")]
    [SerializeField] private AudioClip[] clips;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [Tooltip("Seconds before it can be used again.")]
    [SerializeField] private float cooldown = 0.6f;
    [Tooltip("Metres beyond which it can't be heard.")]
    [SerializeField] private float hearingRange = 25f;

    [Header("Wiggle")]
    [Tooltip("Gives the object a little shake when used, like the picture rattling on its nail.")]
    [SerializeField] private bool wiggle = true;
    [SerializeField] private float wiggleDegrees = 4f;
    [SerializeField] private float wiggleSeconds = 0.5f;

    [Header("Events")]
    public UnityEvent onInteracted = new UnityEvent();

    public string Prompt => prompt;

    private float readyAt;
    private int lastClip = -1;
    private Quaternion restRotation;
    private float wiggleUntil = -1f;

    private void Awake()
    {
        restRotation = transform.localRotation;
    }

    public void Interact(GameObject interactor)
    {
        if (Time.time < readyAt)
            return;
        readyAt = Time.time + cooldown;

        if (clips != null && clips.Length > 0)
        {
            int index = Random.Range(0, clips.Length);
            if (clips.Length > 1 && index == lastClip)
                index = (index + 1) % clips.Length;
            lastClip = index;
            SlicedOneShot.Play(clips[index], transform.position, volume, Random.Range(pitchRange.x, pitchRange.y), 0f, 0.05f, 1f, hearingRange);
        }

        if (wiggle)
        {
            restRotation = wiggleUntil > Time.time ? restRotation : transform.localRotation;
            wiggleUntil = Time.time + wiggleSeconds;
        }

        onInteracted.Invoke();
    }

    private void Update()
    {
        if (wiggleUntil < 0f)
            return;

        if (Time.time >= wiggleUntil)
        {
            transform.localRotation = restRotation;
            wiggleUntil = -1f;
            return;
        }

        // A quick shake that dies away.
        float left = (wiggleUntil - Time.time) / Mathf.Max(0.01f, wiggleSeconds);
        float angle = Mathf.Sin(Time.time * 45f) * wiggleDegrees * left;
        transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
