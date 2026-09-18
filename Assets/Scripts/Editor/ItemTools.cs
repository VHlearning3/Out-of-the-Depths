using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Item assets from the GDD list (Assets/Items) and the inventory hotbar HUD.
// Tools > Out of the Depths > Create GDD Items / Add Inventory HUD To Open Scene. The test arena builder runs both.
public static class ItemTools
{
    public const string ItemFolder = "Assets/Items";
    private const string WhiteSpritePath = "Assets/Art/Textures/UI/UI_White.png";

    private struct Spec
    {
        public string asset;
        public string name;
        public ItemDefinition.Category category;
        public int stack;

        public Spec(string asset, string name, ItemDefinition.Category category, int stack)
        {
            this.asset = asset;
            this.name = name;
            this.category = category;
            this.stack = stack;
        }
    }

    private static readonly Spec[] GddItems =
    {
        new Spec("Item_StoneFragment", "stone fragment", ItemDefinition.Category.PuzzlePiece, 3),
        new Spec("Item_BoneKeyFragment", "bone key fragment", ItemDefinition.Category.PuzzlePiece, 3),
        new Spec("Item_BoneKey", "bone key", ItemDefinition.Category.Key, 1),
        new Spec("Item_SymbolKey", "symbol key", ItemDefinition.Category.Key, 1),
        new Spec("Item_FirstRoomKey", "first room key", ItemDefinition.Category.Key, 1),
        new Spec("Item_Dagger", "dagger", ItemDefinition.Category.Weapon, 1),
        new Spec("Item_Trident", "trident", ItemDefinition.Category.Weapon, 1),
        new Spec("Item_Pearl", "pearl", ItemDefinition.Category.Collectible, 99),
        new Spec("Item_Shell", "shell", ItemDefinition.Category.Collectible, 99),
    };

    [MenuItem("Tools/Out of the Depths/Create GDD Items")]
    public static void EnsureItemDefinitions()
    {
        if (!AssetDatabase.IsValidFolder(ItemFolder))
            AssetDatabase.CreateFolder("Assets", "Items");

        int created = 0;
        foreach (Spec spec in GddItems)
        {
            string path = $"{ItemFolder}/{spec.asset}.asset";
            if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null)
                continue;

            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);

            var so = new SerializedObject(item);
            so.FindProperty("displayName").stringValue = spec.name;
            so.FindProperty("category").enumValueIndex = (int)spec.category;
            so.FindProperty("maxStack").intValue = spec.stack;
            so.ApplyModifiedPropertiesWithoutUndo();
            created++;
        }

        AssetDatabase.SaveAssets();
        FixPickupPrefabs();
        Debug.Log($"GDD items ready in {ItemFolder} ({created} created).");
    }

    public static ItemDefinition Load(string assetName) =>
        AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemFolder}/{assetName}.asset");

    // Every item asset in the folder, by name.
    public static ItemDefinition[] LoadAll()
    {
        var list = new System.Collections.Generic.List<ItemDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { ItemFolder }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null)
                list.Add(item);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return list.ToArray();
    }

    // Pickup prefabs made before item assets existed have no Item set: default them to a bone key fragment.
    private static void FixPickupPrefabs()
    {
        ItemDefinition fallback = Load("Item_BoneKeyFragment");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { InteractablePrefabTools.PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            PickupItem pickup = prefab != null ? prefab.GetComponent<PickupItem>() : null;
            if (pickup == null)
                continue;

            var so = new SerializedObject(pickup);
            SerializedProperty item = so.FindProperty("item");
            if (item.objectReferenceValue != null)
                continue;

            item.objectReferenceValue = fallback;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("Pickup prefab item set to bone key fragment: " + path);
        }
        AssetDatabase.SaveAssets();
    }

    // Slash Attack hides its Weapon Visual until a weapon is owned; point it at the dagger placeholder under Hands if unset.
    private static void WireWeaponVisual(GameObject player)
    {
        var slash = player.GetComponentInChildren<SlashAttack>(true);
        if (slash == null)
            return;

        var so = new SerializedObject(slash);
        SerializedProperty visual = so.FindProperty("weaponVisual");
        if (visual == null || visual.objectReferenceValue != null)
            return;

        foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Dagger", System.StringComparison.OrdinalIgnoreCase))
                continue;
            visual.objectReferenceValue = t.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(player.scene);
            Debug.Log("Slash Attack weapon visual set to " + t.name);
            return;
        }
    }

    [MenuItem("Tools/Out of the Depths/Add Inventory HUD To Open Scene")]
    public static void EnsureInventoryHud()
    {
        PlayerInventory inventory = null;
        var player = Object.FindFirstObjectByType<SwimController>();
        if (player != null)
        {
            inventory = player.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = player.gameObject.AddComponent<PlayerInventory>();
            WireWeaponVisual(player.gameObject);
        }

        var existing = Object.FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            var existingSo = new SerializedObject(existing);
            SerializedProperty inventoryProp = existingSo.FindProperty("inventory");
            if (inventoryProp.objectReferenceValue == null && inventory != null)
            {
                inventoryProp.objectReferenceValue = inventory;
                existingSo.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
            return;
        }

        GameObject hud = GameObject.Find("HUD");
        Canvas canvas = hud != null ? hud.GetComponentInChildren<Canvas>() : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("No HUD canvas in the open scene, inventory HUD not added.");
            return;
        }

        var go = new GameObject("Inventory", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(400f, 80f);

        var ui = go.AddComponent<InventoryUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("inventory").objectReferenceValue = inventory;
        so.FindProperty("container").objectReferenceValue = rect;
        so.FindProperty("slotSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSpritePath);
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("Inventory HUD added under " + canvas.name);
    }
}
