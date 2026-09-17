using UnityEngine;
using UnityEngine.Events;

// A spot that takes items out of the inventory: a pedestal wanting 3 stone fragments, a lock wanting a key, the seaweed
// that turns 3 bone fragments into a bone key. Press E with the items on you and they go in automatically (GDD);
// onFilled fires when complete - wire it to open a door, enable a chest, etc.
[RequireComponent(typeof(Collider))]
public class ItemSocket : MonoBehaviour, IInteractable
{
    [Header("Needs")]
    [SerializeField] private ItemDefinition requiredItem;
    [SerializeField, Min(1)] private int requiredAmount = 1;
    [Tooltip("Take the items out of the inventory. Off = the player keeps them (a key you reuse).")]
    [SerializeField] private bool consumeItems = true;
    [Tooltip("Prompt reads '<verb> <item> (placed/needed)'.")]
    [SerializeField] private string verb = "place";

    [Header("Gives (optional)")]
    [Tooltip("Handed to the player when complete, e.g. the bone key made from 3 fragments.")]
    [SerializeField] private ItemDefinition rewardItem;
    [SerializeField, Min(1)] private int rewardAmount = 1;

    [Header("Feedback")]
    [Tooltip("Switched on one by one as pieces go in (e.g. fragment meshes sitting on the pedestal).")]
    [SerializeField] private GameObject[] placedVisuals;
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip completeSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("Events")]
    public UnityEvent<int> onProgress = new UnityEvent<int>();
    public UnityEvent onFilled = new UnityEvent();

    public int Placed { get; private set; }
    public bool IsFilled => Placed >= requiredAmount;
    public string Prompt => requiredItem == null ? verb : $"{verb} {requiredItem.DisplayName} ({Placed}/{requiredAmount})";

    private void Awake()
    {
        UpdateVisuals();
    }

    public void Interact(GameObject interactor)
    {
        if (IsFilled || requiredItem == null)
            return;

        var inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
            return;

        int place = Mathf.Min(requiredAmount - Placed, inventory.Count(requiredItem));
        if (place <= 0)
            return;

        if (consumeItems)
            inventory.Remove(requiredItem, place);

        Placed += place;
        UpdateVisuals();
        onProgress.Invoke(Placed);

        if (!IsFilled)
        {
            Play(placeSound);
            return;
        }

        Play(completeSound != null ? completeSound : placeSound);
        if (rewardItem != null)
            inventory.Add(rewardItem, rewardAmount);

        // Done: disabled so the E prompt goes away.
        enabled = false;
        onFilled.Invoke();
    }

    private void UpdateVisuals()
    {
        for (int i = 0; i < placedVisuals.Length; i++)
        {
            if (placedVisuals[i] != null)
                placedVisuals[i].SetActive(i < Placed);
        }
    }

    private void Play(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
