using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A pufferfish nest: where the enemy pufferfish come from, the way windows and roof holes are where the friendly fish
// come from, but made to be seen: a dark red, spiny mound with a hot red glow in its mouth and a red light that
// pulses, nothing like the cool blue windows. It wakes when the player is within Wake Range and it can see them, and
// keeps up to Max Alive pufferfish out: before each one it warns you (the glow swells and flickers and it
// rumbles for Warn Seconds), then the pufferfish bursts out of its mouth, already puffed up, and comes for you.
// One that is killed is replaced Refill Seconds after it would have respawned. The twist: slash the nest itself
// (Nest Health, a few hits) and it cracks, goes dark and never sends another. A health bar floats over it while you
// are near and can see it (Show Bar Within), and dips with each hit.
// Place it with its blue arrow (forward) pointing out of its mouth: up from a floor, into the room from a wall. It
// builds its own look when it has no model under it; give it a Pufferfish prefab. The builders place them.
public class PufferNest : MonoBehaviour, IDamageable
{
    [Header("Pufferfish")]
    [SerializeField] private GameObject pufferPrefab;
    [Tooltip("How many of its pufferfish can be out at once.")]
    [SerializeField, Min(1)] private int maxAlive = 2;
    [Tooltip("Wakes when the player is this close (metres) and in sight of its mouth.")]
    [SerializeField] private float wakeRange = 11f;
    [Tooltip("The warning before each pufferfish: glow, bubbles, rumble (seconds).")]
    [SerializeField] private float warnSeconds = 1.3f;
    [Tooltip("Seconds between one pufferfish coming out and the next starting to warn.")]
    [SerializeField] private float between = 2.5f;
    [Tooltip("A killed pufferfish comes back this long after it would have respawned.")]
    [SerializeField] private float refillSeconds = 8f;
    [Tooltip("How far a pufferfish shoots out of the mouth before it turns on the player, and how fast.")]
    [SerializeField] private float burstDistance = 2.2f;
    [SerializeField] private float burstSpeed = 7f;

    [Header("Nest")]
    [Tooltip("Slashes' worth of damage it takes to destroy it (the dagger does about 20 a hit).")]
    [SerializeField] private float nestHealth = 60f;
    [SerializeField] private float size = 1.3f;
    [SerializeField] private Color bodyColor = new Color(0.26f, 0.05f, 0.07f);
    [SerializeField] private Color spikeColor = new Color(0.92f, 0.78f, 0.45f);
    [SerializeField] private Color glowColor = new Color(1f, 0.18f, 0.12f);
    [SerializeField] private AudioClip warnSound;
    [SerializeField] private AudioClip burstSound;
    [SerializeField] private AudioClip breakSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [Tooltip("The health bar over it shows while the player is this close (metres) and can see it.")]
    [SerializeField] private float showBarWithin = 16f;

    public bool Destroyed { get; private set; }

    private readonly List<GameObject> puffers = new List<GameObject>();
    private readonly Queue<GameObject> waiting = new Queue<GameObject>();   // made, ready to come out
    private Transform player;
    private Transform look;          // the built look (scaled as it warns and breathes)
    private Transform glow;
    private Light glowLight;
    private Material glowMaterial;
    private readonly List<Renderer> bodyRenderers = new List<Renderer>();
    private float health;
    private float warmth;            // 0..1, how hot the glow is (warning)
    private float nextReadyAt;
    private bool busy;
    private float hitFlash;
    private float barShown;          // 0..1, the health bar fading in and out
    private float barHealth;         // what the bar shows: follows the real health, the chunk a hit took drains after a moment
    private float lastHitAt = -10f;
    private static readonly RaycastHit[] sight = new RaycastHit[8];

    private Vector3 Mouth => transform.position + transform.forward * (size * 0.35f);

    private void Start()
    {
        health = nestHealth;
        barHealth = nestHealth;
        var swimmer = FindFirstObjectByType<SwimController>();
        player = swimmer != null ? swimmer.transform : null;
        if (GetComponentInChildren<Renderer>() == null)
            BuildLook();
        else
            look = transform;
        MakeGlow();
        for (int i = 0; i < maxAlive && pufferPrefab != null; i++)
            MakePuffer();
        nextReadyAt = Time.time + 0.5f;
    }

    private void OnDestroy()
    {
        if (glowMaterial != null)
            Destroy(glowMaterial);
    }

    // ---- The pufferfish -------------------------------------------------------------------------------------------

    // Made once, parked inside the nest (switched off) until it is its turn; killed ones come back to it.
    private void MakePuffer()
    {
        GameObject go = Instantiate(pufferPrefab, Mouth, transform.rotation);
        go.name = pufferPrefab.name + " (" + name + ")";
        go.SetActive(false);
        puffers.Add(go);
        waiting.Enqueue(go);
        var edible = go.GetComponent<EdibleFish>();
        if (edible != null)
            edible.onRespawned.AddListener(() => Returned(go));
    }

    // A killed one has respawned (Fish Controller put it back where it was made): into the nest, out again later.
    private void Returned(GameObject go)
    {
        go.SetActive(false);
        if (Destroyed)
            return;
        StartCoroutine(RefillLater(go));
    }

    private IEnumerator RefillLater(GameObject go)
    {
        yield return new WaitForSeconds(refillSeconds);
        if (!Destroyed && go != null && !waiting.Contains(go))
            waiting.Enqueue(go);
    }

    private void Update()
    {
        if (player == null)
        {
            var swimmer = FindFirstObjectByType<SwimController>();
            player = swimmer != null ? swimmer.transform : null;
        }
        Breathe();
        UpdateBar();
        if (Destroyed || busy || waiting.Count == 0 || Time.time < nextReadyAt || !SeesPlayer())
            return;
        StartCoroutine(SendOne(waiting.Dequeue()));
    }

    // Awake: the player is near and the mouth can see them.
    private bool SeesPlayer()
    {
        if (player == null)
            return false;
        Vector3 target = player.position + Vector3.up;
        Vector3 delta = target - Mouth;
        float distance = delta.magnitude;
        if (distance > wakeRange)
            return false;
        int n = Physics.RaycastNonAlloc(Mouth, delta / distance, sight, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider hit = sight[i].collider;
            if (PlayerBody.Is(hit) || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player) || hit.GetComponentInParent<FishController>() != null)
                continue;
            return false;
        }
        return true;
    }

    // The warning, then out it comes: puffed, fast, straight out of the mouth, and only then after the player.
    private IEnumerator SendOne(GameObject go)
    {
        busy = true;
        if (warnSound != null)
            SoundVariety.PlayAt(warnSound, Mouth, volume);
        for (float t = 0f; t < warnSeconds; t += Time.deltaTime)
        {
            warmth = Mathf.Max(warmth, t / warnSeconds);
            if (Destroyed)
            {
                busy = false;
                waiting.Enqueue(go);
                yield break;
            }
            yield return null;
        }
        if (go == null)
        {
            busy = false;
            yield break;
        }

        var aggression = go.GetComponent<FishAggression>();
        var wander = go.GetComponent<FishWander>();
        go.transform.SetPositionAndRotation(Mouth, Quaternion.LookRotation(transform.forward, Mathf.Abs(transform.forward.y) > 0.9f ? transform.up : Vector3.up));
        go.SetActive(true);
        if (aggression != null)
            aggression.enabled = false;
        if (wander != null)
            wander.enabled = false;
        if (burstSound != null)
            SoundVariety.PlayAt(burstSound, Mouth, volume);
        warmth = 1.4f;   // a flash as it goes

        Vector3 from = Mouth;
        Vector3 to = Mouth + transform.forward * burstDistance;
        float seconds = burstDistance / Mathf.Max(0.1f, burstSpeed);
        Vector3 scale = go.transform.localScale;
        for (float t = 0f; t < seconds && go != null; t += Time.deltaTime)
        {
            float k = Ease.OutCubic(t / seconds);
            go.transform.position = Vector3.Lerp(from, to, k);
            go.transform.localScale = scale * Mathf.Lerp(0.4f, 1f, k);
            yield return null;
        }
        if (go != null && !Alive(go))
            go.transform.localScale = scale;   // killed on the way out: leave it to die, do not wake it
        else if (go != null)
        {
            go.transform.position = to;
            go.transform.localScale = scale;
            if (wander != null)
            {
                wander.ResetHome(to);
                wander.enabled = true;
            }
            if (aggression != null)
                aggression.enabled = true;
        }
        nextReadyAt = Time.time + between;
        busy = false;
    }

    private static bool Alive(GameObject go)
    {
        var body = go.GetComponent<FishController>();
        return body == null || (body.IsAlive && !body.IsFading);
    }

    // ---- The nest -------------------------------------------------------------------------------------------------

    // Slashed: it flinches; out of health it cracks and goes dark for good.
    public void TakeDamage(float amount)
    {
        if (Destroyed || amount <= 0f)
            return;
        health -= amount;
        hitFlash = 1f;
        lastHitAt = Time.time;
        if (health <= 0f)
            Break();
    }

    // Checkpoint Save: broken already.
    public void RestoreBroken()
    {
        if (!Destroyed)
            Break(false);
    }

    private void Break(bool loud = true)
    {
        Destroyed = true;
        StopAllCoroutines();
        busy = false;
        waiting.Clear();
        if (loud && breakSound != null)
            SoundVariety.PlayAt(breakSound, transform.position, volume);
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;
        // One caught half way out of the mouth is let go at once.
        foreach (GameObject go in puffers)
        {
            if (go == null || !go.activeSelf || !Alive(go))
                continue;
            var aggression = go.GetComponent<FishAggression>();
            var wander = go.GetComponent<FishWander>();
            if (wander != null && !wander.enabled && (aggression == null || !aggression.enabled))
                wander.enabled = true;
            if (aggression != null)
                aggression.enabled = true;
        }
        StartCoroutine(Crumble());
    }

    private IEnumerator Crumble()
    {
        Vector3 from = look.localScale;
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            float k = t / 0.8f;
            look.localScale = Vector3.Scale(from, new Vector3(1f + 0.25f * k, 1f - 0.55f * k, 1f + 0.25f * k));
            yield return null;
        }
        // Dead rock: dull, no glow.
        foreach (Renderer r in bodyRenderers)
        {
            var tint = r.GetComponent<RendererTint>();
            if (tint != null)
                tint.Tint = Color.Lerp(tint.Tint, new Color(0.12f, 0.1f, 0.1f), 0.7f);
        }
    }

    // The glow breathes slowly while it sleeps, swells and flickers as it warns, flares as one goes, flinches when hit.
    private void Breathe()
    {
        warmth = Mathf.MoveTowards(warmth, 0f, Time.deltaTime * (busy ? 0f : 0.9f));
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 4f);
        float idle = 0.5f + 0.5f * Mathf.Sin(Time.time * 1.6f + transform.position.x);
        float flicker = warmth > 0.05f ? Mathf.PerlinNoise(Time.time * 18f, transform.position.z) : 0f;
        float heat = Destroyed ? 0f : 0.35f + 0.25f * idle + warmth * (0.6f + 0.5f * flicker);

        if (glowLight != null)
        {
            glowLight.intensity = Mathf.Lerp(glowLight.intensity, heat * 3f, 1f - Mathf.Exp(-10f * Time.deltaTime));
            glowLight.range = size * 5f;
        }
        if (glow != null)
        {
            Camera eye = Camera.main;
            glow.gameObject.SetActive(!Destroyed);
            if (eye != null)
            {
                Vector3 toEye = eye.transform.position - Mouth;
                glow.position = Mouth + toEye.normalized * 0.1f;
                glow.rotation = Quaternion.LookRotation(-toEye, eye.transform.up);
            }
            glow.localScale = Vector3.one * size * (1.4f + 1.2f * heat);
            if (glowMaterial != null)
                glowMaterial.SetFloat("_Halo", 0.35f + 0.6f * heat);
        }
        if (look != null && look != transform && !Destroyed)
        {
            float swell = 1f + 0.04f * idle + 0.12f * Mathf.Clamp01(warmth) - 0.1f * hitFlash;
            look.localScale = Vector3.one * swell;
        }
    }

    // ---- Look -----------------------------------------------------------------------------------------------------

    // A squat dark mound with a hole in it, a ring of pale spikes round the mouth and more spikes over its back.
    private void BuildLook()
    {
        look = new GameObject("Look").transform;
        look.SetParent(transform, false);
        var rng = new System.Random(name.GetHashCode());
        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // In the nest's own space: +Z (its blue arrow) is out of the mouth; the mound is squashed along it, half sunk
        // into the wall or floor behind.
        Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(size, size, size * 0.55f), Quaternion.identity, bodyColor);
        Part(PrimitiveType.Sphere, new Vector3(0f, 0f, size * 0.25f), new Vector3(size * 0.42f, size * 0.42f, size * 0.1f), Quaternion.identity, new Color(0.05f, 0f, 0f));
        // A ring of pale spikes round the mouth, leaning out...
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2f / 12f + Range(-0.12f, 0.12f);
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Vector3 at = dir * size * 0.3f + Vector3.forward * size * 0.18f;
            Quaternion turn = Quaternion.LookRotation(Vector3.Lerp(dir, Vector3.forward, 0.55f).normalized);
            Part(PrimitiveType.Cube, at + turn * Vector3.forward * size * 0.14f, new Vector3(0.05f, 0.05f, size * Range(0.28f, 0.42f)), turn, spikeColor);
        }
        // ...and more over its back, all on the side facing out.
        for (int i = 0; i < 18; i++)
        {
            Vector3 dir = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(0.05f, 0.7f)).normalized;
            Vector3 at = Vector3.Scale(dir, new Vector3(size * 0.48f, size * 0.48f, size * 0.26f));
            Quaternion turn = Quaternion.LookRotation(dir);
            Part(PrimitiveType.Cube, at + turn * Vector3.forward * size * 0.1f, new Vector3(0.045f, 0.045f, size * Range(0.18f, 0.3f)), turn, spikeColor * 0.85f);
        }

        // Something to hit: the slash finds the nest through this.
        var hitbox = gameObject.AddComponent<SphereCollider>();
        hitbox.radius = size * 0.5f;
    }

    private void Part(PrimitiveType type, Vector3 position, Vector3 scale, Quaternion rotation, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        DestroyImmediate(part.GetComponent<Collider>());   // the nest's own sphere is its one collider
        part.transform.SetParent(look, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        part.AddComponent<RendererTint>().Tint = color;
        bodyRenderers.Add(part.GetComponent<Renderer>());
    }

    // The red glow in the mouth (a soft billboard that shows through the murk) and the pulsing red light.
    private void MakeGlow()
    {
        Shader shader = Shader.Find("Out of the Depths/Sun Glow");
        if (shader != null)
        {
            glowMaterial = new Material(shader) { name = "PufferNest Glow (runtime)" };
            glowMaterial.SetColor("_Color", glowColor);
            glowMaterial.SetFloat("_Core", 0.5f);
            glowMaterial.SetFloat("_CoreSize", 0.22f);
            glowMaterial.SetFloat("_Halo", 0.5f);
            glowMaterial.SetFloat("_Shimmer", 0.5f);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Glow";
            quad.transform.SetParent(transform, false);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = glowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            glow = quad.transform;
        }
        var lightObject = new GameObject("Light");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.position = Mouth + transform.forward * 0.4f;
        glowLight = lightObject.AddComponent<Light>();
        glowLight.type = LightType.Point;
        glowLight.color = glowColor;
        glowLight.shadows = LightShadows.None;
        glowLight.intensity = 1f;
    }

    // ---- Health bar -----------------------------------------------------------------------------------------------

    private void UpdateBar()
    {
        bool show = !Destroyed && player != null && Vector3.Distance(player.position, transform.position) <= showBarWithin && InView();
        barShown = Mathf.MoveTowards(barShown, show ? 1f : 0f, Time.deltaTime / (show ? 0.25f : 0.5f));
        // The chunk a hit took hangs there a moment, then drains down to the real health.
        if (Time.time - lastHitAt > 0.45f)
            barHealth = Mathf.MoveTowards(barHealth, Mathf.Max(0f, health), nestHealth * 1.5f * Time.deltaTime);
    }

    // On screen, in front, and nothing solid between the eyes and the nest.
    private bool InView()
    {
        Camera eye = Camera.main;
        if (eye == null)
            return false;
        Vector3 top = transform.position + transform.forward * (size * 0.4f);
        Vector3 viewport = eye.WorldToViewportPoint(top);
        if (viewport.z <= 0f || viewport.x < -0.05f || viewport.x > 1.05f || viewport.y < -0.05f || viewport.y > 1.05f)
            return false;
        Vector3 delta = top - eye.transform.position;
        float distance = delta.magnitude;
        int n = Physics.RaycastNonAlloc(eye.transform.position, delta / distance, sight, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider hit = sight[i].collider;
            if (hit.transform.IsChildOf(transform) || PlayerBody.Is(hit) || (player != null && hit.transform.IsChildOf(player)) || hit.GetComponentInParent<FishController>() != null)
                continue;
            return false;
        }
        return true;
    }

    // A small dark pill over the nest: NEST in small red capitals, a red fill for what is left, a pale chunk for what
    // the last hit took. Bigger up close, smaller far off, like the damage numbers over fish.
    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || barShown <= 0f || PauseMenu.IsOpen || EndCredits.Playing)
            return;
        Camera eye = Camera.main;
        if (eye == null)
            return;
        Vector3 over = transform.position + transform.forward * (size * 0.5f) + Vector3.up * (size * 0.55f + 0.35f);
        Vector3 screen = eye.WorldToScreenPoint(over);
        if (screen.z <= 0.1f)
            return;
        float s = HudStyle.Scale * Mathf.Clamp(7f / screen.z, 0.55f, 1.3f);
        float alpha = barShown;
        float w = 118f * s, h = 22f * s, pad = 7f * s;
        var panel = new Rect(screen.x - w * 0.5f, Screen.height - screen.y - h, w, h);
        HudStyle.Panel(panel, h * 0.5f, alpha);

        GUIStyle label = HudStyle.Label(Mathf.Max(7, Mathf.RoundToInt(9f * s)));
        float labelWidth = HudStyle.Spaced(new Vector2(panel.x + pad + 2f * s, panel.center.y - label.fontSize * 0.8f), "NEST", label, glowColor, 1.6f * s, alpha);
        float trackX = panel.x + pad + labelWidth + 8f * s;
        var track = new Rect(trackX, panel.center.y - 3f * s, panel.xMax - pad - 2f * s - trackX, 6f * s);
        HudStyle.Fill(track, 3f * s, new Color(1f, 1f, 1f, 0.12f * alpha));
        float now = Mathf.Clamp01(health / nestHealth);
        float shown = Mathf.Clamp01(barHealth / nestHealth);
        if (shown > now + 0.001f)
            HudStyle.Fill(new Rect(track.x, track.y, track.width * shown, track.height), 3f * s, new Color(1f, 0.92f, 0.85f, 0.75f * alpha));
        if (now > 0.001f)
        {
            Color fill = Color.Lerp(HudStyle.Danger, Color.white, hitFlash * 0.6f);
            HudStyle.Fill(new Rect(track.x, track.y, Mathf.Max(track.height, track.width * now), track.height), 3f * s, new Color(fill.r, fill.g, fill.b, alpha));
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, size * 0.5f);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * (size * 0.35f + burstDistance));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.15f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, wakeRange);
    }
}
