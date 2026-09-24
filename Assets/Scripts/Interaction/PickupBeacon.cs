using System.Collections.Generic;
using UnityEngine;

// Makes a pickup stand out in the murk: a soft glow behind it that shows through the fog (the Sun Glow shader, sized
// to the item and breathing slowly) and a small pulsing light that lights up what it lies on. The colour says what
// kind of thing it is: keys gold, puzzle pieces sea green, weapons ice blue, pearls and shells pearly pink. Weapons
// (the dagger, the trident) glow bigger and brighter than the rest (Weapon Boost).
// Pickup Item adds one to itself at start (its Beacon switch). The glow and light live outside the pickup, so the
// outline, the highlight and the model fitting never see them; they follow it, and go when it is picked up, hidden
// (a shut drawer) or gone.
[RequireComponent(typeof(PickupItem))]
public class PickupBeacon : MonoBehaviour
{
    [Tooltip("How big the glow is, as a multiple of the item's size (at least Min Glow Size metres).")]
    [SerializeField] private float glowScale = 2.4f;
    [SerializeField] private float minGlowSize = 0.7f;
    [Tooltip("How strong the glow is.")]
    [SerializeField, Range(0f, 3f)] private float glowStrength = 1f;
    [Tooltip("The light's brightness (it pulses between half and all of this) and reach in metres.")]
    [SerializeField] private float lightIntensity = 1.2f;
    [SerializeField] private float lightRange = 2.5f;
    [Tooltip("Seconds per pulse.")]
    [SerializeField] private float pulseSeconds = 2.2f;
    [Tooltip("Weapons: the glow's size and strength and the light's brightness and reach are multiplied by this.")]
    [SerializeField] private float weaponBoost = 1.8f;

    private static readonly Dictionary<ItemDefinition.Category, Material> materials = new Dictionary<ItemDefinition.Category, Material>();
    private static Transform holder;   // every beacon goes under one "Pickup Beacons" object, not loose in the scene

    private PickupItem pickup;
    private Transform beacon;
    private Transform glow;
    private Light glowLight;
    private Vector3 centre;     // the item's middle, in the pickup's space
    private float size;
    private float phase;
    private float boost = 1f;

    public static Color ColourFor(ItemDefinition item)
    {
        switch (item != null ? item.Kind : ItemDefinition.Category.PuzzlePiece)
        {
            case ItemDefinition.Category.Key: return new Color(1f, 0.78f, 0.35f);
            case ItemDefinition.Category.Weapon: return new Color(0.6f, 0.88f, 1f);
            case ItemDefinition.Category.Collectible: return new Color(1f, 0.82f, 0.95f);
            default: return new Color(0.45f, 1f, 0.82f);
        }
    }

    private void Start()
    {
        pickup = GetComponent<PickupItem>();
        phase = Random.value * 10f;

        // The item's middle and size, from its meshes (not the sparkle).
        var bounds = new Bounds(transform.position, Vector3.zero);
        bool any = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer)
                continue;
            if (any)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            any = true;
        }
        centre = transform.InverseTransformPoint(bounds.center);
        size = Mathf.Max(minGlowSize, Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * glowScale);
        boost = pickup.Item != null && pickup.Item.Kind == ItemDefinition.Category.Weapon ? Mathf.Max(1f, weaponBoost) : 1f;
        size *= boost;

        Color colour = ColourFor(pickup.Item);
        if (holder == null)
            holder = new GameObject("Pickup Beacons").transform;
        beacon = new GameObject("Beacon (" + name + ")").transform;
        beacon.SetParent(holder, false);

        Material material = MaterialFor(pickup.Item, colour, boost);
        if (material != null)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Glow";
            quad.transform.SetParent(beacon, false);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            glow = quad.transform;
        }

        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(beacon, false);
        glowLight = lightObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = colour;
        glowLight.range = lightRange * boost;
        glowLight.shadows = LightShadows.None;

        beacon.gameObject.SetActive(isActiveAndEnabled);
        LateUpdate();
    }

    private void OnEnable()
    {
        if (beacon != null)
            beacon.gameObject.SetActive(true);
    }

    private void OnDisable()
    {
        if (beacon != null)
            beacon.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (beacon != null)
            Destroy(beacon.gameObject);
    }

    private void LateUpdate()
    {
        if (beacon == null)
            return;
        bool show = !pickup.Collected;
        if (beacon.gameObject.activeSelf != show)
            beacon.gameObject.SetActive(show);
        if (!show)
            return;

        Vector3 middle = transform.TransformPoint(centre);
        beacon.position = middle;
        float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time + phase) * Mathf.PI * 2f / Mathf.Max(0.1f, pulseSeconds));
        glowLight.intensity = lightIntensity * boost * (0.5f + 0.5f * pulse);

        Camera camera = Camera.main;
        if (glow != null && camera != null)
        {
            // Just behind the item as seen from the camera, facing it, so the item sits in front of its own glow.
            Vector3 away = middle - camera.transform.position;
            float distance = away.magnitude;
            away = distance > 0.001f ? away / distance : camera.transform.forward;
            glow.position = middle + away * (size * 0.25f);
            glow.rotation = Quaternion.LookRotation(away, camera.transform.up);
            glow.localScale = Vector3.one * size * (0.92f + 0.08f * pulse);
        }
    }

    // One glow material per kind of item, shared by every pickup of that kind.
    private Material MaterialFor(ItemDefinition item, Color colour, float boost)
    {
        ItemDefinition.Category kind = item != null ? item.Kind : ItemDefinition.Category.PuzzlePiece;
        if (materials.TryGetValue(kind, out Material material) && material != null)
            return material;
        Shader shader = Shader.Find("Out of the Depths/Sun Glow");
        if (shader == null)
            return null;
        material = new Material(shader) { name = "PickupGlow " + kind };
        material.SetColor("_Color", colour);
        material.SetFloat("_Core", 0.35f * glowStrength * boost);
        material.SetFloat("_CoreSize", 0.25f);
        material.SetFloat("_Halo", 0.55f * glowStrength * boost);
        material.SetFloat("_Shimmer", 0.35f);
        materials[kind] = material;
        return material;
    }
}
