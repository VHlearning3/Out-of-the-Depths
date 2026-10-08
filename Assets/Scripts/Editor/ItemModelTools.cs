using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Connects 3D models from Art/Models to item assets, so pickups show real art instead of placeholder cubes.
// Tools > Out of the Depths > Assign Known Item Models: gives each item in KnownModels its model (only if it has none yet)
// and a hotbar icon rendered from that model. Also runs by itself when the project loads, so new models get picked up.
// Tools > Out of the Depths > Apply Item Models To Pickups: bakes the model into every pickup prefab so it shows in the editor,
// not just in Play mode. (Play mode does the same swap on the fly for any pickup whose prefab was not baked.)
// A model re-exported at other units leaves the pickups already baked into scenes far off their size (the dagger came
// back from Blender in metres instead of centimetres: a hundred times bigger, a giant standing in the room). Those are
// fitted again by themselves in every open scene when scripts reload and in each scene as it is opened (save it to keep it).
public static class ItemModelTools
{
    public const string ModelFolder = "Assets/Art/Models";
    public const string IconFolder = "Assets/Art/Textures/ItemIcons";

    // Item asset name -> model file name (without extension) under Art/Models, plus the other looks of the same item
    // (World Model Variants: the bone key comes in three pieces). Add a line per new model. "File/Part" is one part of
    // a model holding several (the stone fragments are the three parts of Fragment_Pieces): it is made a prefab of
    // its own (Prefabs/Items/<Part>), so a pickup shows just that piece.
    private static readonly (string item, string model, string[] variants)[] KnownModels =
    {
        ("Item_FirstRoomKey", "gold_key", null),
        ("Item_SymbolKey", "rune_key", null),
        ("Item_BoneKey", "bone_key_full", null),
        ("Item_BoneKeyFragment", "bone_key_piece1", new[] { "bone_key_piece2", "bone_key_piece3" }),
        ("Item_StoneFragment", "Fragment_Pieces/Fragment_1", new[] { "Fragment_Pieces/Fragment_2", "Fragment_Pieces/Fragment_3" }),
        ("Item_Dagger", "dagger", null),     // Vili's weapons (Art/Models/weapons), tip up (+Y)
        ("Item_Trident", "trident", null),
    };

    // How the weapons sit: in a pickup (size on top of the pickup's 0.35 m fit) and in your hand (length, grip), applied
    // once per Weapon Fit Version (the tip direction is read from the mesh).
    // Flip Tip: the end the mesh test picks is the wrong one (the trident's side prongs make its top wider than its butt).
    private static readonly (string item, float pickupScale, float heldLength, float heldGrip, Vector3 heldRotation, bool flipTip)[] WeaponFits =
    {
        ("Item_Dagger", 0.9f, 0.3f, 0.2f, new Vector3(-8f, -6f, 0f), false),
        ("Item_Trident", 3.2f, 1.5f, 0.35f, new Vector3(-4f, -4f, 0f), true),
    };
    private const int WeaponFitVersion = 3;

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

    // ---- baked pickups whose model was re-exported at another size -------------------------------------------------

    [InitializeOnLoadMethod]
    private static void RefitStaleOnLoad()
    {
        EditorSceneManager.sceneOpened -= RefitStaleInOpenedScene;
        EditorSceneManager.sceneOpened += RefitStaleInOpenedScene;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
                RefitStale(SceneManager.GetSceneAt(i));
        };
    }

    private static void RefitStaleInOpenedScene(Scene scene, OpenSceneMode mode)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            RefitStale(scene);
    }

    // Every pickup in `scene` baked for its item whose model is now over 1.5 times or under 2/3 of the size it should be
    // (Model Size times the item's World Model Scale; the same test Play mode makes) is fitted again like the bake tool
    // does: the item's rotation, Model Size, centred. Returns how many.
    public static int RefitStale(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return 0;
        int refitted = 0;
        foreach (GameObject top in scene.GetRootGameObjects())
            foreach (PickupItem pickup in top.GetComponentsInChildren<PickupItem>(true))
            {
                var so = new SerializedObject(pickup);
                var item = so.FindProperty("item").objectReferenceValue as ItemDefinition;
                var visual = so.FindProperty("visual").objectReferenceValue as Transform;
                float size = so.FindProperty("modelSize").floatValue;
                if (!so.FindProperty("useItemModel").boolValue || item == null || item.WorldModel == null || visual == null
                    || size <= 0f || so.FindProperty("bakedFor").objectReferenceValue != item || !visual.gameObject.activeInHierarchy)
                    continue;   // not baked, or hidden (a hidden renderer has no size to go by)
                float longest = LongestSide(visual);
                float expected = size * item.WorldModelScale * Mathf.Abs(pickup.transform.lossyScale.x);
                if (longest < 1e-4f || expected < 1e-4f)
                    continue;
                float ratio = longest / expected;
                if (ratio <= 1.5f && ratio >= 0.67f)
                    continue;
                Undo.RecordObject(visual, "Refit item model");
                PickupItem.FitItemModel(pickup.transform, item, visual.gameObject, null, size);
                EditorUtility.SetDirty(visual);
                EditorSceneManager.MarkSceneDirty(scene);
                refitted++;
                Debug.Log($"Item model: {pickup.name} in {scene.name} showed {item.name} at {ratio:0.##} times its size (its model was re-exported at other units); fitted again. Save the scene to keep it.", pickup);
            }
        return refitted;
    }

    private static float LongestSide(Transform visual)
    {
        bool any = false;
        var bounds = new Bounds();
        foreach (Renderer r in visual.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || !r.enabled)
                continue;
            if (any)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            any = true;
        }
        return any ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : 0f;
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

            // A weapon: how big it shows in a pickup and how it sits in your hand, once per fit version.
            SerializedProperty fitVersion = so.FindProperty("heldFitVersion");
            if (fitVersion != null && fitVersion.intValue < WeaponFitVersion && worldModel.objectReferenceValue is GameObject weaponModel)
            {
                foreach (var fit in WeaponFits)
                {
                    if (fit.item != itemName)
                        continue;
                    so.FindProperty("worldModelScale").floatValue = fit.pickupScale;
                    so.FindProperty("heldLength").floatValue = fit.heldLength;
                    so.FindProperty("heldGrip").floatValue = fit.heldGrip;
                    so.FindProperty("heldRotation").vector3Value = fit.heldRotation;
                    so.FindProperty("heldTipAxis").vector3Value = TipAxis(weaponModel) * (fit.flipTip ? -1f : 1f);
                    fitVersion.intValue = WeaponFitVersion;
                    changed = true;
                }
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

    // Which way a long model points, in its own space: along its longest side, towards the end that narrows to a
    // point (a blade's tip, the trident's middle prong) rather than the blunt one (a pommel, the butt of the shaft).
    internal static Vector3 TipAxis(GameObject model)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            foreach (Vector3 v in filter.sharedMesh.vertices)
                points.Add(model.transform.InverseTransformPoint(filter.transform.TransformPoint(v)));
        }
        if (points.Count == 0)
            return Vector3.up;

        var bounds = new Bounds(points[0], Vector3.zero);
        foreach (Vector3 p in points)
            bounds.Encapsulate(p);
        int axis = bounds.size.x >= bounds.size.y && bounds.size.x >= bounds.size.z ? 0 : bounds.size.y >= bounds.size.z ? 1 : 2;
        float min = bounds.min[axis], max = bounds.max[axis];
        float slice = (max - min) * 0.06f;
        float Spread(bool top)
        {
            var end = new Bounds();
            bool any = false;
            foreach (Vector3 p in points)
            {
                if (top ? p[axis] < max - slice : p[axis] > min + slice)
                    continue;
                Vector3 flat = p;
                flat[axis] = 0f;
                if (!any)
                    end = new Bounds(flat, Vector3.zero);
                else
                    end.Encapsulate(flat);
                any = true;
            }
            return any ? end.size.magnitude : 0f;
        }
        var direction = Vector3.zero;
        direction[axis] = Spread(true) <= Spread(false) ? 1f : -1f;
        return direction;
    }

    internal static GameObject FindModel(string fileName)
    {
        int slash = fileName.IndexOf('/');
        if (slash > 0)
            return PartModel(fileName.Substring(0, slash), fileName.Substring(slash + 1));
        foreach (string guid in AssetDatabase.FindAssets(fileName + " t:Model", new[] { ModelFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals(fileName, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    public const string PartFolder = "Assets/Prefabs/Items";

    // One part of a model holding several, as a prefab of its own (made once, in Prefabs/Items): the part as the
    // model has it (its turn, its size, its material), moved to the middle. Null if the model or the part is missing.
    private static GameObject PartModel(string fileName, string partName)
    {
        string path = $"{PartFolder}/{partName}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;
        GameObject model = FindModel(fileName);
        if (model == null)
            return null;
        var copy = (GameObject)Object.Instantiate(model);
        try
        {
            Transform part = null;
            foreach (Transform t in copy.GetComponentsInChildren<Transform>(true))
                if (t.name == partName)
                {
                    part = t;
                    break;
                }
            if (part == null)
                return null;
            part.SetParent(null, true);   // keeps how it is turned and sized in the model
            part.position = Vector3.zero;
            for (int i = part.childCount - 1; i >= 0; i--)
                if (part.GetChild(i).GetComponentInChildren<Renderer>() == null)
                    Object.DestroyImmediate(part.GetChild(i).gameObject);
            if (!AssetDatabase.IsValidFolder(PartFolder))
                AssetDatabase.CreateFolder("Assets/Prefabs", "Items");
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(part.gameObject, path);
            Object.DestroyImmediate(part.gameObject);
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(copy);
        }
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
