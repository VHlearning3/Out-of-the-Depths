using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The outline around whatever the player is looking at. Built the first time an object is targeted: a copy of every
// MeshRenderer under it, drawn with the inverted-hull outline shader (Resources/Shaders/OutlineHull), then simply
// switched on and off. PlayerInteractor owns the material (colour, thickness) and calls Show / Hide.
public class OutlineHull : MonoBehaviour
{
    private const string HullName = "OutlineHull";

    private readonly List<Renderer> hulls = new List<Renderer>();
    private Material material;

    public static void Show(GameObject target, Material outlineMaterial)
    {
        if (target == null || outlineMaterial == null)
            return;
        OutlineHull hull = target.GetComponent<OutlineHull>();
        if (hull == null)
            hull = target.AddComponent<OutlineHull>();
        hull.Build(outlineMaterial);
        hull.SetVisible(true);
    }

    public static void Hide(GameObject target)
    {
        if (target == null)
            return;
        OutlineHull hull = target.GetComponent<OutlineHull>();
        if (hull != null)
            hull.SetVisible(false);
    }

    // Rebuilt when the material changes or a renderer has gone (a pickup swapped its model, a fish died).
    private void Build(Material outlineMaterial)
    {
        if (material == outlineMaterial && hulls.Count > 0 && AllAlive())
            return;
        Clear();
        material = outlineMaterial;

        foreach (MeshRenderer source in GetComponentsInChildren<MeshRenderer>(false))
        {
            if (source.name == HullName)
                continue;   // never outline an outline
            MeshFilter filter = source.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;

            var go = new GameObject(HullName);
            go.transform.SetParent(source.transform, false);
            go.layer = source.gameObject.layer;
            go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;

            var renderer = go.AddComponent<MeshRenderer>();
            var materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var block = new MaterialPropertyBlock();
            block.SetVector("_Center", filter.sharedMesh.bounds.center);
            renderer.SetPropertyBlock(block);
            renderer.enabled = false;
            hulls.Add(renderer);
        }
    }

    private bool AllAlive()
    {
        foreach (Renderer hull in hulls)
            if (hull == null)
                return false;
        return true;
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer hull in hulls)
        {
            if (hull == null)
                continue;
            // Follow the source renderer: no outline on a hidden mesh.
            Renderer source = hull.transform.parent != null ? hull.transform.parent.GetComponent<Renderer>() : null;
            hull.enabled = visible && (source == null || source.enabled);
        }
    }

    private void Clear()
    {
        foreach (Renderer hull in hulls)
            if (hull != null)
                Destroy(hull.gameObject);
        hulls.Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }
}
