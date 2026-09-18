using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Press E to pick up: puts the item in the player's inventory, plays feedback and pops out of existence.
// Root = this script + collider, mesh in a child called Visual (it bobs and spins), so the model can be swapped without touching logic.
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [Tooltip("Which item this is (Assets/Items). New ones: Assets > Create > Out of the Depths > Item.")]
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;

    [Header("Idle motion")]
    [Tooltip("The mesh that bobs and spins. Empty = first child.")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobAmount = 0.08f;
    [SerializeField] private float bobSpeed = 1.6f;
    [Tooltip("Degrees per second. 0 = no spin.")]
    [SerializeField] private float spinSpeed = 40f;

    [Header("Feedback")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.6f;
    [SerializeField] private GameObject pickedUpVfx;
    [Tooltip("The little grow-then-shrink pop when it is taken. 0 = vanishes instantly.")]
    [SerializeField] private float collectDuration = 0.25f;

    [Header("Events")]
    public UnityEvent onPickedUp = new UnityEvent();

    public ItemDefinition Item => item;
    public int Amount => amount;
    public string Prompt => item != null ? "pick up " + item.DisplayName : "pick up";

    private Vector3 visualRestPosition;
    private Quaternion visualRestRotation;
    private float phase;
    private bool collected;

    private void Awake()
    {
        if (visual == null && transform.childCount > 0)
            visual = transform.GetChild(0);
        if (visual != null)
        {
            visualRestPosition = visual.localPosition;
            visualRestRotation = visual.localRotation;
        }
        phase = Random.value * 10f;
    }

    private void Update()
    {
        if (visual == null || collected)
            return;

        visual.localPosition = visualRestPosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobAmount);
        if (spinSpeed != 0f)
            visual.localRotation = visualRestRotation * Quaternion.Euler(0f, (Time.time * spinSpeed + phase * 36f) % 360f, 0f);
    }

    public void Interact(GameObject interactor)
    {
        if (collected)
            return;

        if (item == null)
        {
            Debug.LogWarning($"{name}: Pickup Item has no Item assigned, nothing to pick up.", this);
            return;
        }

        var inventory = interactor.GetComponentInParent<PlayerInventory>();
        if (inventory == null)
        {
            Debug.LogWarning($"{name}: {interactor.name} has no Player Inventory component, can't pick up.", this);
            return;
        }

        int stored = inventory.Add(item, amount);
        if (stored == 0)
            return;

        // Inventory took only part of a stack: the rest stays here for later.
        amount -= stored;
        if (amount > 0)
            return;

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position, pickupVolume);

        if (pickedUpVfx != null)
            Instantiate(pickedUpVfx, transform.position, transform.rotation);

        collected = true;
        onPickedUp.Invoke();
        StartCoroutine(Collect());
    }

    // Quick grow, then shrink to nothing while rising a little, then gone.
    private IEnumerator Collect()
    {
        GetComponent<Collider>().enabled = false;
        if (visual != null && collectDuration > 0f)
        {
            Vector3 startScale = visual.localScale;
            Vector3 startPosition = visual.localPosition;
            for (float t = 0f; t < collectDuration; t += Time.deltaTime)
            {
                float k = t / collectDuration;
                float size = k < 0.3f ? Mathf.Lerp(1f, 1.25f, k / 0.3f) : Mathf.Lerp(1.25f, 0f, Ease.OutCubic((k - 0.3f) / 0.7f));
                visual.localScale = startScale * size;
                visual.localPosition = startPosition + Vector3.up * (0.5f * k);
                yield return null;
            }
            visual.localScale = startScale;
            visual.localPosition = startPosition;
        }
        gameObject.SetActive(false);
    }
}
