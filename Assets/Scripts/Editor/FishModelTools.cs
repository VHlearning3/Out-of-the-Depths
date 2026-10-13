using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
// The enemy pufferfish (Art/Models/bad fish, bad_fish_*: swim, attack, hit (bad_fish_attack_hit), hit and death, dead
// idle) the same way, by Use Bad Fish Model On Enemy Prefabs, with BadFish.controller (Swim / Attack / Hit / Dying /
// DeadIdle) and BadFish.mat (a copy of the artist's puffer Fish M with its emission map lit, red; made here if
// missing, kept after):
//   Pufferfish                   (the biters, and every one a Puffer Nest sends out): 1.15 m long, in proportion;
//   ChasePufferfish_Placeholder  (the chase pack, when the Chase Sequence does not use the swarm): 1.25 m long.
// The chase swarm (hundreds of fish drawn with GPU instancing, which cannot play a skinned animation) gets the swim
// baked into still frames instead: Resources/Swarm/BadFishSwarm (one mesh per frame) and an instanced copy of the
// material, Resources/Materials/SwarmBadFish, played back as a flipbook by Chase Swarm.
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

    // ---- The enemy pufferfish (Art/Models/bad fish) ---------------------------------------------------------------

    private const string BadFolder = "Assets/Art/Models/bad fish/";
    public const string BadModelPath = BadFolder + "bad_fish_swim_fix.fbx";
    public const string BadControllerPath = BadFolder + "BadFish.controller";
    public const string BadMaterialPath = BadFolder + "BadFish.mat";
    private const string BadSourceMaterialPath = BadFolder + "puffer Fish M.mat";   // the artist's own
    // The glow of the emission map (the red spots on its back, the fins, the eyes), so it reads as danger in the dark.
    private static readonly Color BadGlow = new Color(1.3f, 0.32f, 0.26f);
    private const string BadSwimClip = "bad_fish_swim_fix";
    private const string BadAttackClip = "bad_fish_attack_fix";
    private const string BadHitClip = "bad_fish_attack_hit";
    private const string BadDyingClip = "bad_fish_attack_hit_and_death_fix";
    private const string BadDeadIdleClip = "bad_fish_death_Idle_fix";
    public const float PufferfishLength = 1.15f;
    public const float ChasePufferfishLength = 1.25f;
    // The chase swarm's look, in Resources so a swarm made while the game runs finds it and a build keeps it.
    public const string SwarmMeshPath = "Assets/Resources/Swarm/BadFishSwarm.asset";
    public const string SwarmMaterialPath = "Assets/Resources/Materials/SwarmBadFish.mat";
    private const int SwarmFrames = 12;
    private const float SwarmFishAcross = 1.25f;   // the baked fish's largest size, metres (Chase Swarm's Fish Size scales it)

    [MenuItem("Tools/Out of the Depths/Use Bad Fish Model On Enemy Prefabs")]
    public static void ApplyBadFish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Bad fish: leave Play mode first.");
            return;
        }
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(BadModelPath);
        Material material = BadFishMaterial();
        if (model == null || material == null)
        {
            Debug.LogWarning($"Bad fish: no model at {BadModelPath} or no material at {BadSourceMaterialPath}.");
            return;
        }
        LoopClip(BadFolder + BadDeadIdleClip + ".fbx");   // belly-up and bobbing for as long as it drifts
        AnimatorController controller = BadController();
        if (controller == null)
            return;

        int done = 0;
        done += Dress("Pufferfish", new Vector3(0f, 0f, PufferfishLength), false, model, material, controller) ? 1 : 0;
        done += Dress("ChasePufferfish_Placeholder", new Vector3(0f, 0f, ChasePufferfishLength), false, model, material, controller) ? 1 : 0;
        bool swarm = BakeSwarm(model, Clip(BadFolder, BadSwimClip), material);
        AssetDatabase.SaveAssets();
        Debug.Log($"Bad fish: {done} enemy prefabs now wear {BadModelPath} with {BadControllerPath}" +
                  (swarm ? $"; the chase swarm's swim baked into {SwarmMeshPath}." : "; the chase swarm could not be baked."));
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
            Debug.LogWarning($"Fish model: no prefab at {path}.");
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

    private static AnimationClip Clip(string folder, string file)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(folder + file + ".fbx"))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                return clip;
        return null;
    }

    // The file's animation set to loop (as made, frame range untouched).
    private static void LoopClip(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer))
            return;
        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        bool custom = clips != null && clips.Length > 0;
        if (!custom)
            clips = importer.defaultClipAnimations;
        bool changed = !custom;
        foreach (ModelImporterClipAnimation clip in clips)
            if (!clip.loopTime)
            {
                clip.loopTime = true;
                changed = true;
            }
        if (!changed)
            return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    // Swim (the default, its speed from SwimSpeed), Attack (a bite or a lunge) and Hit (each back to Swim when done), and
    // from anywhere Dying on Die, then DeadIdle. Made the first time; edit it freely after, it is kept.
    public static AnimatorController BadController()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(BadControllerPath);
        if (existing != null)
            return existing;

        AnimationClip swim = Clip(BadFolder, BadSwimClip), attack = Clip(BadFolder, BadAttackClip), hit = Clip(BadFolder, BadHitClip),
            dying = Clip(BadFolder, BadDyingClip), deadIdle = Clip(BadFolder, BadDeadIdleClip);
        if (swim == null || attack == null || hit == null || dying == null || deadIdle == null)
        {
            Debug.LogWarning($"Bad fish: an animation is missing from {BadFolder} (swim, attack, attack hit, attack hit and death, death idle).");
            return null;
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(BadControllerPath);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter(new AnimatorControllerParameter { name = "SwimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState swimState = machine.AddState("Swim", new Vector3(300f, 0f));
        swimState.motion = swim;
        swimState.speedParameterActive = true;
        swimState.speedParameter = "SwimSpeed";
        AnimatorState attackState = machine.AddState("Attack", new Vector3(300f, -120f));
        attackState.motion = attack;
        AnimatorState hitState = machine.AddState("Hit", new Vector3(560f, 0f));
        hitState.motion = hit;
        AnimatorState dyingState = machine.AddState("Dying", new Vector3(300f, 160f));
        dyingState.motion = dying;
        AnimatorState deadState = machine.AddState("DeadIdle", new Vector3(560f, 160f));
        deadState.motion = deadIdle;
        machine.defaultState = swimState;

        On(swimState, attackState, "Attack", 0.08f);
        On(attackState, attackState, "Attack", 0.1f);   // bites again before the last one is over: bite again
        On(swimState, hitState, "Hit", 0.06f);
        On(attackState, hitState, "Hit", 0.06f);
        On(hitState, hitState, "Hit", 0.04f);
        WhenDone(attackState, swimState, 0.2f);
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

    // The chase swarm's hundreds of fish are drawn with GPU instancing, which cannot play a skinned animation, so the swim
    // is baked here into Swarm Frames still meshes (one asset, a mesh per frame) that Chase Swarm plays as a flipbook:
    // nose along +Z, centred, Swarm Fish Across metres at its largest. Plus an instanced copy of the material (kept once
    // made, so it can be tuned).
    private static bool BakeSwarm(GameObject model, AnimationClip swim, Material material)
    {
        if (model == null || swim == null)
            return false;
        Scene preview = EditorSceneManager.NewPreviewScene();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
        try
        {
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (skinned == null)
                return false;

            var frames = new List<Mesh>();
            Bounds first = default;
            float k = 1f;
            for (int f = 0; f < SwarmFrames; f++)
            {
                swim.SampleAnimation(go, swim.length * f / SwarmFrames);
                var baked = new Mesh();
                skinned.BakeMesh(baked, true);
                Matrix4x4 toWorld = skinned.transform.localToWorldMatrix;
                Vector3[] vertices = baked.vertices;
                Vector3[] normals = baked.normals;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (i < normals.Length)
                        normals[i] = toWorld.MultiplyVector(normals[i]).normalized;
                }
                if (f == 0)
                {
                    first = GeometryUtility.CalculateBounds(vertices, Matrix4x4.identity);
                    float most = Mathf.Max(first.size.x, Mathf.Max(first.size.y, first.size.z));
                    k = most > 1e-5f ? SwarmFishAcross / most : 1f;
                }
                for (int i = 0; i < vertices.Length; i++)
                    vertices[i] = (vertices[i] - first.center) * k;

                var mesh = new Mesh { name = "BadFishSwim_" + f.ToString("00"), indexFormat = baked.indexFormat };
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = baked.uv;
                mesh.subMeshCount = baked.subMeshCount;
                for (int sub = 0; sub < baked.subMeshCount; sub++)
                    mesh.SetTriangles(baked.GetTriangles(sub), sub);
                mesh.RecalculateBounds();
                frames.Add(mesh);
                Object.DestroyImmediate(baked);
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SwarmMeshPath));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(SwarmMaterialPath));
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(SwarmMeshPath);
            AssetDatabase.CreateAsset(frames[0], SwarmMeshPath);
            for (int f = 1; f < frames.Count; f++)
                AssetDatabase.AddObjectToAsset(frames[f], SwarmMeshPath);
            AssetDatabase.SaveAssets();

            var swarmMaterial = AssetDatabase.LoadAssetAtPath<Material>(SwarmMaterialPath);
            if (swarmMaterial == null)
            {
                swarmMaterial = new Material(material) { name = "SwarmBadFish", enableInstancing = true };
                AssetDatabase.CreateAsset(swarmMaterial, SwarmMaterialPath);
                if (material.IsKeywordEnabled("_EMISSION"))
                {
                    swarmMaterial.EnableKeyword("_EMISSION");
                    EditorUtility.SetDirty(swarmMaterial);
                }
            }
            else if (!swarmMaterial.enableInstancing)
            {
                swarmMaterial.enableInstancing = true;
                EditorUtility.SetDirty(swarmMaterial);
            }
            return true;
        }
        finally
        {
            Object.DestroyImmediate(go);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // The artist's puffer Fish M (URP Lit, puffer_texture, puffer_emission), copied so it can be tuned without touching
    // theirs, with the emission map switched on: its colour was black, so the spots, fins and eyes never glowed.
    private static Material BadFishMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(BadMaterialPath);
        if (material != null)
            return material;
        var source = AssetDatabase.LoadAssetAtPath<Material>(BadSourceMaterialPath);
        if (source == null)
            return null;
        material = new Material(source) { name = "BadFish" };
        if (material.HasProperty("_EmissionColor") && material.GetTexture("_EmissionMap") != null)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", BadGlow);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;   // None reads as "emissive is black" to URP, which switches the glow off again
        }
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", Mathf.Min(material.GetFloat("_Smoothness"), 0.35f));   // wet, not plastic
        AssetDatabase.CreateAsset(material, BadMaterialPath);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }
        return material;
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
