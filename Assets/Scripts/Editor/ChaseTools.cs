using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Chase-sequence helpers: the ChasePufferfish_Placeholder prefab and the red danger pulse on the HUD.
// Tools > Out of the Depths > Add Chase Danger HUD To Open Scene. The test arena builder runs both.
public static class ChaseTools
{
    public const string PursuerPrefabPath = InteractablePrefabTools.PrefabRoot + "/Placeholders/ChasePufferfish_Placeholder.prefab";
    private const string SfxFolder = "Assets/Sound/SFX Sound effects/";
    private const string FishMaterialPath = "Assets/Art/Materials/Fish.mat";

    // Root = trigger sphere + Chase Pufferfish + Renderer Tint; child Visual = a dark red ball with spikes.
    // Swap the Visual for real art, keep the root.
    public static GameObject EnsurePursuerPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PursuerPrefabPath);
        if (existing != null)
            return existing;

        var root = new GameObject("ChasePufferfish_Placeholder");
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.55f;
        var fish = root.AddComponent<ChasePufferfish>();
        root.AddComponent<RendererTint>().Tint = new Color(0.55f, 0.1f, 0.08f);

        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FishMaterialPath);

        GameObject body = Primitive(PrimitiveType.Sphere, "Body", visual.transform, Vector3.zero, new Vector3(1.1f, 1f, 1.1f), material);
        body.transform.localRotation = Quaternion.identity;

        // Spikes all round, pointing outward.
        Vector3[] directions =
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.back,
            new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(-1f, -1f, 0f),
            new Vector3(0f, 1f, -1f), new Vector3(0f, -1f, -1f), new Vector3(1f, 0f, -1f), new Vector3(-1f, 0f, -1f),
        };
        foreach (Vector3 raw in directions)
        {
            Vector3 direction = raw.normalized;
            GameObject spike = Primitive(PrimitiveType.Cube, "Spike", visual.transform, direction * 0.55f, new Vector3(0.12f, 0.12f, 0.45f), material);
            spike.transform.localRotation = Quaternion.LookRotation(direction, Mathf.Abs(direction.y) > 0.9f ? Vector3.forward : Vector3.up);
        }

        var so = new SerializedObject(fish);
        so.FindProperty("biteSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + "Enemy fish Attack sound effect.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PursuerPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("Created " + PursuerPrefabPath);
        return prefab;
    }

    private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        if (material != null)
            go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    // A stretched "ChaseDanger" rect behind everything else on the HUD canvas, with the Chase Danger UI on it.
    [MenuItem("Tools/Out of the Depths/Add Chase Danger HUD To Open Scene")]
    public static void EnsureDangerHud()
    {
        if (Object.FindFirstObjectByType<ChaseDangerUI>(FindObjectsInactive.Include) != null)
            return;

        GameObject hud = GameObject.Find("HUD");
        Canvas canvas = hud != null ? hud.GetComponentInChildren<Canvas>() : Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("No HUD canvas in the open scene, chase danger HUD not added.");
            return;
        }

        var go = new GameObject("ChaseDanger", typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
        go.AddComponent<ChaseDangerUI>();

        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("Chase danger HUD added under " + canvas.name);
    }
}
