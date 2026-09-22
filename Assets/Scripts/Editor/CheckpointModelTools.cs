using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Assembles the checkpoint from the model files in Art/Models/environment/checkpoint (Unity folds an OBJ into one mesh,
// so the parts that move are their own files: checkpoint_pillar.obj + checkpoint_book_left.obj + checkpoint_book_right.obj) onto the RespawnPlate prefab as its Visual,
// so every checkpoint in every scene shows it. The prefab root keeps the trigger collider and the Checkpoint script.
// The book (the model whose name says "book") gets Checkpoint.mat: the UV texture in the folder as base and emission
// map, emission on, so the glow shows through the paint; the pillar gets the plain glow material, RespawnPlate.mat.
// A point light, the sparkle burst and the hovering book are wired up.
// Tools > Out of the Depths > Use Checkpoint Model. Both scene builders run it too. Safe to run again.
public static class CheckpointModelTools
{
    public const string ModelFolder = "Assets/Art/Models/environment/checkpoint";
    private const string PrefabPath = InteractablePrefabTools.PrefabRoot + "/Placeholders/RespawnPlate.prefab";
    private const string GlowMaterialPath = "Assets/Art/Materials/RespawnPlate.mat";
    private const string ModelMaterialPath = "Assets/Art/Materials/Checkpoint.mat";
    private const string VisualName = "Visual";
    private const string LightName = "Glow";
    private const float VisualScale = 0.7f;   // the model is about 2 x 5.4 m; this brings it to about 1.4 x 3.8

    // The part of the model that floats: the first object under the Visual whose name contains one of these.
    private static readonly string[] HoverNames = { "book", "plane" };

    [MenuItem("Tools/Out of the Depths/Use Checkpoint Model")]
    public static void Apply()
    {
        List<(GameObject model, string path)> models = FindModels();
        if (models.Count == 0)
        {
            Debug.LogWarning($"Checkpoint model: no model file in {ModelFolder}, the RespawnPlate prefab is unchanged.");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogWarning($"Checkpoint model: no prefab at {PrefabPath}.");
            return;
        }

        Material bookMaterial = EnsureMaterial(out Material pillarMaterial);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            bool changed = EnsureVisual(root, models);
            changed |= EnsureMaterials(root, bookMaterial, pillarMaterial);
            changed |= FitCollider(root);
            changed |= EnsureLight(root);
            changed |= WireCheckpoint(root);
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                var names = new List<string>();
                foreach (var (_, path) in models)
                    names.Add(System.IO.Path.GetFileName(path));
                Debug.Log($"Checkpoint model: RespawnPlate now shows {string.Join(" + ", names)} at {VisualScale:0.00} scale, the book in {bookMaterial.name}, with its light, sparkles and hovering book wired.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Checkpoint.mat for the book: URP Lit with the folder's texture as base and emission map, emission on. The pillar
    // parts use the plain glow material (also returned); the book falls back to it when the folder has no texture.
    private static Material EnsureMaterial(out Material glowMaterial)
    {
        Texture2D texture = null;
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ModelFolder }))
        {
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (texture != null)
                break;
        }

        var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (glow != null && !glow.IsKeywordEnabled("_EMISSION"))
        {
            glow.EnableKeyword("_EMISSION");
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(glow);
        }
        glowMaterial = glow;
        if (texture == null)
            return glow;

        var material = AssetDatabase.LoadAssetAtPath<Material>(ModelMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, ModelMaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.25f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_EmissionColor", new Color(0.05f, 0.25f, 0.25f));   // the Checkpoint script drives this per pillar
        EditorUtility.SetDirty(material);
        return material;
    }

    // Every model file as a child of the Visual, standing on the floor. Rebuilt only when the set of files or the scale changed.
    private static bool EnsureVisual(GameObject root, List<(GameObject model, string path)> models)
    {
        Transform old = root.transform.Find(VisualName);
        if (old != null && root.GetComponent<MeshFilter>() == null && Mathf.Approximately(old.localScale.x, VisualScale) && UsesExactly(old, models))
            return false;

        // The first placeholder was the root itself: a squashed cylinder. Take its mesh away and give it back its
        // natural scale; instances keep their own position and rotation.
        foreach (var filter in root.GetComponents<MeshFilter>())
            Object.DestroyImmediate(filter);
        foreach (var renderer in root.GetComponents<MeshRenderer>())
            Object.DestroyImmediate(renderer);
        root.transform.localScale = Vector3.one;
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var visual = new GameObject(VisualName);
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * VisualScale;
        Transform book = null;
        foreach (var (model, path) in models)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            Transform parent = visual.transform;
            if (fileName.ToLowerInvariant().Contains("book"))
            {
                // The book's files (its two halves) go under one Book object: that is what hovers and flies.
                if (book == null)
                {
                    book = new GameObject("Book").transform;
                    book.SetParent(visual.transform, false);
                }
                parent = book;
            }
            var part = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            part.name = fileName;
            part.transform.localPosition = Vector3.zero;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = Vector3.one;
        }

        // Stand it on the floor: the root sits at floor level, so lift the model by however far its lowest point is below the origin.
        Bounds bounds = ModelBounds(visual);
        visual.transform.localPosition = new Vector3(0f, -bounds.min.y, 0f);
        return true;
    }

    // The book in its textured material, everything else in the glow material.
    private static bool EnsureMaterials(GameObject root, Material bookMaterial, Material pillarMaterial)
    {
        Transform visual = root.transform.Find(VisualName);
        if (visual == null)
            return false;
        Transform book = FindHoverPart(visual);
        bool changed = false;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
        {
            bool isBook = book != null && (renderer.transform == book || renderer.transform.IsChildOf(book));
            Material wanted = isBook ? bookMaterial : pillarMaterial;
            if (wanted == null)
                continue;
            var materials = renderer.sharedMaterials;
            bool same = true;
            for (int i = 0; i < materials.Length; i++)
                same &= materials[i] == wanted;
            if (same)
                continue;
            for (int i = 0; i < materials.Length; i++)
                materials[i] = wanted;
            renderer.sharedMaterials = materials;
            changed = true;
        }
        return changed;
    }

    // The trigger covers the whole model so the E prompt finds it from any side.
    private static bool FitCollider(GameObject root)
    {
        Transform visual = root.transform.Find(VisualName);
        if (visual == null)
            return false;
        Bounds bounds = ModelBounds(visual.gameObject);
        Vector3 size = bounds.size + new Vector3(0.2f, 0.1f, 0.2f);
        var collider = root.GetComponent<BoxCollider>();
        if (collider != null && collider.isTrigger && (collider.center - bounds.center).sqrMagnitude < 0.0001f && (collider.size - size).sqrMagnitude < 0.0001f)
            return false;
        if (collider == null)
            collider = root.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.center = bounds.center;
        collider.size = size;
        return true;
    }

    // A point light in the middle of the pillar; the Checkpoint script drives its intensity and sweeps it on activation.
    private static bool EnsureLight(GameObject root)
    {
        if (root.transform.Find(LightName) != null)
            return false;
        Transform visual = root.transform.Find(VisualName);
        float height = visual != null ? ModelBounds(visual.gameObject).center.y : 1f;

        var go = new GameObject(LightName);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = new Vector3(0f, height, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.35f, 1f, 1f);
        light.range = 6f;
        light.intensity = 0.2f;
        light.shadows = LightShadows.None;
        return true;
    }

    private static bool WireCheckpoint(GameObject root)
    {
        var checkpoint = root.GetComponent<Checkpoint>();
        if (checkpoint == null)
            return false;
        Transform visual = root.transform.Find(VisualName);
        var so = new SerializedObject(checkpoint);
        bool changed = false;
        changed |= Set(so, "visual", visual);
        changed |= Set(so, "glowLight", root.transform.Find(LightName)?.GetComponent<Light>());
        changed |= Set(so, "activateVfx", InteractablePrefabTools.FindSparkle());
        Transform book = FindHoverPart(visual);
        changed |= Set(so, "hover", book);
        changed |= MeasureBook(book, so);
        if (changed)
            so.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    // The book: the first object under the Visual whose name says so (the checkpoint_book model, or a part named Plane).
    private static Transform FindHoverPart(Transform visual)
    {
        if (visual == null)
            return null;
        foreach (string key in HoverNames)
            foreach (Transform part in visual.GetComponentsInChildren<Transform>())
                if (part != visual && part.name.ToLowerInvariant().Contains(key))
                    return part;
        return null;
    }

    // Measures the imported book halves (BookHinge: the spine, the closing turn and its direction, the facing) and
    // writes the numbers onto the Checkpoint. The Checkpoint measures again at start; these are what the Inspector
    // shows and the fallback when it cannot.
    private static bool MeasureBook(Transform book, SerializedObject so)
    {
        if (book == null)
            return false;
        if (!BookHinge.Measure(book, out BookHinge hinge, out string problem))
        {
            Debug.LogWarning("Checkpoint model: the book cannot be hinged: " + problem + ".");
            return false;
        }
        bool changed = false;
        changed |= SetVector(so, "bookSpineBottom", hinge.spineBottom);
        changed |= SetVector(so, "bookSpineTop", hinge.spineTop);
        changed |= SetFloat(so, "bookCloseAngle", hinge.closeAngle);
        changed |= SetBool(so, "bookCloseFlip", hinge.flip);
        changed |= SetFloat(so, "faceYawOffset", hinge.faceYawOffset);
        if (changed)
            Debug.Log("Checkpoint model: book measured - " + hinge + ".");
        return changed;
    }

    private static bool SetVector(SerializedObject so, string field, Vector3 value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || (prop.vector3Value - value).sqrMagnitude < 1e-6f)
            return false;
        prop.vector3Value = value;
        return true;
    }

    private static bool SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || Mathf.Abs(prop.floatValue - value) < 0.01f)
            return false;
        prop.floatValue = value;
        return true;
    }

    private static bool SetBool(SerializedObject so, string field, bool value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || prop.boolValue == value)
            return false;
        prop.boolValue = value;
        return true;
    }

    private static bool Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || value == null || prop.objectReferenceValue == value)
            return false;
        prop.objectReferenceValue = value;
        return true;
    }

    // Does the Visual hold exactly these model files: every one of them, and nothing from a file that is gone?
    private static bool UsesExactly(Transform visual, List<(GameObject model, string path)> models)
    {
        var used = new HashSet<string>();
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
            if (filter.sharedMesh != null)
                used.Add(AssetDatabase.GetAssetPath(filter.sharedMesh));
        var wanted = new HashSet<string>();
        foreach (var (_, path) in models)
            wanted.Add(path);
        return used.SetEquals(wanted);
    }

    // Every model file in the folder, by name, imported if Unity has not seen them yet.
    private static List<(GameObject model, string path)> FindModels()
    {
        var result = new List<(GameObject, string)>();
        if (!AssetDatabase.IsValidFolder(ModelFolder))
        {
            AssetDatabase.Refresh();
            if (!AssetDatabase.IsValidFolder(ModelFolder))
                return result;
        }

        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        if (paths.Count == 0)
        {
            // Files copied in while Unity was not looking: import them now and look again.
            foreach (string file in System.IO.Directory.GetFiles(ModelFolder))
                if (file.EndsWith(".obj") || file.EndsWith(".fbx"))
                    AssetDatabase.ImportAsset(file.Replace('\\', '/'));
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        paths.Sort(System.StringComparer.OrdinalIgnoreCase);

        // The book halves are measured at start (BookHinge), which in a build needs their meshes readable.
        foreach (string path in paths)
        {
            if (!System.IO.Path.GetFileName(path).ToLowerInvariant().Contains("book"))
                continue;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
        }
        foreach (string path in paths)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model != null)
                result.Add((model, path));
        }
        return result;
    }

    // Renderer bounds are world space; the prefab root sits at the origin while editing, so they double as local bounds.
    private static Bounds ModelBounds(GameObject visual)
    {
        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(visual.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }
}
