using UnityEditor;
using UnityEngine;

// Tools > Out of the Depths > Make Coral Prefabs: a prefab for each coral model in Art/Models/environment/corals
// (made by Blender/make_corals.py), in Prefabs/Corals, ready to drag into a scene: the model, with a box collider round
// its bulk so the player swims round it. An existing prefab is left as it is (delete it to make it again). The ship
// builder places these when they are there.
public static class CoralPrefabTools
{
    public const string ModelFolder = "Assets/Art/Models/environment/corals";
    public const string PrefabFolder = "Assets/Prefabs/Corals";

    [MenuItem("Tools/Out of the Depths/Make Coral Prefabs")]
    public static void MakeMenu()
    {
        int made = Make();
        Debug.Log(made > 0 ? $"Coral prefabs: {made} made in {PrefabFolder}." : $"Coral prefabs: nothing new to make ({PrefabFolder} already has them, or there are no coral models in {ModelFolder}).");
    }

    public static int Make()
    {
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Corals");
        int made = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                continue;
            string target = $"{PrefabFolder}/{model.name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(target) != null)
                continue;

            var root = new GameObject(model.name);
            try
            {
                var art = (GameObject)PrefabUtility.InstantiatePrefab(model);
                art.transform.SetParent(root.transform, false);
                AddCollider(root, art);
                PrefabUtility.SaveAsPrefabAsset(root, target);
                made++;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
        if (made > 0)
            AssetDatabase.SaveAssets();
        return made;
    }

    // The prefab for a coral model, if there is one.
    public static GameObject PrefabFor(string modelName) => AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{modelName}.prefab");

    // A box round the bulk of the coral: a little narrower than its widest, the full height (a mesh collider would need
    // fewer faces than the corals have).
    private static void AddCollider(GameObject root, GameObject art)
    {
        MeshFilter shape = art.GetComponentInChildren<MeshFilter>();
        if (shape == null || shape.sharedMesh == null)
            return;
        Bounds b = shape.sharedMesh.bounds;
        Matrix4x4 m = root.transform.worldToLocalMatrix * shape.transform.localToWorldMatrix;
        var box = new Bounds(m.MultiplyPoint3x4(b.min), Vector3.zero);   // the mesh box's corners in the prefab's space (the model may be turned)
        for (int i = 0; i < 8; i++)
            box.Encapsulate(m.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        var solid = root.AddComponent<BoxCollider>();
        solid.center = box.center;
        solid.size = new Vector3(box.size.x * 0.75f, box.size.y, box.size.z * 0.75f);
    }
}
