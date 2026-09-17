using UnityEngine;
using UnityEngine.Events;

// Press E to pick up: puts the item in the player's inventory, plays feedback and hides the object.
// Root = this script + collider, mesh in a child called Visual, so the model can be swapped without touching logic.
[RequireComponent(typeof(Collider))]
public class PickupItem : MonoBehaviour, IInteractable
{
    [Header("Item")]
    [Tooltip("Which item this is (Assets/Items). New ones: Assets > Create > Out of the Depths > Item.")]
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(1)] private int amount = 1;

    [Header("Feedback")]
    [SerializeField] private AudioClip pickupSound;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.6f;
    [SerializeField] private GameObject pickedUpVfx;

    [Header("Events")]
    public UnityEvent onPickedUp = new UnityEvent();

    public ItemDefinition Item => item;
    public string Prompt => item != null ? "pick up " + item.DisplayName : "pick up";

    public void Interact(GameObject interactor)
    {
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

        onPickedUp.Invoke();
        gameObject.SetActive(false);
    }
}
