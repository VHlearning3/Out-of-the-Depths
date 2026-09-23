using UnityEngine;

// A current: while the player is inside this trigger box it carries them along its blue arrow (forward), up to Max
// Speed that way; swimming against it still works, just slowly. A stream of bubbles shows where it runs (a bubble lift
// when it points up). The ship puts bubble lifts under the two hatches and one up to a pearl in the basement.
[RequireComponent(typeof(BoxCollider))]
public class WaterCurrent : MonoBehaviour
{
    [Tooltip("How hard it pushes, in metres per second per second.")]
    [SerializeField] private float strength = 5f;
    [Tooltip("It stops adding speed once you are going this fast along it, in metres per second.")]
    [SerializeField] private float maxSpeed = 4f;

    [Header("Bubbles")]
    [SerializeField] private bool bubbles = true;
    [Tooltip("A soft round speck (the builders use Art/Materials/WaterMotes.mat). Empty = no bubbles.")]
    [SerializeField] private Material bubbleMaterial;
    [Tooltip("Bubbles per second.")]
    [SerializeField] private float bubbleRate = 30f;
    [SerializeField] private Vector2 bubbleSize = new Vector2(0.04f, 0.14f);

    private SwimController swimmer;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (bubbles && bubbleMaterial != null)
            MakeBubbles();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerBody.Is(other))
            swimmer = other.GetComponentInParent<SwimController>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (PlayerBody.Is(other))
            swimmer = null;
    }

    private void Update()
    {
        if (swimmer == null || !swimmer.isActiveAndEnabled || swimmer.Frozen)
            return;
        Vector3 along = transform.forward;
        float speed = Vector3.Dot(swimmer.CurrentVelocity, along);
        if (speed < maxSpeed)
            swimmer.AddImpulse(along * Mathf.Min(strength * Time.deltaTime, maxSpeed - speed));
    }

    // Bubbles rising from the box's back face to its front, spread across it.
    private void MakeBubbles()
    {
        var box = GetComponent<BoxCollider>();
        var go = new GameObject("Bubbles");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = box.center - Vector3.forward * (box.size.z * 0.5f);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        float length = Mathf.Max(0.5f, box.size.z * transform.lossyScale.z);
        float travel = Mathf.Max(0.5f, maxSpeed * 0.6f);
        var main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(length / travel * 0.7f, length / travel);
        main.startSpeed = new ParticleSystem.MinMaxCurve(travel * 0.8f, travel * 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(bubbleSize.x, bubbleSize.y);
        main.startColor = new Color(0.85f, 0.95f, 1f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.CeilToInt(bubbleRate * length / travel * 1.5f) + 10;

        var emission = system.emission;
        emission.rateOverTime = bubbleRate;

        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;   // the back face, emitting along forward
        shape.scale = new Vector3(box.size.x * 0.8f, box.size.y * 0.8f, 1f);

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.8f;

        var fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = bubbleMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
            return;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.12f);
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.DrawLine(box.center, box.center + Vector3.forward * (box.size.z * 0.5f));
        Gizmos.matrix = Matrix4x4.identity;
    }
}
