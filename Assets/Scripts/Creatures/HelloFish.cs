using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The easter egg: now and then, as a pickup comes up to be inspected, a little fish darts in from the side of the
// screen, stops beside the item, turns to you and says "Hello!" in a speech bubble, then shoots off across the view
// and is gone. While it is there it can be touched with the cursor: rub the cursor over it to pet it (it wiggles
// happily, hearts float up and it says something sweet), or click it to poke it (it flinches, puffs up and complains;
// enough pokes and it swims off in a huff). It stays while the cursor is on it, and a press that starts on it never
// turns the item. Chance, timings, lines, the heart and the sounds are on Assets/Resources/HelloFish.asset
// (HelloFishSettings); Always (the admin page switch) makes it come on every inspect. It looks like the fish in the
// scene (a copy of one's meshes, nothing else of it) or, with no fish around, a small made-up one. Taking the item
// early sends it off at once. It makes itself.
public class HelloFish : MonoBehaviour
{
    // How often an inspect brings it, 0..1 (Chance on the settings asset).
    public static float Chance => HelloFishSettings.Current.chance;
    // Testing (admin page): every inspect.
    public static bool Always { get; set; }

    // True while a mouse press that began on the fish is held: the inspect view does not turn the item meanwhile.
    public static bool HasMouse => current != null && current.Claims();

    private const float InTime = 0.45f;      // darting in
    private const float OutTime = 0.5f;      // shooting off
    private const float Length = 0.28f;      // nose to tail, in metres
    private const float PokeSlop = 12f;      // pixels at 1080p a click may move and still be a poke
    private const float BubbleTail = 12f;    // the speech bubble's little tail, pixels at 1080p

    private static HelloFish current;

    private HelloFishSettings settings;
    private Transform eye;
    private Camera eyeCamera;
    private Transform item;
    private float side;              // +1 = comes in from the right, -1 = from the left
    private float age;
    private float leaveAt;
    private Vector3 leaveFrom;       // camera space, where it was when it started to leave
    private bool leaving;
    private bool huffy;             // poked too often: leaving, and no touch keeps it
    private Vector3 lastPosition;
    private readonly List<Material> madeMaterials = new List<Material>();
    private Quaternion heading;      // where it faces, before the tail beat
    private GUIStyle style;
    private Texture2D bubble;
    private Texture2D heart;
    private bool madeHeart;
    private AudioSource voice;
    private AudioClip blip;
    private bool said;

    // What the bubble says, since when (it pops again on every new line), and whether it is a complaint.
    private string line;
    private float lineAt;
    private bool complaining;

    // Touching.
    private int pressFrame = -1;
    private bool pressOnFish;       // the current press began on the fish
    private float pressAt;          // unscaled time of that press
    private float pressMoved;       // pixels at 1080p the cursor moved during it
    private Vector2 lastMouse;
    private bool hadMouse;
    private float rubbed;           // pixels at 1080p rubbed over it since the last heart
    private int pets, pokes;
    private float happy;            // 0..1: up on every heart, fading
    private float puff, puffShown;  // 0..1: up on a poke, fading
    private Vector3 flinch, flinchVelocity;   // camera space, springing back

    private struct Heart
    {
        public Vector2 at;      // GUI pixels where it popped out
        public float born, size, sway, tilt;
    }
    private readonly List<Heart> hearts = new List<Heart>();

    public static void Maybe(Camera camera, Transform heldItem)
    {
        if (current != null || camera == null || heldItem == null)
            return;
        HelloFishSettings settings = HelloFishSettings.Current;
        if (!Always && Random.value >= settings.chance)
            return;
        var fish = new GameObject("HelloFish").AddComponent<HelloFish>();
        fish.settings = settings;
        fish.eye = camera.transform;
        fish.eyeCamera = camera;
        fish.item = heldItem;
        fish.side = Random.value < 0.5f ? -1f : 1f;
        fish.leaveAt = InTime + settings.staySeconds;
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
        if (madeHeart && heart != null)
            Destroy(heart);
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
            local.y += Mathf.Abs(Mathf.Sin(age * 8f)) * 0.014f * happy;   // little happy hops while petted
            wiggle = 4f + 10f * happy;
            greeting = true;
        }

        Vector3 position = eye.TransformPoint(local + flinch);
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
        Quaternion wriggle = Quaternion.Euler(0f, Mathf.Sin(age * 70f) * 12f * puff, Mathf.Sin(age * 8f) * 16f * happy);
        transform.rotation = heading * Wiggle(wiggle) * wriggle;

        // Puffed up when poked: swells in a blink, sinks back slowly.
        puffShown = Mathf.Lerp(puffShown, puff, 1f - Mathf.Exp(-30f * Time.deltaTime));
        transform.localScale = Vector3.one * (1f + settings.pokePuff * puffShown);

        if (greeting && !said && age >= InTime + 0.08f)
        {
            said = true;
            SayHello();
        }
    }

    // The tail beat: a quick side-to-side yaw, faster the harder it swims.
    private float wigglePhase;
    private Quaternion Wiggle(float degrees)
    {
        return Quaternion.Euler(0f, Mathf.Sin(wigglePhase) * degrees, 0f);
    }

    private void Update()
    {
        wigglePhase += Time.deltaTime * (leaving || age < InTime ? 22f : 10f + 12f * happy);
        Touch();

        float dt = Time.deltaTime;
        happy = Mathf.Max(0f, happy - dt * 0.6f);
        puff = Mathf.Max(0f, puff - dt * 1.5f);
        flinchVelocity += (-flinch * 160f - flinchVelocity * 14f) * dt;   // a spring back into place
        flinch += flinchVelocity * dt;

        float heartSeconds = Mathf.Max(0.2f, settings.heartSeconds);
        for (int i = hearts.Count - 1; i >= 0; i--)
            if (age - hearts[i].born >= heartSeconds)
                hearts.RemoveAt(i);
    }

    // ---- Petting and poking --------------------------------------------------------------------------------------

    // Can the cursor touch it now: it has said hello and is not leaving, nothing is paused and the cursor shows.
    private bool Touchable => said && !leaving && !PauseMenu.IsOpen && Cursor.visible && eyeCamera != null;

    private void Touch()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;
        Vector2 at = mouse.position.ReadValue();
        SyncPress(mouse, at);

        float toRef = 1080f / Mathf.Max(1, Screen.height);   // screen pixels to pixels at 1080p
        float step = hadMouse ? Mathf.Min((at - lastMouse).magnitude * toRef, 80f) : 0f;   // no huge jumps
        lastMouse = at;
        hadMouse = true;
        bool pressed = mouse.leftButton.isPressed;
        if (pressOnFish && pressed)
            pressMoved += step;

        if (!Touchable)
        {
            pressOnFish &= pressed;
            return;
        }
        bool over = Over(at);
        if (over)
            Linger();

        // Petting: rubbing the cursor over it, with the button up or pressed on the fish (not while turning the item).
        if (over && (!pressed || pressOnFish) && step > 0f)
        {
            rubbed += step;
            if (rubbed >= settings.rubPerHeart)
            {
                rubbed -= settings.rubPerHeart;
                Pet();
            }
        }

        // Poking: a short click on it, hardly moving.
        if (mouse.leftButton.wasReleasedThisFrame && pressOnFish)
        {
            if (Time.unscaledTime - pressAt <= settings.pokeSeconds && pressMoved <= PokeSlop)
                Poke(at);
            pressOnFish = false;
        }
    }

    // Works out, once per press, whether the press began on the fish. Called from Update and from HasMouse, so the
    // inspect view gets the answer on the very frame of the press whichever runs first.
    private void SyncPress(Mouse mouse, Vector2 at)
    {
        if (!mouse.leftButton.wasPressedThisFrame || pressFrame == Time.frameCount)
            return;
        pressFrame = Time.frameCount;
        pressOnFish = Touchable && Over(at);
        pressAt = Time.unscaledTime;
        pressMoved = 0f;
    }

    private bool Claims()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return false;
        SyncPress(mouse, mouse.position.ReadValue());
        return pressOnFish && mouse.leftButton.isPressed;
    }

    // Is this screen point (pixels, bottom-left origin) on the fish: near the line from its nose to its tail, as wide
    // as its body plus a little slack, so it is easy to hit whichever way it faces.
    private bool Over(Vector2 at)
    {
        Vector3 middle = eyeCamera.WorldToScreenPoint(transform.position);
        if (middle.z <= 0.01f)
            return false;
        float half = Length * 0.5f * transform.localScale.x;
        Vector2 nose = eyeCamera.WorldToScreenPoint(transform.position + transform.forward * half);
        Vector2 tail = eyeCamera.WorldToScreenPoint(transform.position - transform.forward * half);
        float pixelsPerMetre = Screen.height / (2f * middle.z * Mathf.Tan(eyeCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        float radius = half * 0.65f * pixelsPerMetre + 14f * Screen.height / 1080f;
        Vector2 along = tail - nose;
        float k = along.sqrMagnitude > 1e-4f ? Mathf.Clamp01(Vector2.Dot(at - nose, along) / along.sqrMagnitude) : 0f;
        return Vector2.Distance(at, nose + along * k) <= radius;
    }

    // Touched: it stays a while longer (unless it is leaving in a huff).
    private void Linger()
    {
        if (!leaving && !huffy)
            leaveAt = Mathf.Max(leaveAt, age + settings.stayAfterTouch);
    }

    private void Pet()
    {
        pets++;
        happy = 1f;
        string[] lines = settings.petLines;
        if (lines != null && lines.Length > 0)
            Say(lines[(pets - 1) % lines.Length], false);
        SpawnHeart(1f);
        if (Random.value < 0.45f)
            SpawnHeart(0.6f);
        Play(settings.petSound);
        Linger();
    }

    private void Poke(Vector2 at)
    {
        pokes++;
        rubbed = 0f;
        happy = 0f;
        puff = 1f;
        // Jolts away from the finger (and back a little), then springs back into place.
        Vector2 fishOnScreen = eyeCamera.WorldToScreenPoint(transform.position);
        Vector2 away = (fishOnScreen - at).sqrMagnitude > 1f ? (fishOnScreen - at).normalized : new Vector2(side, 0f);
        flinchVelocity += new Vector3(away.x * 0.55f, away.y * 0.55f, 0.8f);
        string[] lines = settings.pokeLines;
        if (lines != null && lines.Length > 0)
            Say(lines[Mathf.Min(pokes - 1, lines.Length - 1)], true);
        Play(settings.pokeSound);
        if (settings.leaveAfterPokes > 0 && pokes >= settings.leaveAfterPokes)
        {
            huffy = true;
            leaveAt = Mathf.Min(leaveAt, age + 1.2f);   // says its last word, then off it goes
        }
        else
        {
            Linger();
        }
    }

    private void Say(string text, bool complaint)
    {
        line = text;
        lineAt = age;
        complaining = complaint;
    }

    private void SpawnHeart(float sizeFactor)
    {
        Vector3 top = eyeCamera.WorldToScreenPoint(transform.position + eye.up * (Length * 0.3f));
        if (top.z <= 0f)
            return;
        float scale = Screen.height / 1080f * UIScale.Hud;
        hearts.Add(new Heart
        {
            at = new Vector2(top.x + side * Random.Range(20f, 48f) * scale, Screen.height - top.y),   // its outer side
            born = age,
            size = Random.Range(settings.heartSize.x, settings.heartSize.y) * sizeFactor,
            sway = Random.Range(0f, Mathf.PI * 2f),
            tilt = Random.Range(-16f, 16f),
        });
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
            return;
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.playOnAwake = false;
        }
        SoundVariety.OneShot(voice, clip, settings.volume);
    }

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

    private void SayHello()
    {
        Say(settings.helloText, false);
        if (settings.helloSound != null)
        {
            Play(settings.helloSound);
            return;
        }

        // No hello sound on the asset: two little rising blips, "hel-lo", made here.
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
        if (voice == null)
        {
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            voice.playOnAwake = false;
        }
        voice.PlayOneShot(blip, 0.6f);
    }

    private void OnGUI()
    {
        if (!said || eye == null || eyeCamera == null || PauseMenu.IsOpen)
            return;
        float scale = Screen.height / 1080f * UIScale.Hud;
        float fade = leaving ? 1f - Mathf.Clamp01((age - leaveAt) / 0.2f) : 1f;
        Color was = GUI.color;
        DrawHearts(scale);   // behind the bubble, so what it says stays readable
        DrawBubble(scale, fade);
        GUI.color = was;
    }

    // The speech bubble over its head: pops in on every new line; a complaint is written in red and shakes.
    private void DrawBubble(float scale, float fade)
    {
        if (string.IsNullOrEmpty(line) || fade <= 0f)
            return;
        Vector3 screen = eyeCamera.WorldToScreenPoint(transform.position + eye.up * (Length * 0.55f * transform.localScale.x));
        if (screen.z <= 0f)
            return;
        float since = age - lineAt;
        float pop = since < 0.15f ? Mathf.Lerp(0f, 1.15f, since / 0.15f) : since < 0.25f ? Mathf.Lerp(1.15f, 1f, (since - 0.15f) / 0.1f) : 1f;
        if (pop <= 0.01f)
            return;
        scale *= pop;

        if (bubble == null)
            bubble = BubbleTexture();
        style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        style.font = GameFont.Font;
        style.fontSize = Mathf.Max(8, Mathf.RoundToInt(30f * scale));
        style.normal.textColor = complaining ? settings.complainColor : new Color(0.05f, 0.12f, 0.18f);

        float shake = complaining ? Mathf.Sin(age * 90f) * 5f * scale * Mathf.Clamp01(1f - since / 0.35f) : 0f;
        float centre = screen.x + shake;
        var content = new GUIContent(line);
        Vector2 size = style.CalcSize(content);
        float width = size.x + 36f * scale, height = size.y + 18f * scale;
        float x = centre - width * 0.5f;
        float y = Screen.height - screen.y - height - 14f * scale;
        GUI.color = new Color(1f, 1f, 1f, 0.95f * fade);
        GUI.DrawTexture(new Rect(x, y, width, height), bubble);
        // The little tail under the bubble, pointing down at the fish.
        float tail = BubbleTail * scale;
        for (int row = 0; row < Mathf.CeilToInt(tail); row++)
        {
            float half = (tail - row) * 0.5f;
            GUI.DrawTexture(new Rect(centre - half, y + height - 1f + row, half * 2f, 1f), Texture2D.whiteTexture);
        }
        GUI.color = new Color(1f, 1f, 1f, fade);
        GUI.Label(new Rect(x, y, width, height), content, style);
    }

    // Hearts from petting: each pops out of the fish's outer side, floats up and away from the item (past the speech
    // bubble) with a sway, and fades.
    private void DrawHearts(float scale)
    {
        if (hearts.Count == 0 || Event.current.type != EventType.Repaint)
            return;
        if (heart == null)
        {
            heart = settings.heartPicture;
            if (heart == null)
            {
                heart = HeartTexture(settings.heartColor);
                madeHeart = true;
            }
        }
        float seconds = Mathf.Max(0.2f, settings.heartSeconds);
        Matrix4x4 matrix = GUI.matrix;
        foreach (Heart h in hearts)
        {
            float since = age - h.born;
            float t = Mathf.Clamp01(since / seconds);
            float pop = since < 0.12f ? Mathf.Lerp(0.2f, 1.25f, since / 0.12f) : since < 0.24f ? Mathf.Lerp(1.25f, 1f, (since - 0.12f) / 0.12f) : 1f;
            float rise = settings.heartRise * scale * (1f - (1f - t) * (1f - t));   // quick at first, slowing
            float x = h.at.x + Mathf.Sin(h.sway + t * 7f) * 10f * scale + side * rise * 0.55f;
            float y = h.at.y - rise;
            float size = h.size * scale * pop;
            float alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            GUIUtility.RotateAroundPivot(h.tilt + Mathf.Sin(h.sway + t * 5f) * 8f, new Vector2(x, y));
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTexture(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), heart);
            GUI.matrix = matrix;
        }
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

    // A heart in `color` with a darker rim and a soft shine on the left lobe, smooth-edged (4 x 4 samples a pixel).
    private static Texture2D HeartTexture(Color color)
    {
        const int size = 64, samples = 4;
        const float extent = 1.32f, middle = 0.12f, rimScale = 1.16f;
        Color rim = Color.Lerp(color, Color.black, 0.45f);
        rim.a = 1f;
        Color shine = Color.Lerp(color, Color.white, 0.75f);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        var pixels = new Color[size * size];
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float outer = 0f, fill = 0f, gloss = 0f;
                for (int sy = 0; sy < samples; sy++)
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float u = ((px + (sx + 0.5f) / samples) / size - 0.5f) * 2f * extent;
                        float v = ((py + (sy + 0.5f) / samples) / size - 0.5f) * 2f * extent + middle;
                        if (!InHeart(u, v))
                            continue;
                        outer++;
                        if (!InHeart(u * rimScale, (v - middle) * rimScale + middle))
                            continue;
                        fill++;
                        // The shine: a small tilted oval on the upper left lobe.
                        float gx = u + 0.5f, gy = v - 0.62f;
                        float c = Mathf.Cos(0.6f), s = Mathf.Sin(0.6f);
                        float rx = gx * c + gy * s, ry = -gx * s + gy * c;
                        if (rx * rx / (0.2f * 0.2f) + ry * ry / (0.11f * 0.11f) <= 1f)
                            gloss++;
                    }
                float total = samples * samples;
                Color col = rim;
                if (fill > 0f)
                    col = Color.Lerp(rim, Color.Lerp(color, shine, gloss / fill * 0.85f), fill / outer);
                col.a = outer / total;
                pixels[py * size + px] = col;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // The classic heart curve, (u^2 + v^2 - 1)^3 - u^2 v^3 <= 0, v up.
    private static bool InHeart(float u, float v)
    {
        float a = u * u + v * v - 1f;
        return a * a * a - u * u * v * v * v <= 0f;
    }
}
