using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// A spot that takes items out of the inventory: a pedestal wanting 3 stone fragments, a lock wanting a key, the seaweed
// that turns 3 bone fragments into a bone key. You have to be holding the item (selected in the hotbar) and press E;
// each Placed Visual then glides from your hand to its spot and settles; onFilled fires once the last one has landed.
// The prompt tells the player what to do: hold the item, or what is still needed.
[RequireComponent(typeof(Collider))]
public class ItemSocket : MonoBehaviour, IInteractable
{
    [Header("Needs")]
    [SerializeField] private ItemDefinition requiredItem;
    [SerializeField, Min(1)] private int requiredAmount = 1;
    [Tooltip("Take the items out of the inventory. Off = the player keeps them (a key you reuse).")]
    [SerializeField] private bool consumeItems = true;
    [Tooltip("The item has to be the one in your hand (the selected hotbar slot). Off = anywhere in the inventory will do.")]
    [SerializeField] private bool requireHeld = true;
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
    [Tooltip("How long a piece takes to glide from your hand to its spot. 0 = appears instantly.")]
    [SerializeField] private float placeDuration = 0.7f;
    [Tooltip("Height of the gentle arc it travels on, in metres.")]
    [SerializeField] private float placeArcHeight = 0.35f;
    [Tooltip("Full turns a piece makes on the way, slowing as it lands. 0 = it just turns to its resting pose.")]
    [SerializeField] private float placeSpins = 0f;
    [Tooltip("Pause between pieces when several go in at once.")]
    [SerializeField] private float betweenPieces = 0.2f;

    [Header("Events")]
    public UnityEvent<int> onProgress = new UnityEvent<int>();
    public UnityEvent onFilled = new UnityEvent();

    public int Placed { get; private set; }
    public bool IsFilled => Placed >= requiredAmount;

    public string Prompt
    {
        get
        {
            if (placing)
                return "wait";
            if (requiredItem == null)
                return verb;
            string name = requiredItem.DisplayName;
            string count = $"({Placed}/{requiredAmount})";
            PlayerInventory inventory = PlayerInventoryInScene();
            if (inventory == null || inventory.Count(requiredItem) == 0)
                return $"needs {name} {count}";
            if (requireHeld && inventory.SelectedItem != requiredItem)
                return $"hold the {name} to {verb} it  (1-5 / wheel)";
            return $"{verb} {name} {count}";
        }
    }

    private Vector3[] restPositions;
    private Quaternion[] restRotations;
    private Vector3[] restScales;
    private bool placing;
    private PlayerInventory playerInventory;

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

    private PlayerInventory PlayerInventoryInScene()
    {
        if (playerInventory == null)
            playerInventory = FindFirstObjectByType<PlayerInventory>();
        return playerInventory;
    }

    public void Interact(GameObject interactor)
    {
        if (IsFilled || placing || requiredItem == null)
            return;

        var inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
            return;
        playerInventory = inventory;

        // You place what is in your hand: the prompt already says to select it if it is somewhere else.
        if (requireHeld && inventory.SelectedItem != requiredItem)
            return;

        int place = Mathf.Min(requiredAmount - Placed, inventory.Count(requiredItem));
        if (place <= 0)
            return;

        if (consumeItems)
            inventory.Remove(requiredItem, place);

        int first = Placed;
        Placed += place;
        StartCoroutine(PlacePieces(first, place, HandPoint(interactor), HandRotation(interactor), inventory));
    }

    // Where the held item sits: just in front of and below the camera, a little to the right, like in a hand.
    private static Vector3 HandPoint(GameObject interactor)
    {
        Camera camera = interactor.GetComponentInChildren<Camera>();
        if (camera == null)
            camera = Camera.main;
        if (camera == null)
            return interactor.transform.position + Vector3.up * 1.2f;
        Transform eye = camera.transform;
        return eye.position + eye.forward * 0.45f + eye.right * 0.15f - eye.up * 0.2f;
    }

    private static Quaternion HandRotation(GameObject interactor)
    {
        Camera camera = interactor.GetComponentInChildren<Camera>();
        if (camera == null)
            camera = Camera.main;
        return camera != null ? Quaternion.LookRotation(camera.transform.forward, Vector3.up) : interactor.transform.rotation;
    }

    private IEnumerator PlacePieces(int first, int amount, Vector3 from, Quaternion fromRotation, PlayerInventory inventory)
    {
        placing = true;
        for (int i = first; i < first + amount; i++)
        {
            if (i < placedVisuals.Length && placedVisuals[i] != null)
                yield return FlyIn(i, from, fromRotation);

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

    // From the hand to the rest pose: a gentle arc, easing in and out, turning smoothly into its resting rotation
    // (plus any spins asked for), growing to full size on the way, and a small settle as it lands.
    private IEnumerator FlyIn(int index, Vector3 from, Quaternion fromRotation)
    {
        Transform t = placedVisuals[index].transform;
        Vector3 to = restPositions[index];
        Quaternion rest = restRotations[index];
        Vector3 scale = restScales[index];
        placedVisuals[index].SetActive(true);

        for (float time = 0f; placeDuration > 0f && time < placeDuration; time += Time.deltaTime)
        {
            float k = Ease.InOutCubic(time / placeDuration);
            Vector3 position = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * placeArcHeight);
            Quaternion rotation = Quaternion.Slerp(fromRotation, rest, k);
            if (placeSpins > 0f)
                rotation = rotation * Quaternion.Euler(0f, placeSpins * 360f * (1f - k), 0f);
            t.SetPositionAndRotation(position, rotation);
            t.localScale = scale * Mathf.Lerp(0.8f, 1f, k);
            yield return null;
        }

        t.SetPositionAndRotation(to, rest);

        // The settle: a quick, small swell and back, like the piece seating itself.
        const float settle = 0.16f;
        for (float time = 0f; time < settle; time += Time.deltaTime)
        {
            float k = time / settle;
            t.localScale = scale * (1f + 0.07f * Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        t.localScale = scale;
    }

    private void Play(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position, volume);
    }
}
