using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// A spot that takes items out of the inventory: a pedestal wanting 3 stone fragments, a lock wanting a key, the seaweed
// that turns 3 bone fragments into a bone key. Press E with the items on you and they go in automatically (GDD):
// each Placed Visual flies in from the player, spinning, and lands on its spot; onFilled fires once the last one has landed.
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
    [Tooltip("Switched on one by one as pieces go in (e.g. fragment meshes sitting on the pedestal). Place them where they should end up.")]
    [SerializeField] private GameObject[] placedVisuals;
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioClip completeSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;

    [Header("Placing animation")]
    [Tooltip("How long a piece takes to fly from the player to its spot. 0 = appears instantly.")]
    [SerializeField] private float placeDuration = 0.9f;
    [Tooltip("Height of the arc it travels on.")]
    [SerializeField] private float placeArcHeight = 1.2f;
    [Tooltip("Full turns a piece makes on the way, slowing down as it lands.")]
    [SerializeField] private float placeSpins = 2f;
    [Tooltip("Pause between pieces when several go in at once.")]
    [SerializeField] private float betweenPieces = 0.25f;

    [Header("Events")]
    public UnityEvent<int> onProgress = new UnityEvent<int>();
    public UnityEvent onFilled = new UnityEvent();

    public int Placed { get; private set; }
    public bool IsFilled => Placed >= requiredAmount;
    public string Prompt => placing ? "wait" : requiredItem == null ? verb : $"{verb} {requiredItem.DisplayName} ({Placed}/{requiredAmount})";

    private Vector3[] restPositions;
    private Quaternion[] restRotations;
    private Vector3[] restScales;
    private bool placing;

    private void Awake()
    {
        placedVisuals ??= new GameObject[0];
        restPositions = new Vector3[placedVisuals.Length];
        restRotations = new Quaternion[placedVisuals.Length];
        restScales = new Vector3[placedVisuals.Length];
        for (int i = 0; i < placedVisuals.Length; i++)
        {
            if (placedVisuals[i] == null)
                continue;
            Transform t = placedVisuals[i].transform;
            restPositions[i] = t.position;
            restRotations[i] = t.rotation;
            restScales[i] = t.localScale;
            placedVisuals[i].SetActive(false);
        }
    }

    public void Interact(GameObject interactor)
    {
        if (IsFilled || placing || requiredItem == null)
            return;

        var inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
            return;

        int place = Mathf.Min(requiredAmount - Placed, inventory.Count(requiredItem));
        if (place <= 0)
            return;

        if (consumeItems)
            inventory.Remove(requiredItem, place);

        int first = Placed;
        Placed += place;
        Vector3 from = interactor.transform.position + Vector3.up * 1.2f;
        StartCoroutine(PlacePieces(first, place, from, inventory));
    }

    private IEnumerator PlacePieces(int first, int amount, Vector3 from, PlayerInventory inventory)
    {
        placing = true;
        for (int i = first; i < first + amount; i++)
        {
            if (i < placedVisuals.Length && placedVisuals[i] != null)
                yield return FlyIn(i, from);

            bool last = i + 1 >= requiredAmount;
            Play(last && completeSound != null ? completeSound : placeSound);
            onProgress.Invoke(i + 1);

            if (!last && betweenPieces > 0f)
                yield return new WaitForSeconds(betweenPieces);
        }
        placing = false;

        if (!IsFilled)
            yield break;

        if (rewardItem != null)
            inventory.Add(rewardItem, rewardAmount);

        // Done: disabled so the E prompt goes away.
        enabled = false;
        onFilled.Invoke();
    }

    // Arc from the player's hands to the rest pose, spinning down, with a small bounce on landing.
    private IEnumerator FlyIn(int index, Vector3 from)
    {
        Transform t = placedVisuals[index].transform;
        Vector3 to = restPositions[index];
        Quaternion rest = restRotations[index];
        Vector3 scale = restScales[index];
        placedVisuals[index].SetActive(true);

        for (float time = 0f; placeDuration > 0f && time < placeDuration; time += Time.deltaTime)
        {
            float k = time / placeDuration;
            Vector3 position = Vector3.Lerp(from, to, Ease.InOutCubic(k)) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * placeArcHeight);
            float spin = placeSpins * 360f * (1f - Ease.OutCubic(k));
            t.SetPositionAndRotation(position, rest * Quaternion.Euler(0f, spin, 0f));
            t.localScale = scale * Mathf.Lerp(0.6f, 1f, Ease.OutBack(k));
            yield return null;
        }

        t.SetPositionAndRotation(to, rest);
        t.localScale = scale;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
