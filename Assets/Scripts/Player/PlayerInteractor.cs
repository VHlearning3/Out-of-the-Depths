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

    [Header("Events")]
    public UnityEvent<GameObject> onTargetChanged = new UnityEvent<GameObject>();

    public IInteractable CurrentTarget { get; private set; }

    private InputAction interactAction;
    private GameObject currentTargetObject;
    private readonly Collider[] overlapResults = new Collider[16];

    private void Awake()
    {
        interactAction = inputActions.FindActionMap("Player").FindAction("Interact");
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
        FindTarget(out var target, out var targetObject);
        if (targetObject != currentTargetObject)
            SetTarget(target, targetObject);
    }

    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
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
        CurrentTarget = target;
        currentTargetObject = targetObject;
        NotifyTargeted(currentTargetObject, true);

        if (promptLabel != null)
            promptLabel.text = target != null ? string.Format(promptFormat, target.Prompt) : string.Empty;
        if (promptRoot != null)
            promptRoot.SetActive(target != null);

        onTargetChanged.Invoke(targetObject);
    }

    private static void NotifyTargeted(GameObject target, bool targeted)
    {
        if (target == null)
            return;

        foreach (var listener in target.GetComponents<IInteractTargetListener>())
            listener.OnTargeted(targeted);
    }
}
