using UnityEngine;
using UnityEngine.UI;

// Top-right "18/50": collectibles (pearls, shells) picked up, out of every collectible pickup that exists in the scene.
// With Auto Hide it only shows for a few seconds when the count changes.
public class CollectibleCounterUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Text label;
    [Tooltip("0 = count the collectible pickups in the scene at start.")]
    [SerializeField] private int total = 0;
    [Tooltip("Fade it away except for a few seconds when the count changes (the Settings page can turn this off for the whole HUD).")]
    [SerializeField] private bool autoHide = true;
    [SerializeField] private float showSeconds = 4f;

    private HudAutoHide fade;

    private int goal;

    private void OnEnable()
    {
        if (inventory != null)
            inventory.onCollectibleChanged.AddListener(OnCollectibleChanged);
    }

    private void OnDisable()
    {
        if (inventory != null)
            inventory.onCollectibleChanged.RemoveListener(OnCollectibleChanged);
    }

    private void Start()
    {
        if (autoHide)
            fade = HudAutoHide.On(this, showSeconds);
        goal = total;
        if (goal <= 0)
        {
            foreach (PickupItem pickup in FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup.Item != null && pickup.Item.Kind == ItemDefinition.Category.Collectible)
                    goal += pickup.Amount;
            }
            if (inventory != null)
                goal += inventory.TotalCollectibles;
        }
        Refresh();
    }

    private void OnCollectibleChanged(ItemDefinition item, int count)
    {
        Refresh();
        fade?.Wake();
    }

    private void Refresh()
    {
        if (label == null)
            return;

        int have = inventory != null ? inventory.TotalCollectibles : 0;
        label.text = goal > 0 ? $"{have}/{goal}" : have.ToString();
    }
}
