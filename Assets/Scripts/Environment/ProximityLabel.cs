using UnityEngine;

// A 3D signpost (TestArena zone labels): fades in only when the player is within Show Distance and, if Face Player is on,
// turns smoothly (yaw only) to face them so the board reads from any side. Works with world-space canvases and TextMesh.
public class ProximityLabel : MonoBehaviour
{
    [SerializeField] private float showDistance = 16f;
    [Tooltip("Distance over which the text fades out past Show Distance.")]
    [SerializeField] private float fadeWidth = 5f;
    [SerializeField] private bool facePlayer = true;
    [Tooltip("How quickly the sign turns toward the player, per second. 0 = snap.")]
    [SerializeField] private float turnSpeed = 4f;

    [Header("Floating")]
    [Tooltip("How far the sign drifts up and down, in metres. 0 = anchored.")]
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobFrequency = 0.25f;
    [Tooltip("Slow lean, in degrees, as if nudged by the current.")]
    [SerializeField] private float leanDegrees = 2.5f;

    private Vector3 restPosition;
    private float bobPhase;
    private TextMesh[] texts;
    private Color[] colors;
    private Renderer[] renderers;
    private CanvasGroup[] groups;
    private Transform player;
    private float alpha = -1f;

    private void Start()
    {
        texts = GetComponentsInChildren<TextMesh>(true);
        colors = new Color[texts.Length];
        renderers = new Renderer[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            colors[i] = texts[i].color;
            renderers[i] = texts[i].GetComponent<Renderer>();
        }
        groups = GetComponentsInChildren<CanvasGroup>(true);
        restPosition = transform.position;
        bobPhase = Random.value * 10f;
        facing = transform.rotation;

        if (Camera.main != null)
            player = Camera.main.transform;
    }

    // Reads the sign's resting spot again, e.g. after it has been moved by hand in Play mode.
    public void ResetRestPosition()
    {
        restPosition = transform.position;
    }

    private void Update()
    {
        if (player == null)
        {
            if (Camera.main == null)
                return;
            player = Camera.main.transform;
        }

        Vector3 toPlayer = player.position - restPosition;
        float distance = toPlayer.magnitude;
        float target = 1f - Mathf.Clamp01((distance - showDistance) / Mathf.Max(0.01f, fadeWidth));
        if (!Mathf.Approximately(target, alpha))
        {
            alpha = target;
            ApplyAlpha();
        }

        if (alpha <= 0f)
            return;

        float t = Time.time + bobPhase;
        transform.position = restPosition + Vector3.up * (Mathf.Sin(t * bobFrequency * 2f * Mathf.PI) * bobAmplitude);
        Quaternion lean = Quaternion.Euler(Mathf.Sin(t * 0.37f) * leanDegrees, 0f, Mathf.Sin(t * 0.23f) * leanDegrees);

        if (!facePlayer)
        {
            transform.rotation = transform.rotation * Quaternion.Inverse(lastLean) * lean;
            lastLean = lean;
            return;
        }

        // The canvases are laid out so the front face reads when the sign's forward points away from the viewer.
        Vector3 away = -toPlayer;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            return;

        Quaternion look = Quaternion.LookRotation(away, Vector3.up);
        facing = turnSpeed > 0f ? Quaternion.Slerp(facing, look, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime)) : look;
        transform.rotation = facing * lean;
        lastLean = lean;
    }

    private Quaternion facing = Quaternion.identity;
    private Quaternion lastLean = Quaternion.identity;

    private void ApplyAlpha()
    {
        for (int i = 0; i < texts.Length; i++)
        {
            Color color = colors[i];
            color.a *= alpha;
            texts[i].color = color;
            renderers[i].enabled = alpha > 0.01f;
        }
        foreach (CanvasGroup group in groups)
            group.alpha = alpha;
    }
}
