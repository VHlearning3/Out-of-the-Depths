using UnityEngine;
using UnityEngine.UI;

// Bottom-of-screen hotbar from the GDD: one box per inventory slot showing the item's icon (or its name), the count and
// the key number; the selected slot is brighter. Boxes are built at runtime, so it only needs a rect and the white sprite.
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [Tooltip("Slot boxes are created under this. Leave empty to use this object's own rect.")]
    [SerializeField] private RectTransform container;
    [SerializeField] private Sprite slotSprite;

    [Header("Look")]
    [SerializeField] private float slotSize = 64f;
    [SerializeField] private float spacing = 8f;
    [SerializeField] private Color slotColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color selectedColor = new Color(0.25f, 0.65f, 0.75f, 0.85f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private int fontSize = 14;
    [Tooltip("The selected slot grows to this size, smoothly.")]
    [SerializeField] private float selectedScale = 1.12f;

    private Image[] boxes;
    private int selected;
    private Image[] icons;
    private Text[] labels;
    private Text[] counts;
    private Font font;

    private void OnEnable()
    {
        if (inventory == null)
            return;
        inventory.onChanged.AddListener(Refresh);
        inventory.onSelectionChanged.AddListener(OnSelectionChanged);
    }

    private void OnDisable()
    {
        if (inventory == null)
            return;
        inventory.onChanged.RemoveListener(Refresh);
        inventory.onSelectionChanged.RemoveListener(OnSelectionChanged);
    }

    private void Start()
    {
        if (inventory == null)
        {
            Debug.LogError($"{name}: InventoryUI has no Player Inventory assigned.", this);
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Build();
        Refresh();
    }

    private void Build()
    {
        RectTransform parent = container != null ? container : (RectTransform)transform;
        int n = inventory.SlotCount;
        boxes = new Image[n];
        icons = new Image[n];
        labels = new Text[n];
        counts = new Text[n];

        float step = slotSize + spacing;
        float startX = -(n - 1) * step * 0.5f;

        for (int i = 0; i < n; i++)
        {
            RectTransform box = MakeRect("Slot " + (i + 1), parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            box.sizeDelta = new Vector2(slotSize, slotSize);
            box.anchoredPosition = new Vector2(startX + i * step, 0f);
            boxes[i] = box.gameObject.AddComponent<Image>();
            boxes[i].sprite = slotSprite;
            boxes[i].color = slotColor;
            boxes[i].raycastTarget = false;

            RectTransform icon = MakeRect("Icon", box, Vector2.zero, Vector2.one);
            icon.offsetMin = new Vector2(8f, 8f);
            icon.offsetMax = new Vector2(-8f, -8f);
            icons[i] = icon.gameObject.AddComponent<Image>();
            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
            icons[i].enabled = false;

            labels[i] = MakeText("Name", box, TextAnchor.MiddleCenter, new Vector2(4f, 4f));
            labels[i].resizeTextForBestFit = true;
            labels[i].resizeTextMinSize = 8;
            labels[i].resizeTextMaxSize = fontSize;

            counts[i] = MakeText("Count", box, TextAnchor.LowerRight, new Vector2(4f, 2f));

            Text key = MakeText("Key", box, TextAnchor.UpperLeft, new Vector2(4f, 2f));
            key.text = (i + 1).ToString();
            key.fontSize = Mathf.Max(8, fontSize - 3);
        }

        OnSelectionChanged(inventory.SelectedIndex);
    }

    private static RectTransform MakeRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private Text MakeText(string name, RectTransform parent, TextAnchor alignment, Vector2 padding)
    {
        RectTransform rect = MakeRect(name, parent, Vector2.zero, Vector2.one);
        rect.offsetMin = padding;
        rect.offsetMax = -padding;

        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = textColor;
        text.raycastTarget = false;
        return text;
    }

    public void Refresh()
    {
        if (boxes == null)
            return;

        for (int i = 0; i < boxes.Length; i++)
        {
            PlayerInventory.Slot slot = inventory.GetSlot(i);
            bool hasIcon = !slot.IsEmpty && slot.item.Icon != null;
            icons[i].enabled = hasIcon;
            icons[i].sprite = hasIcon ? slot.item.Icon : null;
            labels[i].text = slot.IsEmpty || hasIcon ? string.Empty : slot.item.DisplayName;
            counts[i].text = slot.count > 1 ? "x" + slot.count : string.Empty;
        }
    }

    private void OnSelectionChanged(int index)
    {
        if (boxes == null)
            return;

        selected = index;
        for (int i = 0; i < boxes.Length; i++)
            boxes[i].color = i == index ? selectedColor : slotColor;
    }

    private void Update()
    {
        if (boxes == null)
            return;

        // Ease the slot sizes toward their targets so selection reads as a pop, not a jump.
        float blend = 1f - Mathf.Exp(-14f * Time.deltaTime);
        for (int i = 0; i < boxes.Length; i++)
        {
            float target = i == selected ? selectedScale : 1f;
            RectTransform rect = boxes[i].rectTransform;
            float size = Mathf.Lerp(rect.localScale.x, target, blend);
            rect.localScale = new Vector3(size, size, 1f);
        }
    }
}
