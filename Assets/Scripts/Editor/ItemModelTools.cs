using System.IO;
using UnityEditor;
using UnityEngine;

// Connects 3D models from Art/Models to item assets, so pickups show real art instead of placeholder cubes.
// Tools > Out of the Depths > Assign Known Item Models: gives each item in KnownModels its model (only if it has none yet)
// and a hotbar icon rendered from that model. Also runs by itself when the project loads, so new models get picked up.
// Tools > Out of the Depths > Apply Item Models To Pickups: bakes the model into every pickup prefab so it shows in the editor,
// not just in Play mode. (Play mode does the same swap on the fly for any pickup whose prefab was not baked.)
public static class ItemModelTools
{
    public const string ModelFolder = "Assets/Art/Models";
    public const string IconFolder = "Assets/Art/Textures/ItemIcons";

    // Item asset name -> model file name (without extension) under Art/Models, plus the other looks of the same item
    // (World Model Variants: the bone key comes in three pieces). Add a line per new model.
    private static readonly (string item, string model, string[] variants)[] KnownModels =
    {
        ("Item_FirstRoomKey", "gold_key", null),
        ("Item_SymbolKey", "rune_key", null),
        ("Item_BoneKey", "bone_key_full", null),
        ("Item_BoneKeyFragment", "bone_key_piece1", new[] { "bone_key_piece2", "bone_key_piece3" }),
    };

    private const int IconRetries = 20;
    private static int iconRetriesLeft;
    private static double nextIconRetry;

    [InitializeOnLoadMethod]
    private static void AssignOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AssignKnownModels(false);

            // Thumbnails render in the background; keep trying for a while so icons appear without a second project load.
            if (MissingIcons())
            {
                iconRetriesLeft = IconRetries;
                nextIconRetry = EditorApplication.timeSinceStartup + 0.5;
                EditorApplication.update -= RetryIcons;
                EditorApplication.update += RetryIcons;
            }
        };
    }

    private static void RetryIcons()
    {
        if (EditorApplication.timeSinceStartup < nextIconRetry)
            return;

        nextIconRetry = EditorApplication.timeSinceStartup + 0.5;
        AssignKnownModels(false);

        if (!MissingIcons() || --iconRetriesLeft <= 0)
            EditorApplication.update -= RetryIcons;
    }

    private static bool MissingIcons()
    {
        foreach (var (itemName, _, _) in KnownModels)
        {
            ItemDefinition item = ItemTools.Load(itemName);
            if (item != null && item.WorldModel != null && item.Icon == null)
                return true;
        }
        return false;
    }

    [MenuItem("Tools/Out of the Depths/Assign Known Item Models")]
    public static void AssignKnownModelsMenu() => AssignKnownModels(true);

    public static void AssignKnownModels(bool verbose)
    {
        int assigned = 0;
        foreach (var (itemName, modelName, variantNames) in KnownModels)
        {
            ItemDefinition item = ItemTools.Load(itemName);
            GameObject model = FindModel(modelName);
            if (item == null || model == null)
            {
                if (verbose)
                    Debug.LogWarning($"Item model: {(item == null ? "item " + itemName : "model " + modelName)} not found.");
                continue;
            }

            var so = new SerializedObject(item);
            SerializedProperty worldModel = so.FindProperty("worldModel");
            SerializedProperty icon = so.FindProperty("icon");
            bool changed = false;

            if (worldModel.objectReferenceValue == null)
            {
                worldModel.objectReferenceValue = model;
                changed = true;
            }

            // The other looks, only while the item lists none of its own.
            SerializedProperty variants = so.FindProperty("worldModelVariants");
            if (variantNames != null && variants != null && variants.arraySize == 0)
            {
                var found = new System.Collections.Generic.List<GameObject>();
                foreach (string name in variantNames)
                {
                    GameObject variant = FindModel(name);
                    if (variant != null)
                        found.Add(variant);
                    else if (verbose)
                        Debug.LogWarning($"Item model: variant {name} of {itemName} not found.");
                }
                if (found.Count > 0)
                {
                    variants.arraySize = found.Count;
                    for (int i = 0; i < found.Count; i++)
                        variants.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
                    changed = true;
                }
            }
            if (icon.objectReferenceValue == null)
            {
                Sprite sprite = RenderIcon(model, itemName);
                if (sprite != null)
                {
                    icon.objectReferenceValue = sprite;
                    changed = true;
                }
            }

            if (!changed)
                continue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            assigned++;
            Debug.Log($"Item model: {itemName} now uses {modelName}.", item);
        }

        if (assigned > 0)
            AssetDatabase.SaveAssets();
        if (verbose)
            Debug.Log($"Known item models assigned: {assigned} changed. Bake them into prefabs with Tools > Out of the Depths > Apply Item Models To Pickups.");
    }

    [MenuItem("Tools/Out of the Depths/Apply Item Models To Pickups")]
    public static void ApplyItemModelsToPickups()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { InteractablePrefabTools.PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (BakeModel(root))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                    Debug.Log("Item model baked into " + path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Debug.Log($"Pickup prefabs updated: {changed}.");
    }

    // Same for every pickup placed in the open scene(s), so the arena shows the models in the editor, not only in Play mode.
    // The test arena builder runs this after placing its pickups.
    [MenuItem("Tools/Out of the Depths/Apply Item Models To Pickups In Open Scene")]
    public static void ApplyItemModelsInOpenScene()
    {
        int changed = 0;
        foreach (var pickup in Object.FindObjectsByType<PickupItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!pickup.gameObject.scene.IsValid() || PrefabUtility.IsPartOfPrefabAsset(pickup))
                continue;

            // A child of a prefab instance can't be deleted here, so the placeholder is hidden instead.
            bool instance = PrefabUtility.IsPartOfPrefabInstance(pickup);
            if (!BakeModel(pickup.gameObject, !instance))
                continue;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pickup.gameObject.scene);
            changed++;
        }
        Debug.Log($"Item models applied to {changed} pickup(s) in the open scene.");
    }

    // Swaps the placeholder visual of a pickup (prefab contents or scene object) for the item's model. False if nothing to do.
    private static bool BakeModel(GameObject root, bool destroyPlaceholder = true)
    {
        var pickup = root.GetComponent<PickupItem>();
        if (pickup == null)
            return false;

        var so = new SerializedObject(pickup);
        var item = so.FindProperty("item").objectReferenceValue as ItemDefinition;
        if (item == null || item.WorldModel == null)
            return false;

        var visual = so.FindProperty("visual").objectReferenceValue as Transform;
        if (visual == null && root.transform.childCount > 0)
            visual = root.transform.GetChild(0);

        // Already baked for this item (on this object or the prefab it came from): just re-fit the existing model, so a
        // changed World Model Scale / Rotation on the item shows up. Never instantiate a second copy.
        SerializedProperty bakedFor = so.FindProperty("bakedFor");
        SerializedProperty bakedVariant = so.FindProperty("bakedVariant");
        int variant = so.FindProperty("modelVariant").intValue;
        if (visual != null && bakedFor.objectReferenceValue == item && bakedVariant.intValue == variant)
        {
            PickupItem.FitItemModel(root.transform, item, visual.gameObject, null, so.FindProperty("modelSize").floatValue);
            return true;
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(item.WorldModelFor(variant), root.scene);
        Transform fitted = PickupItem.FitItemModel(root.transform, item, model, visual, so.FindProperty("modelSize").floatValue, destroyPlaceholder);

        // Record what the visual is now. Play mode leaves it alone while Item and the variant match, and swaps it if
        // an instance overrides either.
        so.FindProperty("visual").objectReferenceValue = fitted;
        bakedFor.objectReferenceValue = item;
        bakedVariant.intValue = variant;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    internal static GameObject FindModel(string fileName)
    {
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Model", new[] { ModelFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals(fileName, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    // A hotbar sprite from the editor's own thumbnail of the model. Returns null if the thumbnail is not ready yet;
    // the next project load or menu run will try again.
    internal static Sprite RenderIcon(GameObject model, string itemName)
    {
        Texture2D preview = AssetPreview.GetAssetPreview(model);
        if (preview == null)
            return null;

        if (!AssetDatabase.IsValidFolder(IconFolder))
        {
            Directory.CreateDirectory(IconFolder);
            AssetDatabase.Refresh();
        }

        string path = $"{IconFolder}/{itemName}_Icon.png";
        var rt = RenderTexture.GetTemporary(preview.width, preview.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(preview, rt);
        RenderTexture.active = rt;
        var readable = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
        readable.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        File.WriteAllBytes(path, readable.EncodeToPNG());
        Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(path);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
