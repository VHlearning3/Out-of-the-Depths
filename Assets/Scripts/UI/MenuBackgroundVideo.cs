using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// A looping video behind the main menu: a moving background, like a GIF. Drop a video into Clip (.mp4 or .webm
// imported as a Video Clip), or name a file in Assets/StreamingAssets for a big one that should stream, or give it
// Frames: a GIF's frames as pictures (Unity cannot play a GIF itself), shown one after another at Frames Per Second,
// round and round. Lighter still (the main menu's, Noora's animation in Art/UI/MainMenu): Still Image is the whole
// picture once, and Moving Frames only the part that moves, cut out of every frame and laid over it at Moving Area,
// each fading smoothly into the next (Crossfade) instead of jumping; one full picture and twelve small ones instead of
// twelve full ones, about a third of the memory, and no video (videos are compressed and awkward on some platforms,
// WebGL above all). Breathe zooms the whole background in and out very slowly. It plays under the title and buttons, covers the screen
// and is dimmed so the text stays readable; Still Image shows while it loads and whenever there is no video, and
// with neither the plain Background panel shows. Everything it needs (the RawImage, the aspect fitter, the dimming
// layer and the VideoPlayer) is made at runtime, so the scene only carries this component. Lives under the menu
// Canvas, after the Background panel and before the title. Swap the video any time: change Clip in the Inspector, or
// call SetClip from a script.
[RequireComponent(typeof(RectTransform))]
public class MenuBackgroundVideo : MonoBehaviour
{
    [Header("Video")]
    [Tooltip("The video to loop (.mp4 or .webm, imported as a Video Clip). Empty = the still image, or the plain background.")]
    [SerializeField] private VideoClip clip;
    [Tooltip("Or a file in Assets/StreamingAssets, e.g. menu.mp4, used when Clip is empty. Better for big videos: it streams instead of loading whole.")]
    [SerializeField] private string streamingAssetsFile = "";
    [SerializeField] private bool loop = true;
    [SerializeField, Range(0.25f, 2f)] private float speed = 1f;
    [Tooltip("Play the sound in the video. Off = silent, the usual for a menu.")]
    [SerializeField] private bool playAudio = false;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;

    [Header("Frames (a GIF's pictures)")]
    [Tooltip("Shown one after another, round and round, when there is no video. Empty = no frames.")]
    [SerializeField] private Texture[] frames = new Texture[0];
    [Tooltip("How many frames a second (the GIF's own pace: 2 = half a second each).")]
    [SerializeField, Min(0.1f)] private float framesPerSecond = 2f;

    [Header("Moving part (a GIF cut up)")]
    [Tooltip("Only the part of the picture that moves, one picture per frame, laid over Still Image at Moving Area and played at Frames Per Second. Empty = none.")]
    [SerializeField] private Texture[] movingFrames = new Texture[0];
    [Tooltip("Where the moving part sits on Still Image, in its pixels from the top left.")]
    [SerializeField] private RectInt movingArea = new RectInt(1048, 192, 632, 752);
    [Tooltip("Still Image's own size in pixels, as drawn (Moving Area is measured on it), whatever size it was imported at.")]
    [SerializeField] private Vector2Int pictureSize = new Vector2Int(1920, 1080);
    [Tooltip("How much of each frame is spent fading into the next: 0 = hard cuts like the GIF, 1 = always fading.")]
    [SerializeField, Range(0f, 1f)] private float crossfade = 0.75f;
    [Tooltip("A very slow zoom in and out of the whole background (0.02 = 2 %; 0 = still).")]
    [SerializeField, Range(0f, 0.1f)] private float breathe = 0.02f;
    [SerializeField, Min(1f)] private float breatheSeconds = 24f;

    [Header("Look")]
    [Tooltip("Shown while the video loads and whenever there is no video or frames.")]
    [SerializeField] private Texture stillImage;
    [Tooltip("Cover the whole screen (the edges get cropped) rather than fit inside it (bars at the sides).")]
    [SerializeField] private bool coverScreen = true;
    [Tooltip("Darkening over the picture so the menu text stays readable.")]
    [SerializeField, Range(0f, 1f)] private float dim = 0.4f;
    [SerializeField] private Color dimColor = Color.black;

    private RawImage image;
    private AspectRatioFitter fitter;
    private Image dimImage;
    private VideoPlayer player;
    private RenderTexture texture;
    private bool flipping;   // showing Frames
    private float flipStart;
    private RawImage moving, movingNext;
    private bool movingOn;
    private float movingStart;

    private void Awake()
    {
        Build();
    }

    private void OnEnable()
    {
        if (image != null)
            Show();
    }

    private void OnDisable()
    {
        if (player != null)
            player.Stop();
    }

    private void OnDestroy()
    {
        if (player != null)
        {
            player.prepareCompleted -= OnPrepared;
            player.errorReceived -= OnError;
        }
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying && image != null)
            ApplyLook();
    }

    // Change the video from a script (null = back to the still image).
    public void SetClip(VideoClip newClip)
    {
        clip = newClip;
        streamingAssetsFile = "";
        if (image != null && isActiveAndEnabled)
            Show();
    }

    private void Build()
    {
        Stretch((RectTransform)transform);
        image = GetComponent<RawImage>();
        if (image == null)
            image = gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.color = Color.white;
        fitter = GetComponent<AspectRatioFitter>();
        if (fitter == null)
            fitter = gameObject.AddComponent<AspectRatioFitter>();

        moving = Layer("Moving", 0);
        movingNext = Layer("MovingNext", 1);

        Transform dimChild = transform.Find("Dim");
        if (dimChild == null)
        {
            var dimObject = new GameObject("Dim", typeof(RectTransform));
            dimObject.transform.SetParent(transform, false);
            dimChild = dimObject.transform;
        }
        dimImage = dimChild.GetComponent<Image>();
        if (dimImage == null)
            dimImage = dimChild.gameObject.AddComponent<Image>();
        dimImage.raycastTarget = false;
        Stretch((RectTransform)dimChild);

        player = GetComponent<VideoPlayer>();
        if (player == null)
            player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.aspectRatio = VideoAspectRatio.Stretch;   // the fitter gives it its shape on screen
        player.waitForFirstFrame = true;
        player.skipOnDrop = true;
        player.prepareCompleted += OnPrepared;
        player.errorReceived += OnError;
    }

    // The still image straight away, then the video once it is ready.
    private void Show()
    {
        ApplyLook();
        player.Stop();
        flipping = false;
        StopMoving();
        image.texture = stillImage;
        image.enabled = stillImage != null;
        if (stillImage != null)
            fitter.aspectRatio = (float)stillImage.width / Mathf.Max(1, stillImage.height);

        if (clip != null)
        {
            player.source = VideoSource.VideoClip;
            player.clip = clip;
        }
        else if (!string.IsNullOrEmpty(streamingAssetsFile))
        {
            player.source = VideoSource.Url;
            player.url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingAssetsFile);
        }
        else
        {
            StartFrames();
            if (!flipping)
                StartMoving();
            return;
        }
        player.isLooping = loop;
        player.playbackSpeed = speed;
        player.audioOutputMode = playAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;
        player.Prepare();
    }

    // The frames instead of a video, if there are any.
    private void StartFrames()
    {
        if (frames == null || frames.Length == 0 || frames[0] == null)
            return;
        flipping = true;
        flipStart = Time.unscaledTime;
        image.texture = frames[0];
        image.enabled = true;
        fitter.aspectRatio = (float)frames[0].width / Mathf.Max(1, frames[0].height);
    }

    // A RawImage over the picture (under the dimming), off until it is needed.
    private RawImage Layer(string name, int order)
    {
        Transform child = transform.Find(name);
        if (child == null)
        {
            child = new GameObject(name, typeof(RectTransform)).transform;
            child.SetParent(transform, false);
        }
        child.SetSiblingIndex(order);
        RawImage layer = child.GetComponent<RawImage>();
        if (layer == null)
            layer = child.gameObject.AddComponent<RawImage>();
        layer.raycastTarget = false;
        layer.enabled = false;
        return layer;
    }

    // The moving part over the still picture, where Moving Area says (it scales with the picture).
    private void StartMoving()
    {
        if (movingFrames == null || movingFrames.Length == 0 || movingFrames[0] == null || stillImage == null)
            return;
        float w = pictureSize.x > 0 ? pictureSize.x : stillImage.width, h = pictureSize.y > 0 ? pictureSize.y : stillImage.height;
        foreach (RawImage layer in new[] { moving, movingNext })
        {
            RectTransform rect = layer.rectTransform;
            rect.anchorMin = new Vector2(movingArea.x / w, 1f - (movingArea.y + movingArea.height) / h);
            rect.anchorMax = new Vector2((movingArea.x + movingArea.width) / w, 1f - movingArea.y / h);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            layer.enabled = true;
        }
        moving.texture = movingFrames[0];
        movingNext.texture = movingFrames[1 % movingFrames.Length];
        movingNext.color = new Color(1f, 1f, 1f, 0f);
        movingOn = true;
        movingStart = Time.unscaledTime;
    }

    private void StopMoving()
    {
        movingOn = false;
        if (moving != null)
            moving.enabled = false;
        if (movingNext != null)
            movingNext.enabled = false;
    }

    private void Update()
    {
        // The slow zoom: in and out over Breathe Seconds, from the middle.
        if (breathe > 0f)
        {
            float wave = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / breatheSeconds);
            transform.localScale = Vector3.one * (1f + breathe * wave);
        }

        // The moving part: frame i, fading into the next over the last Crossfade of its time.
        if (movingOn)
        {
            float t = (Time.unscaledTime - movingStart) * framesPerSecond * speed;
            int n = movingFrames.Length;
            int i = Mathf.FloorToInt(t) % n;
            float into = t - Mathf.Floor(t);
            float blend = crossfade <= 0f ? 0f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((into - (1f - crossfade)) / crossfade));
            Texture now = movingFrames[i], next = movingFrames[(i + 1) % n];
            if (moving.texture != now)
                moving.texture = now;
            if (movingNext.texture != next)
                movingNext.texture = next;
            movingNext.color = new Color(1f, 1f, 1f, blend);
        }

        if (!flipping || frames.Length == 0)
            return;
        int index = Mathf.FloorToInt((Time.unscaledTime - flipStart) * framesPerSecond * speed) % frames.Length;
        Texture frame = frames[index];
        if (frame != null && image.texture != frame)
            image.texture = frame;
    }

    private void OnPrepared(VideoPlayer source)
    {
        int width = Mathf.Max(2, (int)source.width);
        int height = Mathf.Max(2, (int)source.height);
        if (texture == null || texture.width != width || texture.height != height)
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
            texture = new RenderTexture(width, height, 0) { name = "Menu background video" };
        }
        source.targetTexture = texture;
        StopMoving();
        if (playAudio)
            for (ushort track = 0; track < source.audioTrackCount; track++)
                source.SetDirectAudioVolume(track, volume);
        image.texture = texture;
        image.enabled = true;
        fitter.aspectRatio = (float)width / height;
        source.Play();
    }

    private void OnError(VideoPlayer source, string message)
    {
        Debug.LogWarning("Menu background video: " + message, this);
        image.texture = stillImage;
        image.enabled = stillImage != null;
        StartFrames();
    }

    private void ApplyLook()
    {
        fitter.aspectMode = coverScreen ? AspectRatioFitter.AspectMode.EnvelopeParent : AspectRatioFitter.AspectMode.FitInParent;
        dimImage.color = new Color(dimColor.r, dimColor.g, dimColor.b, dim);
        if (player != null)
        {
            player.isLooping = loop;
            player.playbackSpeed = speed;
        }
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
