using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

// A looping video behind the main menu: a moving background, like a GIF. Drop a video into Clip (.mp4 or .webm
// imported as a Video Clip; turn a GIF into one of those first, Unity cannot play GIFs), or name a file in
// Assets/StreamingAssets for a big one that should stream. It plays under the title and buttons, covers the screen
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

    [Header("Look")]
    [Tooltip("Shown while the video loads and whenever there is no video.")]
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
            return;
        }
        player.isLooping = loop;
        player.playbackSpeed = speed;
        player.audioOutputMode = playAudio ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;
        player.Prepare();
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
