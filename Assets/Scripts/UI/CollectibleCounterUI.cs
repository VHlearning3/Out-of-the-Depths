using UnityEngine;
using UnityEngine.UI;

// Top-right "18 / 50": collectibles (pearls, shells) picked up, out of every collectible pickup that exists in the scene.
// With Auto Hide it only shows for a few seconds when the count changes. Generated Look puts it on a small dark pill
// with a pearl in front of the number (when the Icon child is still the plain white placeholder); on a new one the
// number pops a little.
public class CollectibleCounterUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private Text label;
    [Tooltip("0 = count the collectible pickups in the scene at start.")]
    [SerializeField] private int total = 0;
    [Tooltip("Fade it away except for a few seconds when the count changes (the Settings page can turn this off for the whole HUD).")]
    [SerializeField] private bool autoHide = true;
    [SerializeField] private float showSeconds = 4f;
    [Tooltip("A dark pill with a pearl and the count, in the HUD style, while the Icon child is still the white placeholder.")]
    [SerializeField] private bool generatedLook = true;

    private float poppedAt = -10f;

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
        if (generatedLook)
            Dress();
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
        poppedAt = Time.unscaledTime;
    }

    private void Refresh()
    {
        if (label == null)
            return;

        int have = inventory != null ? inventory.TotalCollectibles : 0;
        label.text = goal > 0 ? $"{have} / {goal}" : have.ToString();
    }

    private void Update()
    {
        if (label == null)
            return;
        float t = (Time.unscaledTime - poppedAt) / 0.35f;   // a new one: the number pops and settles
        float pop = t < 1f ? 1f + 0.22f * Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) : 1f;
        label.rectTransform.localScale = new Vector3(pop, pop, 1f);
    }

    // A dark pill, a pearl on the left, the count on the right.
    private void Dress()
    {
        Transform iconChild = transform.Find("Icon");
        var icon = iconChild != null ? iconChild.GetComponent<Image>() : null;
        if (icon == null || !(icon.sprite == null || icon.sprite.name.StartsWith("UI_White")))
            return;
        var rect = (RectTransform)transform;
        rect.sizeDelta = new Vector2(96f, 32f);

        var back = new GameObject("Backdrop", typeof(RectTransform)).AddComponent<Image>();
        back.transform.SetParent(transform, false);
        back.transform.SetAsFirstSibling();
        back.sprite = HudStyle.Pill;
        back.type = Image.Type.Sliced;
        back.color = HudStyle.Ink;
        back.raycastTarget = false;
        StretchFull(back.rectTransform);
        var rim = new GameObject("Rim", typeof(RectTransform)).AddComponent<Image>();
        rim.transform.SetParent(transform, false);
        rim.transform.SetSiblingIndex(1);
        rim.sprite = HudStyle.PillRim;
        rim.type = Image.Type.Sliced;
        rim.color = HudStyle.Rim;
        rim.raycastTarget = false;
        StretchFull(rim.rectTransform);

        icon.sprite = HudStyle.Pearl;
        icon.color = Color.white;
        icon.preserveAspect = true;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(17f, 0f);
        iconRect.sizeDelta = new Vector2(20f, 20f);

        if (label != null)
        {
            RectTransform text = label.rectTransform;
            text.anchorMin = Vector2.zero;
            text.anchorMax = Vector2.one;
            text.offsetMin = new Vector2(32f, 0f);
            text.offsetMax = new Vector2(-13f, 0f);
            text.pivot = new Vector2(1f, 0.5f);
            label.alignment = TextAnchor.MiddleRight;
            label.fontSize = 17;
            label.color = HudStyle.Text;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (label.GetComponent<Shadow>() == null)
            {
                var shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
                shadow.effectDistance = new Vector2(1f, -1.5f);
            }
        }
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
