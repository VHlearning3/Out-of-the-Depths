using UnityEngine;

// A 3D signpost (TestArena zone labels): fades in only when the player is within Show Distance and always turns to face them.
public class ProximityLabel : MonoBehaviour
{
    [SerializeField] private float showDistance = 12f;
    [Tooltip("Distance over which the text fades out past Show Distance.")]
    [SerializeField] private float fadeWidth = 4f;
    [SerializeField] private bool facePlayer = true;

    private TextMesh[] texts;
    private Color[] colors;
    private Renderer[] renderers;
    private CanvasGroup[] groups;
    private Transform player;

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

        if (Camera.main != null)
            player = Camera.main.transform;
    }

    private void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(player.position, transform.position);
        float alpha = 1f - Mathf.Clamp01((distance - showDistance) / Mathf.Max(0.01f, fadeWidth));

        for (int i = 0; i < texts.Length; i++)
        {
            Color color = colors[i];
            color.a *= alpha;
            texts[i].color = color;
            renderers[i].enabled = alpha > 0.01f;
        }
        foreach (CanvasGroup group in groups)
            group.alpha = alpha;

        if (!facePlayer || alpha <= 0f)
            return;

        // TextMesh reads correctly when its forward points away from the viewer.
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(away, Vector3.up);
    }
}
