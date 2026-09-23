using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// How a main menu button feels, to match the pause menu: a quiet tick when the mouse comes onto it, a soft click when
// it is pressed (the theme's Hover Sound and Press Sound), it grows a little while the mouse is on it and dips while
// held, and its label turns the accent colour. All eased, in real time. Main Menu Controller adds it to its buttons.
public class MenuButtonFeel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float hoverScale = 1.05f;
    [SerializeField] private float pressScale = 0.97f;
    [SerializeField] private float speed = 14f;

    private static AudioSource audioSource;
    private static float lastTickAt = -1f;

    private PauseMenuTheme theme;
    private Text label;
    private Color labelColour;
    private Vector3 restScale;
    private bool hovered;
    private bool held;

    public void Setup(PauseMenuTheme menuTheme)
    {
        theme = menuTheme != null ? menuTheme : PauseMenuTheme.Default;
        label = GetComponentInChildren<Text>();
        if (label != null)
            labelColour = label.color;
        restScale = transform.localScale;
    }

    private void OnDisable()
    {
        hovered = held = false;
        if (restScale != Vector3.zero)
            transform.localScale = restScale;
        if (label != null)
            label.color = labelColour;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (Interactable && Time.unscaledTime - lastTickAt > 0.04f)
        {
            lastTickAt = Time.unscaledTime;
            Play(theme != null ? theme.hoverSound : null, theme != null ? theme.hoverVolume : 0f);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        held = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || !Interactable)
            return;
        held = true;
        Play(theme != null ? theme.pressSound : null, theme != null ? theme.pressVolume : 0f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        held = false;
    }

    private bool Interactable
    {
        get
        {
            Selectable selectable = GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }
    }

    private void Update()
    {
        if (theme == null)
            return;
        float k = 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime);
        float goal = held ? pressScale : (hovered ? hoverScale : 1f);
        transform.localScale = Vector3.Lerp(transform.localScale, restScale * goal, k);
        if (label != null)
        {
            Color wanted = hovered && Interactable ? Color.Lerp(labelColour, theme.accent, 0.75f) : labelColour;
            label.color = Color.Lerp(label.color, wanted, k);
        }
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f)
            return;
        if (audioSource == null)
        {
            var host = new GameObject("Menu sounds");
            DontDestroyOnLoad(host);
            audioSource = host.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
        }
        audioSource.pitch = Random.Range(0.96f, 1.04f);
        audioSource.PlayOneShot(clip, volume);
    }
}
