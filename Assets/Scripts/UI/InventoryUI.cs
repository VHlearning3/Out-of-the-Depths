using UnityEngine;
using UnityEngine.UI;

// Bottom-of-screen hotbar from the GDD: one rounded slot per inventory slot, with the item's icon (or a two- or
// three-letter monogram of its name), a count badge and a small key number; the selected slot gets the accent rim,
// pops a little, and the item's full name reads under the bar. Everything is built at runtime from generated
// sprites, so it needs no art; drop your own into Slot Fill / Slot Rim to replace the generated ones.
public class InventoryUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerInventory inventory;
    [Tooltip("Slot boxes are created under this. Leave empty to use this object's own rect.")]
    [SerializeField] private RectTransform container;
    [Tooltip("Optional: your own slot background (rounded box). Empty = generated.")]
    [SerializeField] private Sprite slotFill;
    [Tooltip("Optional: your own slot rim (rounded outline). Empty = generated.")]
    [SerializeField] private Sprite slotRim;
    [Tooltip("The old plain square sprite; only used if Slot Fill is empty and Generated Look is off.")]
    [SerializeField] private Sprite slotSprite;

    [Header("Look")]
    [SerializeField] private bool generatedLook = true;
    [SerializeField] private float slotSize = 58f;
    [SerializeField] private float spacing = 6f;
    [Tooltip("Slot background.")]
    [SerializeField] private Color slotColor = new Color(0.02f, 0.05f, 0.08f, 0.72f);
    [Tooltip("Rim and badge colour of the selected slot.")]
    [SerializeField] private Color selectedColor = new Color(0.35f, 0.85f, 0.95f, 1f);
    [SerializeField] private Color rimColor = new Color(1f, 1f, 1f, 0.14f);
    [SerializeField] private Color textColor = new Color(0.9f, 0.95f, 1f);
    [Tooltip("Size of the item name under the bar.")]
    [SerializeField] private int fontSize = 15;
    [Tooltip("The selected slot grows to this size, smoothly.")]
    [SerializeField] private float selectedScale = 1.1f;
    [Tooltip("How faint an empty slot is.")]
    [SerializeField, Range(0f, 1f)] private float emptyAlpha = 0.45f;

    private RectTransform[] slots;
    private Image[] fills;
    private Image[] rims;
    private Image[] icons;
    private Text[] monograms;
    private Text[] counts;
    private Image[] badges;
    private Text nameLabel;
    private int selected;
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
        slots = new RectTransform[n];
        fills = new Image[n];
        rims = new Image[n];
        icons = new Image[n];
        monograms = new Text[n];
        counts = new Text[n];
        badges = new Image[n];

        Sprite fillSprite = slotFill != null ? slotFill : generatedLook ? RoundedSprite(false) : slotSprite;
        Sprite rimSprite = slotRim != null ? slotRim : generatedLook ? RoundedSprite(true) : null;

        float step = slotSize + spacing;
        float startX = -(n - 1) * step * 0.5f;
        int monogramSize = Mathf.RoundToInt(slotSize * 0.36f);
        int smallSize = Mathf.Max(8, Mathf.RoundToInt(slotSize * 0.18f));

        for (int i = 0; i < n; i++)
        {
            RectTransform slot = MakeRect("Slot " + (i + 1), parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            slot.sizeDelta = new Vector2(slotSize, slotSize);
            slot.anchoredPosition = new Vector2(startX + i * step, 0f);
            slots[i] = slot;

            fills[i] = MakeImage("Fill", slot, fillSprite, slotColor);
            if (rimSprite != null)
                rims[i] = MakeImage("Rim", slot, rimSprite, rimColor);

            RectTransform icon = MakeRect("Icon", slot, Vector2.zero, Vector2.one);
            icon.offsetMin = new Vector2(8f, 8f);
            icon.offsetMax = new Vector2(-8f, -8f);
            icons[i] = icon.gameObject.AddComponent<Image>();
            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
            icons[i].enabled = false;

            monograms[i] = MakeText("Monogram", slot, TextAnchor.MiddleCenter, new Vector2(2f, 2f), monogramSize, FontStyle.Bold, textColor);

            Text key = MakeText("Key", slot, TextAnchor.UpperLeft, new Vector2(6f, 4f), smallSize, FontStyle.Normal, new Color(1f, 1f, 1f, 0.45f));
            key.text = (i + 1).ToString();

            // The count badge: a small accent pill in the corner, hidden until there is a stack.
            RectTransform badge = MakeRect("Badge", slot, new Vector2(1f, 0f), new Vector2(1f, 0f));
            badge.pivot = new Vector2(1f, 0f);
            badge.anchoredPosition = new Vector2(3f, -3f);
            badge.sizeDelta = new Vector2(slotSize * 0.42f, slotSize * 0.28f);
            badges[i] = badge.gameObject.AddComponent<Image>();
            badges[i].sprite = fillSprite;
            badges[i].type = Image.Type.Sliced;
            badges[i].color = selectedColor;
            badges[i].raycastTarget = false;
            badges[i].enabled = false;
            counts[i] = MakeText("Count", badge, TextAnchor.MiddleCenter, Vector2.zero, smallSize, FontStyle.Bold, new Color(0.02f, 0.05f, 0.08f));
        }

        // The selected item's full name, under the bar.
        RectTransform label = MakeRect("SelectedName", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        label.sizeDelta = new Vector2(n * step + 200f, fontSize + 8f);
        label.anchoredPosition = new Vector2(0f, -(slotSize * 0.5f + fontSize * 0.5f + 12f));
        nameLabel = label.gameObject.AddComponent<Text>();
        nameLabel.font = font;
        nameLabel.fontSize = fontSize;
        nameLabel.alignment = TextAnchor.MiddleCenter;
        nameLabel.color = textColor;
        nameLabel.raycastTarget = false;
        var shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1f, -1f);

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

    private static Image MakeImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        RectTransform rect = MakeRect(name, parent, Vector2.zero, Vector2.one);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text MakeText(string name, RectTransform parent, TextAnchor alignment, Vector2 padding, int size, FontStyle style, Color color)
    {
        RectTransform rect = MakeRect(name, parent, Vector2.zero, Vector2.one);
        rect.offsetMin = padding;
        rect.offsetMax = -padding;

        var text = rect.gameObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    public void Refresh()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            PlayerInventory.Slot slot = inventory.GetSlot(i);
            bool hasIcon = !slot.IsEmpty && slot.item.Icon != null;
            icons[i].enabled = hasIcon;
            icons[i].sprite = hasIcon ? slot.item.Icon : null;
            monograms[i].text = slot.IsEmpty || hasIcon ? string.Empty : Monogram(slot.item.DisplayName);
            bool stacked = slot.count > 1;
            badges[i].enabled = stacked;
            counts[i].text = stacked ? "x" + slot.count : string.Empty;
            fills[i].color = slot.IsEmpty ? new Color(slotColor.r, slotColor.g, slotColor.b, slotColor.a * emptyAlpha) : slotColor;
        }
        UpdateNameLabel();
    }

    private void OnSelectionChanged(int index)
    {
        if (slots == null)
            return;

        selected = index;
        for (int i = 0; i < slots.Length; i++)
            if (rims[i] != null)
                rims[i].color = i == index ? selectedColor : rimColor;
        UpdateNameLabel();
    }

    private void UpdateNameLabel()
    {
        if (nameLabel == null)
            return;
        PlayerInventory.Slot slot = inventory.GetSlot(selected);
        nameLabel.text = slot.IsEmpty ? string.Empty : Capitalise(slot.item.DisplayName);
    }

    // "bone key fragment" -> "BKF", "dagger" -> "DA".
    private static string Monogram(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;
        string[] words = name.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 1)
            return name.Length >= 2 ? name.Substring(0, 2).ToUpperInvariant() : name.ToUpperInvariant();
        string initials = string.Empty;
        for (int i = 0; i < words.Length && i < 3; i++)
            initials += char.ToUpperInvariant(words[i][0]);
        return initials;
    }

    private static string Capitalise(string name)
    {
        return string.IsNullOrEmpty(name) ? string.Empty : char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private void Update()
    {
        if (slots == null)
            return;

        // Ease the slot sizes toward their targets so selection reads as a pop, not a jump.
        float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        for (int i = 0; i < slots.Length; i++)
        {
            float target = i == selected ? selectedScale : 1f;
            float size = Mathf.Lerp(slots[i].localScale.x, target, blend);
            slots[i].localScale = new Vector3(size, size, 1f);
        }
    }

    // A rounded square, 64 px, with sliced borders so it can stretch: either the solid fill or just a 2 px rim.
    private static Sprite RoundedSprite(bool rimOnly)
    {
        const int size = 64;
        const float radius = 12f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - size * 0.5f;
                float py = y + 0.5f - size * 0.5f;
                float qx = Mathf.Abs(px) - size * 0.5f + radius;
                float qy = Mathf.Abs(py) - size * 0.5f + radius;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float d = Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - radius;   // signed distance, negative inside
                float a = rimOnly ? Mathf.Clamp01(1.5f - Mathf.Abs(d + 1.5f)) : Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(16f, 16f, 16f, 16f));
    }
}
