using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// The team's fish model on the fish prefabs (Prefabs/Placeholders): Art/Models/fish, Normal_fish_* (one rig and mesh,
// one animation in each file: swim, scatter, hit, hit and death, dead idle). Following HOW TO ADD ART (replace the
// visual, keep the object with the scripts), each prefab keeps its root, scripts and collider, and its look becomes a
// Visual child holding the model ("Model"), nose along +Z, centred and fitted to the length the placeholder had:
//   Fish_Wanderer  (and so every school, spawner and roof-hole fish): swims, scatters, takes hits, dies, 1 m long;
//   WallFish       (the walls of fish): the same, bigger to fill its 1 m box collider (the collider stays, so the wall
//                  still blocks): 1.25 m long and a little deeper-bodied than modelled, 0.7 m wide and 0.7 m tall;
//   DeadFish       (the dead fish lying about to eat): dead from the start, belly-up and bobbing, 0.8 m long.
// The model gets an Animator with NormalFish.controller and the root a Fish Animator that plays it from what the fish
// does (Swim / Scatter / Hit / Dying / DeadIdle, made here the first time; edit it freely after, it is kept), and the
// material NormalFish.mat (a copy of the model's own: URP Lit with fish_texture; made here if missing, kept after).
// Fish Controller's Visual points at the new Visual (it shrinks it away when a dead fish fades). Safe to run again: it
// redoes the look of the three prefabs and nothing else.
public static class FishModelTools
{
    private const string Folder = "Assets/Art/Models/fish/";
    public const string ModelPath = Folder + "Normal_fish_swim_Fix1.fbx";
    public const string ControllerPath = Folder + "NormalFish.controller";
    public const string MaterialPath = Folder + "NormalFish.mat";
    private const string PrefabFolder = "Assets/Prefabs/Placeholders/";

    private const string SwimClip = "Normal_fish_swim_Fix1";
    private const string ScatterClip = "Normal_fish_scatter_fix";
    private const string HitClip = "Normal_fish_Hit_fix";
    private const string DyingClip = "Normal_fish_Hit_and_Death_Fix";
    private const string DeadIdleClip = "Normal_fish_Death_Idle_Fix";

    [MenuItem("Tools/Out of the Depths/Use Normal Fish Model On Fish Prefabs")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Normal fish: leave Play mode first.");
            return;
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            Debug.LogWarning($"Normal fish: no model at {ModelPath}.");
            return;
        }
        Material material = FishMaterial(model);
        AnimatorController controller = Controller();
        if (controller == null)
            return;

        int done = 0;
        done += Dress("Fish_Wanderer", new Vector3(0f, 0f, 1f), false, model, material, controller) ? 1 : 0;
        done += Dress("WallFish", WallFishSize, false, model, material, controller) ? 1 : 0;
        done += Dress("DeadFish", new Vector3(0f, 0f, 0.8f), true, model, material, controller) ? 1 : 0;
        AssetDatabase.SaveAssets();
        Debug.Log($"Normal fish: {done} fish prefabs now wear {ModelPath} with {ControllerPath}.");
    }

    // The wall fish's model, in metres: width, height, length (its box collider is 1 x 1 x 1).
    public static readonly Vector3 WallFishSize = new Vector3(0.7f, 0.7f, 1.25f);

    // One prefab: the old look out, the model in, the scripts pointed at it. `size`: width, height and length in metres;
    // a width or height of 0 keeps the model's own proportions.
    private static bool Dress(string prefabName, Vector3 size, bool dead, GameObject model, Material material, AnimatorController controller)
    {
        string path = PrefabFolder + prefabName + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning($"Normal fish: no prefab at {path}.");
            return false;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // The old look: a Visual child (Fish_Wanderer), or a mesh on the root itself (WallFish, DeadFish).
            Transform old = root.transform.Find("Visual");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var rootRenderer = root.GetComponent<MeshRenderer>();
            if (rootRenderer != null)
                Object.DestroyImmediate(rootRenderer);
            var rootFilter = root.GetComponent<MeshFilter>();
            if (rootFilter != null)
                Object.DestroyImmediate(rootFilter);

            // A root squashed into a fish shape (DeadFish: 0.5 x 0.3 x 0.8) goes back to 1, its collider keeping the size it had.
            Vector3 scale = root.transform.localScale;
            if (scale != Vector3.one)
            {
                float most = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
                var sphere = root.GetComponent<SphereCollider>();
                if (sphere != null)
                {
                    sphere.radius *= most;
                    sphere.center = Vector3.Scale(sphere.center, scale);
                }
                var box = root.GetComponent<BoxCollider>();
                if (box != null)
                {
                    box.size = Vector3.Scale(box.size, scale);
                    box.center = Vector3.Scale(box.center, scale);
                }
                root.transform.localScale = Vector3.one;
            }

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
            instance.name = "Model";
            Fit(instance.transform, size);

            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (SkinnedMeshRenderer skinned in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skinned.sharedMaterial = material;
                skinned.updateWhenOffscreen = false;
            }

            var fishController = root.GetComponent<FishController>();
            if (fishController != null)
                Set(fishController, "visual", p => p.objectReferenceValue = visual);

            var fishAnimator = root.GetComponent<FishAnimator>();
            if (fishAnimator == null)
                fishAnimator = root.AddComponent<FishAnimator>();
            Set(fishAnimator, "animator", p => p.objectReferenceValue = animator);
            Set(fishAnimator, "startDead", p => p.boolValue = dead);

            // The textured model shows its own colours: no tint on top (the testing colours skip it too).
            var tint = root.GetComponent<RendererTint>();
            if (tint != null)
                Set(tint, "tint", p => p.colorValue = Color.white);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Nose along +Z as modelled; scaled so it is size.z metres nose to tail (and size.x wide, size.y tall where those are
    // not 0, else in proportion) and moved so its middle is the Visual's.
    private static void Fit(Transform model, Vector3 size)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        Transform parent = model.parent;
        Bounds bounds = default;
        bool any = false;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Bounds b = r.bounds;
            // In the parent's space (the Visual sits at the root's origin, unrotated and unscaled at this point).
            Vector3 centre = parent.InverseTransformPoint(b.center);
            Vector3 extent = parent.InverseTransformVector(b.size);
            var local = new Bounds(centre, new Vector3(Mathf.Abs(extent.x), Mathf.Abs(extent.y), Mathf.Abs(extent.z)));
            if (any)
                bounds.Encapsulate(local);
            else
                bounds = local;
            any = true;
        }
        if (!any || bounds.size.z < 1e-4f)
            return;
        float k = size.z / bounds.size.z;
        var scale = new Vector3(
            size.x > 0f && bounds.size.x > 1e-4f ? size.x / bounds.size.x : k,
            size.y > 0f && bounds.size.y > 1e-4f ? size.y / bounds.size.y : k,
            k);
        model.localScale = scale;
        model.localPosition = -Vector3.Scale(bounds.center, scale);
    }

    // URP Lit with fish_texture: a copy of the model's own material, so it can be tuned without touching the import.
    private static Material FishMaterial(GameObject model)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
            return material;
        Renderer source = model.GetComponentInChildren<Renderer>(true);
        if (source != null && source.sharedMaterial != null)
            material = new Material(source.sharedMaterial) { name = "NormalFish" };
        else
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "NormalFish" };
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", Mathf.Min(material.GetFloat("_Smoothness"), 0.35f));   // wet, not plastic
        AssetDatabase.CreateAsset(material, MaterialPath);
        return material;
    }

    // Swim (the default, its speed from SwimSpeed), Scatter and Hit (each back to Swim when done), and from anywhere
    // Dying on Die, then DeadIdle. Fish Animator jumps straight back to Swim (or DeadIdle) on a respawn.
    public static AnimatorController Controller()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (existing != null)
            return existing;

        AnimationClip swim = Clip(SwimClip), scatter = Clip(ScatterClip), hit = Clip(HitClip), dying = Clip(DyingClip), deadIdle = Clip(DeadIdleClip);
        if (swim == null || scatter == null || hit == null || dying == null || deadIdle == null)
        {
            Debug.LogWarning($"Normal fish: an animation is missing from {Folder} (swim, scatter, hit, hit and death, death idle).");
            return null;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Scatter", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter(new AnimatorControllerParameter { name = "SwimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState swimState = machine.AddState("Swim", new Vector3(300f, 0f));
        swimState.motion = swim;
        swimState.speedParameterActive = true;
        swimState.speedParameter = "SwimSpeed";
        AnimatorState scatterState = machine.AddState("Scatter", new Vector3(300f, -120f));
        scatterState.motion = scatter;
        AnimatorState hitState = machine.AddState("Hit", new Vector3(560f, 0f));
        hitState.motion = hit;
        AnimatorState dyingState = machine.AddState("Dying", new Vector3(300f, 160f));
        dyingState.motion = dying;
        AnimatorState deadState = machine.AddState("DeadIdle", new Vector3(560f, 160f));
        deadState.motion = deadIdle;
        machine.defaultState = swimState;

        On(swimState, scatterState, "Scatter", 0.12f);
        On(swimState, hitState, "Hit", 0.06f);
        On(scatterState, hitState, "Hit", 0.06f);
        On(hitState, hitState, "Hit", 0.04f);   // hit again mid-flinch: flinch again
        WhenDone(scatterState, swimState, 0.2f);
        WhenDone(hitState, swimState, 0.15f);
        WhenDone(dyingState, deadState, 0.15f);

        AnimatorStateTransition die = machine.AddAnyStateTransition(dyingState);
        die.AddCondition(AnimatorConditionMode.If, 0f, "Die");
        die.hasExitTime = false;
        die.hasFixedDuration = true;
        die.duration = 0.08f;
        die.canTransitionToSelf = false;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void On(AnimatorState from, AnimatorState to, string trigger, float seconds)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        t.hasExitTime = false;
        t.hasFixedDuration = true;
        t.duration = seconds;
    }

    private static void WhenDone(AnimatorState from, AnimatorState to, float seconds)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = 0.95f;
        t.hasFixedDuration = true;
        t.duration = seconds;
    }

    private static AnimationClip Clip(string file)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(Folder + file + ".fbx"))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                return clip;
        return null;
    }

    private static void Set(Object target, string property, System.Action<SerializedProperty> apply)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(property);
        if (p == null)
            return;
        apply(p);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
