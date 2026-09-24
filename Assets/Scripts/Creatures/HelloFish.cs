using UnityEngine;

// The easter egg: now and then, as a pickup comes up to be inspected, a little fish darts in from the side of the
// screen, stops beside the item, turns to you and says "Hello!" in a speech bubble (with two tiny blips), then shoots
// off across the view and is gone, all in about two seconds. Chance is how often (per inspect); Always (the admin
// page switch) makes it every time. It looks like the fish in the scene (a copy of one's meshes, nothing else of it)
// or, with no fish around, a small made-up one. Taking the item early sends it off at once. It makes itself.
public class HelloFish : MonoBehaviour
{
    // How often an inspect brings it, 0..1.
    public static float Chance = 0.1f;
    // Testing (admin page): every inspect.
    public static bool Always { get; set; }

    private const float InTime = 0.45f;      // darting in
    private const float HelloTime = 1.05f;   // stopped beside the item, saying hello
    private const float OutTime = 0.5f;      // shooting off
    private const float Length = 0.28f;      // nose to tail, in metres

    private static HelloFish current;

    private Transform eye;
    private Transform item;
    private float side;              // +1 = comes in from the right, -1 = from the left
    private float age;
    private float leaveAt = InTime + HelloTime;
    private Vector3 leaveFrom;       // camera space, where it was when it started to leave
    private bool leaving;
    private Vector3 lastPosition;
    private readonly System.Collections.Generic.List<Material> madeMaterials = new System.Collections.Generic.List<Material>();
    private Quaternion heading;      // where it faces, before the tail beat
    private GUIStyle style;
    private Texture2D bubble;
    private AudioSource voice;
    private AudioClip blip;
    private bool said;

    public static void Maybe(Camera camera, Transform heldItem)
    {
        if (current != null || camera == null || heldItem == null)
            return;
        if (!Always && Random.value >= Chance)
            return;
        var fish = new GameObject("HelloFish").AddComponent<HelloFish>();
        fish.eye = camera.transform;
        fish.item = heldItem;
        fish.side = Random.value < 0.5f ? -1f : 1f;
        fish.BuildBody();
        fish.transform.position = fish.eye.TransformPoint(fish.StartPoint());
        fish.lastPosition = fish.transform.position;
        fish.heading = Quaternion.LookRotation(fish.eye.TransformPoint(fish.HelloPoint()) - fish.transform.position, fish.eye.up);
        fish.transform.rotation = fish.heading;
        current = fish;
    }

    // The item was taken: off it goes, straight away.
    public static void Hurry()
    {
        if (current != null && !current.leaving)
            current.leaveAt = Mathf.Min(current.leaveAt, current.age);
    }

    private void OnDestroy()
    {
        if (current == this)
            current = null;
        foreach (Material made in madeMaterials)
            Destroy(made);
        if (bubble != null)
            Destroy(bubble);
        if (blip != null)
            Destroy(blip);
    }

    // ---- Where it swims (camera space: x right, y up, z ahead) ---------------------------------------------------

    private float ItemDepth => item != null ? Mathf.Clamp(eye.InverseTransformPoint(item.position).z, 0.4f, 2.5f) : 0.8f;
    private Vector3 StartPoint() => new Vector3(side * 1.35f, -0.12f, ItemDepth + 0.35f);
    private Vector3 HelloPoint() => new Vector3(side * 0.3f, 0.07f, ItemDepth + 0.05f);
    private Vector3 ExitPoint() => new Vector3(-side * 1.6f, 0.4f, ItemDepth + 0.7f);

    private void LateUpdate()
    {
        if (eye == null)
        {
            Destroy(gameObject);
            return;
        }
        age += Time.deltaTime;
        if (!leaving && age >= leaveAt)
        {
            leaving = true;
            leaveFrom = eye.InverseTransformPoint(transform.position);
        }

        Vector3 local;
        float wiggle;
        bool greeting = false;
        if (leaving)
        {
            float k = Mathf.Clamp01((age - leaveAt) / OutTime);
            local = Vector3.Lerp(leaveFrom, ExitPoint(), k * k);   // speeding up as it goes
            local.y += Mathf.Sin(k * Mathf.PI) * 0.08f;
            wiggle = 16f;
            if (k >= 1f)
            {
                Destroy(gameObject);
                return;
            }
        }
        else if (age < InTime)
        {
            float k = Ease.OutCubic(age / InTime);
            local = Vector3.Lerp(StartPoint(), HelloPoint(), k);
            local.y += Mathf.Sin(k * Mathf.PI) * 0.05f;
            wiggle = Mathf.Lerp(16f, 6f, k);
        }
        else
        {
            local = HelloPoint() + new Vector3(0f, Mathf.Sin((age - InTime) * 9f) * 0.008f, 0f);
            wiggle = 4f;
            greeting = true;
        }

        Vector3 position = eye.TransformPoint(local);
        Vector3 moved = position - lastPosition;
        lastPosition = position;
        transform.position = position;

        // Nose along its path while swimming; turned to face you (a little to the side, so it still reads as a fish)
        // while it says hello.
        Vector3 facing = moved.sqrMagnitude > 1e-6f ? moved.normalized : transform.forward;
        if (greeting)
            facing = Vector3.Slerp(-eye.forward, eye.right * -side, 0.35f);
        float turn = 1f - Mathf.Exp(-(greeting ? 14f : 20f) * Time.deltaTime);
        heading = Quaternion.Slerp(heading, Quaternion.LookRotation(facing, eye.up), turn);
        transform.rotation = heading * Wiggle(wiggle);

        if (greeting && !said && age >= InTime + 0.08f)
        {
            said = true;
            Say();
        }
    }

    // The tail beat: a quick side-to-side yaw, faster the harder it swims.
    private float wigglePhase;
    private Quaternion Wiggle(float degrees)
    {
        return Quaternion.Euler(0f, Mathf.Sin(wigglePhase) * degrees, 0f);
    }

    private void Update() => wigglePhase += Time.deltaTime * (leaving || age < InTime ? 22f : 10f);

    // ---- The body ------------------------------------------------------------------------------------------------

    // A copy of a scene fish's meshes (a living one if there is one), or a small made-up fish; scaled to Length.
    private void BuildBody()
    {
        var body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        FishController model = null;
        foreach (FishController fish in FindObjectsByType<FishController>(FindObjectsSortMode.None))
        {
            if (fish.GetComponentInChildren<Renderer>() == null)
                continue;
            model = fish;
            if (fish.IsAlive)
                break;
        }
        if (model == null || !CopyMeshes(model.transform, body))
            MakeFish(body);

        // Centre it and make it Length long, nose to tail.
        Bounds bounds = default;
        bool any = false;
        foreach (Renderer r in body.GetComponentsInChildren<Renderer>())
        {
            if (any)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            any = true;
        }
        if (!any)
            return;
        float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        float scale = longest > 1e-4f ? Length / longest : 1f;
        body.localScale *= scale;
        body.localPosition = -(bounds.center - transform.position) * scale;
    }

    private static bool CopyMeshes(Transform fish, Transform into)
    {
        bool copied = false;
        foreach (Renderer source in fish.GetComponentsInChildren<Renderer>())
        {
            if (!source.enabled || !(source is MeshRenderer || source is SkinnedMeshRenderer))
                continue;
            Mesh mesh = source is SkinnedMeshRenderer skinned ? skinned.sharedMesh : source.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || source.sharedMaterial == null || source.sharedMaterial.shader.name.Contains("Outline"))
                continue;
            var part = new GameObject(source.name);
            part.transform.SetParent(into, false);
            part.transform.localPosition = Quaternion.Inverse(fish.rotation) * (source.transform.position - fish.position);
            part.transform.localRotation = Quaternion.Inverse(fish.rotation) * source.transform.rotation;
            part.transform.localScale = source.transform.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            copied = true;
        }
        return copied;
    }

    // No fish in the scene to copy: an orange body, a tail and two eyes, nose along +Z.
    private void MakeFish(Transform into)
    {
        Material skin = null, eyes = null;
        void Part(PrimitiveType type, Vector3 position, Vector3 scale, Vector3 euler, bool eye)
        {
            var part = GameObject.CreatePrimitive(type);
            DestroyImmediate(part.GetComponent<Collider>());   // nothing to bump into or aim at
            part.transform.SetParent(into, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (skin == null)
            {
                skin = new Material(renderer.sharedMaterial) { name = "Hello Fish (runtime)", color = new Color(1f, 0.55f, 0.15f) };
                eyes = new Material(renderer.sharedMaterial) { name = "Hello Fish Eyes (runtime)", color = Color.black };
                madeMaterials.Add(skin);
                madeMaterials.Add(eyes);
            }
            renderer.sharedMaterial = eye ? eyes : skin;
        }
        Part(PrimitiveType.Sphere, Vector3.zero, new Vector3(0.12f, 0.16f, 0.3f), Vector3.zero, false);
        Part(PrimitiveType.Cube, new Vector3(0f, 0f, -0.18f), new Vector3(0.02f, 0.11f, 0.11f), new Vector3(45f, 0f, 0f), false);
        Part(PrimitiveType.Sphere, new Vector3(0.05f, 0.03f, 0.09f), Vector3.one * 0.035f, Vector3.zero, true);
        Part(PrimitiveType.Sphere, new Vector3(-0.05f, 0.03f, 0.09f), Vector3.one * 0.035f, Vector3.zero, true);
    }

    // ---- Hello! --------------------------------------------------------------------------------------------------

    // Two little rising blips, "hel-lo", made here (no sound file needed).
    private void Say()
    {
        const int rate = 44100;
        const float note = 0.09f, gap = 0.04f;
        int length = Mathf.RoundToInt((note * 2f + gap) * rate);
        var samples = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = i / (float)rate;
            bool second = t > note + gap;
            float local = second ? t - note - gap : t;
            if (!second && t > note)
                continue;
            float k = local / note;
            float pitch = second ? Mathf.Lerp(700f, 1050f, k) : Mathf.Lerp(520f, 640f, k);
            float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k));
            samples[i] = Mathf.Sin(2f * Mathf.PI * pitch * local) * envelope * 0.35f;
        }
        blip = AudioClip.Create("Hello Fish Blip", length, 1, rate, false);
        blip.SetData(samples, 0);
        voice = gameObject.AddComponent<AudioSource>();
        voice.spatialBlend = 0f;
        voice.PlayOneShot(blip, 0.6f);
    }

    private void OnGUI()
    {
        if (!said || eye == null || PauseMenu.IsOpen)
            return;
        Camera camera = eye.GetComponent<Camera>();
        if (camera == null)
            return;
        Vector3 screen = camera.WorldToScreenPoint(transform.position + eye.up * (Length * 0.55f));
        if (screen.z <= 0f)
            return;

        float since = age - (InTime + 0.08f);
        float pop = since < 0.15f ? Mathf.Lerp(0f, 1.15f, since / 0.15f) : since < 0.25f ? Mathf.Lerp(1.15f, 1f, (since - 0.15f) / 0.1f) : 1f;
        float fade = leaving ? 1f - Mathf.Clamp01((age - leaveAt) / 0.2f) : 1f;
        if (pop <= 0.01f || fade <= 0f)
            return;

        float scale = Screen.height / 1080f * UIScale.Hud * pop;
        if (bubble == null)
            bubble = BubbleTexture();
        style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        style.font = GameFont.Font;
        style.fontSize = Mathf.Max(8, Mathf.RoundToInt(30f * scale));
        style.normal.textColor = new Color(0.05f, 0.12f, 0.18f);

        var content = new GUIContent("Hello!");
        Vector2 size = style.CalcSize(content);
        float width = size.x + 36f * scale, height = size.y + 18f * scale;
        float x = screen.x - width * 0.5f;
        float y = Screen.height - screen.y - height - 14f * scale;
        Color was = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.95f * fade);
        GUI.DrawTexture(new Rect(x, y, width, height), bubble);
        // The little tail under the bubble, pointing down at the fish.
        float tail = 12f * scale;
        for (int row = 0; row < Mathf.CeilToInt(tail); row++)
        {
            float half = (tail - row) * 0.5f;
            GUI.DrawTexture(new Rect(screen.x - half, y + height - 1f + row, half * 2f, 1f), Texture2D.whiteTexture);
        }
        GUI.color = new Color(1f, 1f, 1f, fade);
        GUI.Label(new Rect(x, y, width, height), content, style);
        GUI.color = was;
    }

    // A white rounded rectangle, stretched to any size (the corners stay round enough at these sizes).
    private static Texture2D BubbleTexture()
    {
        const int w = 96, h = 48, r = 20;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Max(0f, Mathf.Max(r - x - 0.5f, x + 0.5f - (w - r)));
                float dy = Mathf.Max(0f, Mathf.Max(r - y - 0.5f, y + 0.5f - (h - r)));
                float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }
}
