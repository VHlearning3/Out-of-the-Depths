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
    [SerializeField] private float interactRange = 2.5f;
    [SerializeField] private LayerMask interactableMask = ~0;
    [Tooltip("How much the target has to be in front of the camera. 0 = anywhere around you, 1 = dead centre.")]
    [SerializeField, Range(0f, 1f)] private float minLookAlignment = 0.3f;

    [Header("Prompt")]
    [SerializeField] private Text promptLabel;
    [SerializeField] private string promptFormat = "Press E to {0}";
    [Tooltip("Optional: shown only while there is a target, e.g. the keycap + label group.")]
    [SerializeField] private GameObject promptRoot;

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
    private readonly Collider[] overlapResults = new Collider[16];
    private Material outlineMaterial;
    // The outline material, for anything else that wants the same rim (a pickup being inspected).
    public Material OutlineMaterial => outlineMaterial;

    private void Awake()
    {
        interactAction = inputActions.FindActionMap("Player").FindAction("Interact");
        SetupOutline();
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

    private void FindTarget(out IInteractable best, out GameObject bestObject)
    {
        best = null;
        bestObject = null;
        float bestScore = float.MinValue;

        Vector3 origin = interactOrigin != null ? interactOrigin.position : transform.position;
        Vector3 forward = interactOrigin != null ? interactOrigin.forward : transform.forward;

        // Include triggers explicitly: checkpoint plates and other "walk-through" interactables use trigger colliders.
        int count = Physics.OverlapSphereNonAlloc(origin, interactRange, overlapResults, interactableMask, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            var interactable = overlapResults[i].GetComponentInParent<IInteractable>();
            if (interactable == null)
                continue;

            // A disabled interactable (e.g. a fish that is still alive) is not a valid target.
            if (interactable is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                continue;

            Vector3 toTarget = overlapResults[i].bounds.center - origin;
            float distance = toTarget.magnitude;
            float alignment = distance > 0.001f ? Vector3.Dot(forward, toTarget / distance) : 1f;
            if (alignment < minLookAlignment)
                continue;

            // Prefer what you're looking at, then what's closest.
            float score = alignment * 2f - distance / interactRange;
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = interactable;
            bestObject = ((Component)interactable).gameObject;
        }
    }

    private void SetTarget(IInteractable target, GameObject targetObject)
    {
        NotifyTargeted(currentTargetObject, false);
        OutlineHull.Hide(currentTargetObject);
        CurrentTarget = target;
        currentTargetObject = targetObject;
        NotifyTargeted(currentTargetObject, true);
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
            promptLabel.text = text;
        if (promptRoot != null)
            promptRoot.SetActive(text.Length > 0);
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
