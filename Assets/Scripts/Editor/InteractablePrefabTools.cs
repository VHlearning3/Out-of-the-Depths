using UnityEditor;
using UnityEngine;

// Tools > Out of the Depths > Add Indicators To Interactable Prefabs: every prefab under Assets/Prefabs whose root is an
// IInteractable (live fish, dead fish, pickups) gets Interactable Highlight + Interactable Indicator wired to the sparkle prefab.
// Checkpoints are skipped, they have their own glow. The test arena builder runs this first.
public static class InteractablePrefabTools
{
    public const string PrefabRoot = "Assets/Prefabs";

    [MenuItem("Tools/Out of the Depths/Add Indicators To Interactable Prefabs")]
    public static void AddIndicatorsToInteractablePrefabs()
    {
        ParticleSystem sparkle = FindSparkle();
        if (sparkle == null)
            Debug.LogWarning("No sparkle prefab found under " + PrefabRoot + " (looked for PickupSparkle / Particle_Placeholder). Indicators are added without particles.");

        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool dirty = false;

            var interactable = root.GetComponent<IInteractable>();
            if (interactable != null && !(interactable is Checkpoint))
            {
                if (root.GetComponent<InteractableHighlight>() == null)
                {
                    root.AddComponent<InteractableHighlight>();
                    dirty = true;
                }

                var indicator = root.GetComponent<InteractableIndicator>();
                if (indicator == null)
                {
                    indicator = root.AddComponent<InteractableIndicator>();
                    dirty = true;
                }

                if (sparkle != null)
                {
                    var so = new SerializedObject(indicator);
                    SerializedProperty effect = so.FindProperty("effectPrefab");
                    if (effect.objectReferenceValue == null)
                    {
                        effect.objectReferenceValue = sparkle;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                    }
                }

                dirty |= ApplyStyle(root.GetComponent<InteractableHighlight>(), indicator);
            }

            if (dirty)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
                Debug.Log("Indicators added: " + path);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log($"Interactable prefabs updated: {changed}");
    }

    // One look for every interactable: quiet sparkle, soft highlight. Re-applied on each run so old prefabs pick up the current style.
    private static bool ApplyStyle(InteractableHighlight highlight, InteractableIndicator indicator)
    {
        bool changed = false;
        if (highlight != null)
        {
            var so = new SerializedObject(highlight);
            changed |= SetFloat(so, "tintStrength", 0.2f);
            changed |= SetFloat(so, "brightness", 0.35f);
            changed |= SetFloat(so, "pulseSpeed", 1.2f);
            changed |= SetFloat(so, "pulseAmount", 0.2f);
            SerializedProperty emission = so.FindProperty("emission");
            if (emission != null && emission.colorValue != new Color(1.2f, 1f, 0.6f))
            {
                emission.colorValue = new Color(1.2f, 1f, 0.6f);
                changed = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (indicator != null)
        {
            var so = new SerializedObject(indicator);
            changed |= SetFloat(so, "targetedRateMultiplier", 3f);
            SerializedProperty burst = so.FindProperty("targetedBurst");
            if (burst != null && burst.intValue != 6)
            {
                burst.intValue = 6;
                changed = true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return changed;
    }

    private static bool SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null || Mathf.Approximately(prop.floatValue, value))
            return false;
        prop.floatValue = value;
        return true;
    }

    // Exact file name first, otherwise the first prefab whose name starts with it (Pickup_Placeholder 1, Hazard_Placeholder...).
    public static GameObject FindPrefab(string name)
    {
        GameObject partial = null;
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Prefab", new[] { PrefabRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (partial == null && file.StartsWith(name, System.StringComparison.OrdinalIgnoreCase))
                partial = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return partial;
    }

    public static ParticleSystem FindSparkle()
    {
        foreach (string name in new[] { "PickupSparkle", "Particle_Placeholder", "Sparkle" })
        {
            GameObject prefab = FindPrefab(name);
            if (prefab != null && prefab.TryGetComponent(out ParticleSystem system))
                return system;
        }
        return null;
    }
}
