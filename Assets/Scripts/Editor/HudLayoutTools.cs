using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Tools > Out of the Depths > Apply GDD HUD Layout: arranges the HUD like the GDD mock-up - fish-shaped hunger gauge
// top-left (health under it), collectible counter top-right, "(E) eat" keycap prompt bottom-centre,
// inventory slots bottom-right. Only moves and creates, never deletes, so it is safe to re-run. The arena builder runs it.
public static class HudLayoutTools
{
    private const string WhiteSpritePath = "Assets/Art/Textures/UI/UI_White.png";
    private const float Margin = 24f;

    [MenuItem("Tools/Out of the Depths/Apply GDD HUD Layout (Open Scene)")]
    public static void Apply()
    {
        GameObject hud = GameObject.Find("HUD");
        if (hud == null)
        {
            Debug.LogWarning("No HUD object in the open scene, layout not applied.");
            return;
        }

        var root = hud.GetComponent<RectTransform>();
        Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        Font font = FontFrom(hud);
        if (GameFont.Custom != null)
            GameFont.Apply(hud, font, false);   // the labels already there switch to it too
        var inventory = Object.FindFirstObjectByType<PlayerInventory>();

        LayoutHungerGauge(root, white);
        LayoutHealthBar(root);
        LayoutInventory(root);
        LayoutPrompt(root, white, font);
        EnsureCollectibleCounter(root, white, font, inventory);

        EditorSceneManager.MarkSceneDirty(hud.scene);
        Debug.Log("GDD HUD layout applied.");
    }

    // Hunger becomes a vertical gauge. With UI_White it is a rounded bar; give Background and Fill the fish sprite and
    // the same setup fills the fish from the tail up.
    private static void LayoutHungerGauge(RectTransform root, Sprite white)
    {
        RectTransform bar = Child(root, "HungerBar");
        if (bar == null)
            return;

        Place(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(90f, 170f));

        RectTransform background = Child(bar, "Background");
        if (background != null)
            Stretch(background);

        RectTransform fill = Child(bar, "Fill");
        if (fill != null)
        {
            Stretch(fill);
            var image = fill.GetComponent<Image>();
            if (image != null)
            {
                if (image.sprite == null)
                    image.sprite = white;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Vertical;
                image.fillOrigin = (int)Image.OriginVertical.Bottom;
                image.fillAmount = 1f;

                var meter = bar.GetComponent<StatBarUI>();
                if (meter != null)
                    SetReference(meter, "fillImage", image);
            }
        }

        RectTransform label = Child(bar, "Label");
        if (label != null)
        {
            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(1f, 0f);
            label.pivot = new Vector2(0.5f, 1f);
            label.anchoredPosition = new Vector2(0f, -4f);
            label.sizeDelta = new Vector2(0f, 22f);
            var text = label.GetComponent<Text>();
            if (text != null)
                text.alignment = TextAnchor.UpperCenter;
        }
    }

    private static void LayoutHealthBar(RectTransform root)
    {
        RectTransform bar = Child(root, "HealthBar");
        if (bar != null)
            Place(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -(Margin + 170f + 34f)), new Vector2(150f, 14f));
    }

    private static void LayoutInventory(RectTransform root)
    {
        RectTransform inventory = Child(root, "Inventory");
        if (inventory != null)
            Place(inventory, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, Margin), new Vector2(352f, 80f));
    }

    // "(E) eat": a keycap and the existing prompt label side by side, centred as one unit and hidden when there is no target.
    private static void LayoutPrompt(RectTransform root, Sprite white, Font font)
    {
        RectTransform label = Child(root, "InteractPrompt");
        RectTransform group = Child(root, "InteractPromptRoot");
        if (label == null && group == null)
            return;

        if (group == null)
        {
            group = NewRect("InteractPromptRoot", root);
            var layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fitter = group.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform keycap = NewRect("Keycap", group);
            var keyImage = keycap.gameObject.AddComponent<Image>();
            keyImage.sprite = white;
            keyImage.color = new Color(0.1f, 0.12f, 0.15f, 0.9f);
            var element = keycap.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 36f;
            element.preferredHeight = 36f;
            Text keyText = NewText("E", keycap, font, 22, TextAnchor.MiddleCenter);
            keyText.fontStyle = FontStyle.Bold;
            Stretch(keyText.rectTransform);

            if (label != null)
                label.SetParent(group, false);
        }

        Place(group, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(300f, 44f));

        if (label != null)
        {
            var text = label.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
                text.fontSize = Mathf.Max(text.fontSize, 22);
            }
            label.sizeDelta = new Vector2(0f, 36f);
        }

        var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
        if (interactor != null)
        {
            var so = new SerializedObject(interactor);
            so.FindProperty("promptRoot").objectReferenceValue = group.gameObject;
            so.FindProperty("promptFormat").stringValue = "{0}";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void EnsureCollectibleCounter(RectTransform root, Sprite white, Font font, PlayerInventory inventory)
    {
        RectTransform counter = Child(root, "Collectibles");
        if (counter == null)
        {
            counter = NewRect("Collectibles", root);
            Text count = NewText("0/0", counter, font, 26, TextAnchor.MiddleRight);
            count.name = "Count";
            Stretch(count.rectTransform, 0f, 0f, 44f, 0f);

            RectTransform icon = NewRect("Icon", counter);
            Place(icon, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(32f, 32f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.sprite = white;
            iconImage.color = new Color(0.95f, 0.9f, 0.8f);

            var ui = counter.gameObject.AddComponent<CollectibleCounterUI>();
            SetReference(ui, "label", count);
        }

        var counterUi = counter.GetComponent<CollectibleCounterUI>();
        if (counterUi != null && inventory != null)
            SetReference(counterUi, "inventory", inventory);

        Place(counter, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -Margin), new Vector2(140f, 40f));
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private static RectTransform Child(RectTransform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child as RectTransform : null;
    }

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Text NewText(string content, RectTransform parent, Font font, int size, TextAnchor alignment)
    {
        var text = NewRect("Text", parent).gameObject.AddComponent<Text>();
        text.text = content;
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetReference(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
            return;
        prop.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The game font (Assets/Resources/Fonts), else whatever the HUD already uses, else the built-in one.
    private static Font FontFrom(GameObject hud)
    {
        if (GameFont.Custom != null)
            return GameFont.Custom;
        var any = hud.GetComponentInChildren<Text>(true);
        return any != null && any.font != null ? any.font : GameFont.Font;
    }
}
