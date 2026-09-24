using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Finds the best IInteractable you are looking at, shows the 'Press E to ...' prompt, and triggers it on Interact.
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Transform interactOrigin;

    [Header("Interaction")]
    [SerializeField] private float interactRange = 2.2f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [Tooltip("If the reticle is just off something small (a key on the floor), a beam this wide, in metres, round the line of sight still finds it. 0 = only exactly what the dot is on.")]
    [SerializeField] private float aimAssist = 0.15f;

    [Header("Prompt")]
    [SerializeField] private Text promptLabel;
    [SerializeField] private string promptFormat = "Press E to {0}";
    [Tooltip("Optional: shown only while there is a target, e.g. the keycap + label group.")]
    [SerializeField] private GameObject promptRoot;
    [Tooltip("Prompt text colour on a lock you cannot open yet, or something that needs an item you do not have.")]
    [SerializeField] private Color blockedColor = new Color(1f, 0.36f, 0.32f);
    [Tooltip("Prompt text colour once you have the item it needs in your hotbar.")]
    [SerializeField] private Color readyColor = new Color(0.4f, 1f, 0.45f);
    [Tooltip("Dress the keycap prompt in the HUD style while its keycap is the plain white placeholder: a dark rounded pill, a light keycap showing the key Interact is really bound to (Keybindings), and the text in the HUD colours.")]
    [SerializeField] private bool styledPrompt = true;

    [Header("Outline")]
    [Tooltip("A white outline around whatever you are looking at (the inverted-hull shader in Resources/Shaders).")]
    [SerializeField] private bool outlineTarget = true;
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("Outline thickness in metres.")]
    [SerializeField] private float outlineThickness = 0.006f;
    [Tooltip("1 = the outline grows from the middle of each mesh (clean rims on boxes and simple props). 0 = along the surface normals (better for smooth, organic meshes).")]
    [SerializeField, Range(0f, 1f)] private float outlineCentreBias = 1f;

    [Header("Events")]
    public UnityEvent<GameObject> onTargetChanged = new UnityEvent<GameObject>();

    public IInteractable CurrentTarget { get; private set; }
    // While something else owns E (a pickup being inspected), no targeting and no interacting.
    public bool Busy { get; set; }
    public InputAction InteractAction => interactAction;
    // Hide the prompt without turning interaction off (a cutscene, the chase). E still works.
    public bool PromptHidden
    {
        get => promptHidden;
        set
        {
            if (promptHidden == value)
                return;
            promptHidden = value;
            RefreshPrompt();
        }
    }
    private bool promptHidden;

    private InputAction interactAction;
    private GameObject currentTargetObject;
    private string hint;
    private Color promptColor = Color.white;   // the label's own colour, for everything else
    private readonly RaycastHit[] rayHits = new RaycastHit[32];
    private Material outlineMaterial;
    // The outline material, for anything else that wants the same rim (a pickup being inspected).
    public Material OutlineMaterial => outlineMaterial;

    private void Awake()
    {
        interactAction = inputActions.FindActionMap("Player").FindAction("Interact");
        if (promptLabel != null)
            promptColor = promptLabel.color;
        if (styledPrompt)
            StylePrompt();
        SetupOutline();
        RefreshPrompt();   // hidden until there is something to say (it starts shown in the scene)
    }

    // ---- The prompt's look ---------------------------------------------------------------------------------------------

    private Text keyLabel;
    private LayoutElement keyElement;

    private void StylePrompt()
    {
        if (promptRoot == null)
            return;
        Transform keycap = promptRoot.transform.Find("Keycap");
        Image keyImage = keycap != null ? keycap.GetComponent<Image>() : null;
        if (keyImage == null || !(keyImage.sprite == null || keyImage.sprite.name.StartsWith("UI_White")))
            return;

        keyImage.sprite = HudStyle.Rounded;
        keyImage.type = Image.Type.Sliced;
        keyImage.pixelsPerUnitMultiplier = 2f;   // 6 px corners
        keyImage.color = HudStyle.KeyCap;
        keyElement = keycap.GetComponent<LayoutElement>();
        if (keyElement != null)
        {
            keyElement.preferredWidth = 30f;
            keyElement.preferredHeight = 30f;
        }
        keyLabel = keycap.GetComponentInChildren<Text>();
        if (keyLabel != null)
        {
            keyLabel.color = HudStyle.KeyInk;
            keyLabel.fontSize = 18;
            keyLabel.fontStyle = FontStyle.Normal;
            keyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // The pill behind keycap and text, and its hairline rim.
        var back = promptRoot.GetComponent<Image>();
        if (back == null)
            back = promptRoot.AddComponent<Image>();
        back.sprite = HudStyle.Pill;
        back.type = Image.Type.Sliced;
        back.color = HudStyle.Ink;
        back.raycastTarget = false;
        var rimObject = new GameObject("Rim", typeof(RectTransform));
        rimObject.transform.SetParent(promptRoot.transform, false);
        rimObject.transform.SetAsFirstSibling();
        rimObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var rim = rimObject.AddComponent<Image>();
        rim.sprite = HudStyle.PillRim;
        rim.type = Image.Type.Sliced;
        rim.color = HudStyle.Rim;
        rim.raycastTarget = false;
        var rimRect = rim.rectTransform;
        rimRect.anchorMin = Vector2.zero;
        rimRect.anchorMax = Vector2.one;
        rimRect.offsetMin = Vector2.zero;
        rimRect.offsetMax = Vector2.zero;

        var layout = promptRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.padding = new RectOffset(7, 18, 6, 6);
            layout.spacing = 10f;
        }
        if (promptLabel != null)
        {
            promptLabel.fontSize = 20;
            promptColor = HudStyle.Text;
            promptLabel.color = promptColor;
            if (promptLabel.GetComponent<Shadow>() == null)
            {
                var shadow = promptLabel.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                shadow.effectDistance = new Vector2(1f, -1.5f);
            }
        }
        blockedColor = HudStyle.Blocked;
        readyColor = HudStyle.Ready;
        RefreshKey();
    }

    // The keycap shows the key Interact is bound to (the first keyboard or mouse binding), so a rebind shows too.
    private void RefreshKey()
    {
        if (keyLabel == null || interactAction == null)
            return;
        string key = "E";
        for (int i = 0; i < interactAction.bindings.Count; i++)
        {
            InputBinding binding = interactAction.bindings[i];
            string path = binding.effectivePath;
            if (binding.isComposite || binding.isPartOfComposite || string.IsNullOrEmpty(path) || !(path.StartsWith("<Keyboard>") || path.StartsWith("<Mouse>")))
                continue;
            string shown = interactAction.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrEmpty(shown))
            {
                key = shown.ToUpperInvariant();
                break;
            }
        }
        if (keyLabel.text == key)
            return;
        keyLabel.text = key;
        if (keyElement != null)
            keyElement.preferredWidth = Mathf.Max(30f, keyLabel.preferredWidth + 16f);
    }

    private void OnEnable()
    {
        interactAction.performed += OnInteractPerformed;
    }

    private void OnDisable()
    {
        interactAction.performed -= OnInteractPerformed;
        SetTarget(null, null);
    }

    private void Update()
    {
        if (Busy)
        {
            if (currentTargetObject != null || CurrentTarget != null)
                SetTarget(null, null);
            return;
        }

        FindTarget(out var target, out var targetObject);
        if (targetObject == currentTargetObject && CurrentTarget != null)
            RefreshPrompt();   // the same target, but its prompt may have changed (a socket noticing what you now hold)
        if (targetObject != currentTargetObject)
            SetTarget(target, targetObject);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (Busy)
            return;
        CurrentTarget?.Interact(gameObject);
    }

    // What the reticle (the dot in the middle of the screen) is on: a ray straight out of the camera through it, and
    // the first thing it reaches within Interact Range. Something solid that is not interactable (a wall, the floor,
    // a ceiling) stops it, so nothing behind it can be used; triggers that are not interactable (room volumes, live
    // fish) are looked straight through. If the ray itself finds nothing, a beam Aim Assist wide round it tries again,
    // for small things the dot is just off.
    private void FindTarget(out IInteractable best, out GameObject bestObject)
    {
        Camera eye = Camera.main;
        Transform from = eye != null ? eye.transform : interactOrigin != null ? interactOrigin : transform;
        var ray = new Ray(from.position, from.forward);
        if (!FirstAlong(ray, 0f, out best, out bestObject) && aimAssist > 0f)
            FirstAlong(ray, aimAssist, out best, out bestObject);
    }

    // The first usable interactable along the ray (or beam), unless something solid comes first.
    private bool FirstAlong(Ray ray, float radius, out IInteractable best, out GameObject bestObject)
    {
        best = null;
        bestObject = null;
        int count = radius > 0f
            ? Physics.SphereCastNonAlloc(ray, radius, rayHits, interactRange, interactableMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastNonAlloc(ray, rayHits, interactRange, interactableMask, QueryTriggerInteraction.Collide);

        // Nearest first (a short insertion sort: there are only ever a few).
        for (int i = 1; i < count; i++)
        {
            RaycastHit hit = rayHits[i];
            int j = i - 1;
            while (j >= 0 && rayHits[j].distance > hit.distance)
            {
                rayHits[j + 1] = rayHits[j];
                j--;
            }
            rayHits[j + 1] = hit;
        }

        for (int i = 0; i < count; i++)
        {
            Collider hit = rayHits[i].collider;
            if (hit == null || PlayerBody.Is(hit) || hit.transform.IsChildOf(transform))
                continue;
            var interactable = hit.GetComponentInParent<IInteractable>();
            // A switched-off interactable (a fish that is still alive, a drawer that is done) is not a target.
            bool usable = interactable != null && !(interactable is Behaviour behaviour && !behaviour.isActiveAndEnabled);
            if (usable)
            {
                best = interactable;
                bestObject = ((Component)interactable).gameObject;
                return true;
            }
            if (!hit.isTrigger)
                return false;   // a wall, the floor: nothing behind it counts
        }
        return false;
    }

    private void SetTarget(IInteractable target, GameObject targetObject)
    {
        NotifyTargeted(currentTargetObject, false);
        OutlineHull.Hide(currentTargetObject);
        CurrentTarget = target;
        currentTargetObject = targetObject;
        NotifyTargeted(currentTargetObject, true);
        if (target != null)
            RefreshKey();
        if (outlineMaterial != null)
            OutlineHull.Show(currentTargetObject, outlineMaterial);

        RefreshPrompt();

        onTargetChanged.Invoke(targetObject);
    }

    // A line of help shown while nothing is targeted (a pickup being inspected: hold left mouse to turn it).
    public void ShowHint(string text)
    {
        hint = text;
        RefreshPrompt();
    }

    public void ClearHint()
    {
        hint = null;
        RefreshPrompt();
    }

    private void RefreshPrompt()
    {
        string text = promptHidden ? string.Empty : CurrentTarget != null ? string.Format(promptFormat, CurrentTarget.Prompt) : (hint ?? string.Empty);
        if (promptLabel != null)
        {
            promptLabel.text = text;
            PromptTone tone = !promptHidden && CurrentTarget is IPromptTone toned ? toned.Tone : PromptTone.Normal;
            promptLabel.color = tone == PromptTone.Blocked ? blockedColor : tone == PromptTone.Ready ? readyColor : promptColor;
        }
        if (promptRoot != null)
            promptRoot.SetActive(!string.IsNullOrWhiteSpace(text));   // never a keycap with nothing beside it
    }

    // The outline material, from the shader in Resources/Shaders. Tweak the fields above in Play mode and it follows.
    private void SetupOutline()
    {
        if (!outlineTarget)
        {
            outlineMaterial = null;
            return;
        }
        if (outlineMaterial == null)
        {
            Shader shader = Shader.Find("Out of the Depths/Outline Hull");
            if (shader == null)
            {
                Debug.LogWarning($"{name}: outline shader not found (Assets/Resources/Shaders/OutlineHull.shader), no outline on interactables.", this);
                return;
            }
            outlineMaterial = new Material(shader) { name = "Interactable Outline (runtime)" };
        }
        outlineMaterial.SetColor("_Color", outlineColor);
        outlineMaterial.SetFloat("_Thickness", outlineThickness);
        outlineMaterial.SetFloat("_CentreBias", outlineCentreBias);
    }

    private void OnValidate()
    {
        if (Application.isPlaying && outlineMaterial != null)
            SetupOutline();
    }

    private static void NotifyTargeted(GameObject target, bool targeted)
    {
        if (target == null)
            return;

        foreach (var listener in target.GetComponents<IInteractTargetListener>())
            listener.OnTargeted(targeted);
    }
}
