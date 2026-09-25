using UnityEngine;
using UnityEngine.UI;

// Bottom-of-screen hotbar from the GDD: one rounded slot per inventory slot, with the item's icon (or a two- or
// three-letter monogram of its name), a count badge and a small key number; the selected slot gets the accent rim,
// pops a little, and the item's full name reads under the bar. Everything is built at runtime from generated
// sprites, so it needs no art; drop your own into Slot Fill / Slot Rim to replace the generated ones. The weapon slots
// (Player Inventory's Weapon Slots, at the end) stand a little apart with a warm rim and a faint blade while empty.
// With Auto Hide the bar fades away while it is not needed: it shows for a few seconds after you switch slots or get or
// lose something, and while you look at something that wants an item (the red / green prompts).
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
    [Tooltip("Extra gap between the item slots and the weapon slots.")]
    [SerializeField] private float groupGap = 18f;
    [Tooltip("Rim of a weapon slot that is not selected.")]
    [SerializeField] private Color weaponRimColor = new Color(0.95f, 0.72f, 0.35f, 0.4f);
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

    [Header("Auto hide")]
    [Tooltip("Fade the hotbar away while it is not needed (the Settings page can turn this off for the whole HUD).")]
    [SerializeField] private bool autoHide = false;
    [Tooltip("How long it stays up after you switch slots or the inventory changes.")]
    [SerializeField] private float showSeconds = 3f;

    private RectTransform[] slots;
    private Image[] fills;
    private Image[] rims;
    private Image[] icons;
    private Text[] monograms;
    private Text[] counts;
    private Image[] badges;
    private Image[] glows;
    private Image[] backlights;
    private Image[] discs;
    private float[] baseY;
    private Text nameLabel;
    private int selected;
    private Font font;
    private Sprite bladeSprite;
    private HudAutoHide fade;
    private PlayerInteractor interactor;

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

        font = GameFont.Font;
        Build();
        Refresh();
        if (autoHide)
        {
            fade = HudAutoHide.On(this, showSeconds);
            interactor = inventory.GetComponentInParent<PlayerInteractor>();
            if (interactor == null)
                interactor = FindFirstObjectByType<PlayerInteractor>();
            // Looking at a lock or socket that wants an item: the hotbar shows what you have.
            fade.Needed = () => interactor != null && interactor.CurrentTarget is IPromptTone toned && toned.Tone != PromptTone.Normal;
        }
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
        glows = new Image[n];
        backlights = new Image[n];
        discs = new Image[n];
        baseY = new float[n];

        // The generated look is the HUD's own (HudStyle): shaded glassy slots, a hairline rim, the accent for the
        // selected one; the colours on this component still tint it.
        bool styled = generatedLook && slotFill == null;
        if (styled)
        {
            slotColor = new Color(0.09f, 0.19f, 0.235f, 0.82f);
            selectedColor = HudStyle.Accent;
            rimColor = HudStyle.Rim;
            weaponRimColor = new Color(HudStyle.Gold.r, HudStyle.Gold.g, HudStyle.Gold.b, 0.38f);
            textColor = HudStyle.Text;
        }
        Sprite fillSprite = slotFill != null ? slotFill : generatedLook ? HudStyle.RoundedShaded : slotSprite;
        Sprite rimSprite = slotRim != null ? slotRim : generatedLook ? HudStyle.RoundedRim : null;

        float step = slotSize + spacing;
        int weapons = inventory.WeaponSlots;
        float gap = weapons > 0 && weapons < n ? groupGap : 0f;
        float startX = -((n - 1) * step + gap) * 0.5f;
        bladeSprite = weapons > 0 ? BladeSprite() : null;
        int monogramSize = Mathf.RoundToInt(slotSize * 0.36f);
        int smallSize = Mathf.Max(8, Mathf.RoundToInt(slotSize * 0.18f));

        for (int i = 0; i < n; i++)
        {
            RectTransform slot = MakeRect("Slot " + (i + 1), parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            slot.sizeDelta = new Vector2(slotSize, slotSize);
            slot.anchoredPosition = new Vector2(startX + i * step + (inventory.IsWeaponSlot(i) ? gap : 0f), 0f);
            slots[i] = slot;
            baseY[i] = 0f;

            if (styled)
            {
                // A soft accent glow round the selected slot, behind everything.
                glows[i] = MakeImage("Glow", slot, HudStyle.Glow, new Color(selectedColor.r, selectedColor.g, selectedColor.b, 0f));
                glows[i].rectTransform.offsetMin = new Vector2(-22f, -22f);
                glows[i].rectTransform.offsetMax = new Vector2(22f, 22f);
            }
            fills[i] = MakeImage("Fill", slot, fillSprite, slotColor);
            if (rimSprite != null)
                rims[i] = MakeImage("Rim", slot, rimSprite, rimColor);

            if (styled)
            {
                // A faint light behind an icon, so dark item renders still read; a disc behind a monogram.
                backlights[i] = MakeImage("Backlight", slot, HudStyle.Backlight, new Color(0.7f, 0.94f, 0.96f, 0.2f));
                backlights[i].rectTransform.offsetMin = new Vector2(3f, 3f);
                backlights[i].rectTransform.offsetMax = new Vector2(-3f, -3f);
                backlights[i].enabled = false;
                discs[i] = MakeImage("Disc", slot, HudStyle.Pill, new Color(selectedColor.r, selectedColor.g, selectedColor.b, 0.12f));
                discs[i].type = Image.Type.Simple;
                float inset = slotSize * 0.22f;
                discs[i].rectTransform.offsetMin = new Vector2(inset, inset);
                discs[i].rectTransform.offsetMax = new Vector2(-inset, -inset);
                discs[i].enabled = false;
            }

            RectTransform icon = MakeRect("Icon", slot, Vector2.zero, Vector2.one);
            icon.offsetMin = new Vector2(8f, 8f);
            icon.offsetMax = new Vector2(-8f, -8f);
            icons[i] = icon.gameObject.AddComponent<Image>();
            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
            icons[i].enabled = false;

            monograms[i] = MakeText("Monogram", slot, TextAnchor.MiddleCenter, new Vector2(2f, 2f), styled ? Mathf.RoundToInt(slotSize * 0.26f) : monogramSize, styled ? FontStyle.Normal : FontStyle.Bold, textColor);

            Text key = MakeText("Key", slot, TextAnchor.UpperLeft, new Vector2(6f, 4f), styled ? Mathf.Max(8, Mathf.RoundToInt(slotSize * 0.19f)) : smallSize, FontStyle.Normal, new Color(1f, 1f, 1f, styled ? 0.4f : 0.45f));
            key.text = (i + 1).ToString();

            // The count badge: a small pill tucked into the bottom-right corner, hidden until there is a stack.
            RectTransform badge = MakeRect("Badge", slot, new Vector2(1f, 0f), new Vector2(1f, 0f));
            badge.pivot = new Vector2(1f, 0f);
            badge.anchoredPosition = styled ? new Vector2(-4f, 4f) : new Vector2(3f, -3f);
            badge.sizeDelta = styled ? new Vector2(slotSize * 0.36f, slotSize * 0.27f) : new Vector2(slotSize * 0.42f, slotSize * 0.28f);
            badges[i] = badge.gameObject.AddComponent<Image>();
            badges[i].sprite = styled ? HudStyle.Pill : fillSprite;
            badges[i].type = Image.Type.Sliced;
            badges[i].color = selectedColor;
            badges[i].raycastTarget = false;
            badges[i].enabled = false;
            counts[i] = MakeText("Count", badge, TextAnchor.MiddleCenter, Vector2.zero, smallSize, FontStyle.Bold, new Color(0.02f, 0.05f, 0.08f));
            if (styled)
                Object.Destroy(counts[i].GetComponent<Shadow>());   // dark on a light pill: no shadow
        }

        // The selected item's full name, under the bar.
        RectTransform label = MakeRect("SelectedName", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        label.sizeDelta = new Vector2(n * step + gap + 200f, fontSize + 8f);
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
            bool emptyWeaponSlot = slot.IsEmpty && inventory.IsWeaponSlot(i) && bladeSprite != null;
            icons[i].enabled = hasIcon || emptyWeaponSlot;
            icons[i].sprite = hasIcon ? slot.item.Icon : emptyWeaponSlot ? bladeSprite : null;
            icons[i].color = hasIcon ? Color.white : new Color(weaponRimColor.r, weaponRimColor.g, weaponRimColor.b, glows[i] != null ? 0.2f : 0.3f);
            monograms[i].text = slot.IsEmpty || hasIcon ? string.Empty : Monogram(slot.item.DisplayName);
            bool stacked = slot.count > 1;
            badges[i].enabled = stacked;
            bool styled = glows[i] != null;
            counts[i].text = stacked ? (styled ? slot.count.ToString() : "x" + slot.count) : string.Empty;
            fills[i].color = slot.IsEmpty ? new Color(slotColor.r, slotColor.g, slotColor.b, slotColor.a * emptyAlpha) : slotColor;
            if (backlights[i] != null)
                backlights[i].enabled = hasIcon;
            if (discs[i] != null)
                discs[i].enabled = !slot.IsEmpty && !hasIcon;
        }
        PaintBadges();
        UpdateNameLabel();
        fade?.Wake();
    }

    private void OnSelectionChanged(int index)
    {
        if (slots == null)
            return;

        selected = index;
        for (int i = 0; i < slots.Length; i++)
            if (rims[i] != null)
                rims[i].color = i == index ? selectedColor : inventory.IsWeaponSlot(i) ? weaponRimColor : rimColor;
        PaintBadges();
        UpdateNameLabel();
        fade?.Wake();
    }

    // In the generated look the selected slot's count badge is in the accent, the others pale.
    private void PaintBadges()
    {
        for (int i = 0; i < slots.Length; i++)
            if (glows[i] != null)
                badges[i].color = i == selected ? selectedColor : new Color(0.91f, 0.97f, 0.97f, 0.85f);
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

        // Ease the slot sizes toward their targets so selection reads as a pop, not a jump; in the generated look the
        // selected slot also lifts a little and its glow fades in.
        float blend = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        for (int i = 0; i < slots.Length; i++)
        {
            bool on = i == selected;
            float target = on ? selectedScale : 1f;
            float size = Mathf.Lerp(slots[i].localScale.x, target, blend);
            slots[i].localScale = new Vector3(size, size, 1f);
            if (glows[i] == null)
                continue;
            Vector2 at = slots[i].anchoredPosition;
            slots[i].anchoredPosition = new Vector2(at.x, Mathf.Lerp(at.y, baseY[i] + (on ? 4f : 0f), blend));
            Color glow = glows[i].color;
            glows[i].color = new Color(glow.r, glow.g, glow.b, Mathf.Lerp(glow.a, on ? 0.13f : 0f, blend));
        }
    }

    // A plain blade for an empty weapon slot, drawn at a slant: a tapering blade, a cross guard and a grip.
    private static Sprite BladeSprite()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var pixels = new Color32[size * size];
        Vector2 tip = new Vector2(52f, 52f), hilt = new Vector2(22f, 22f), pommel = new Vector2(11f, 11f);
        Vector2 across = new Vector2(-1f, 1f).normalized;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float a = 0f;
                float t;
                float blade = Segment(p, hilt, tip, out t);
                a = Mathf.Max(a, Mathf.Clamp01(Mathf.Lerp(4.5f, 0.8f, t) - blade));
                a = Mathf.Max(a, Mathf.Clamp01(2.6f - Segment(p, hilt - across * 9f, hilt + across * 9f, out _)));
                a = Mathf.Max(a, Mathf.Clamp01(2.4f - Segment(p, pommel, hilt, out _)));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Distance from p to the segment a-b, and how far along it (0..1) the nearest point is.
    private static float Segment(Vector2 p, Vector2 a, Vector2 b, out float along)
    {
        Vector2 ab = b - a;
        along = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * along);
    }
}
