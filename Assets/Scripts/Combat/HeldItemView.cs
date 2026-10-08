using UnityEngine;

// What you hold that is not a weapon (a key, a bone fragment...) shows in your right hand, low on the right of the view:
// the selected hotbar item's World Model, sized to Size along its longest side and aimed by its own shape, the same
// for every model however it was exported: held by its wide end (a key's bow) with its long end pointing ahead, up
// and in towards the middle of the view (Point), its flat side to you; bobbing gently and lagging a touch behind when
// you turn, so it feels held.
// The dagger and trident are not shown here (the dagger is the drawn slash, the trident Held Weapon's), and nor is an
// item with no model (the stone fragments). It sits under the player's camera, not under the Hands, so the dagger's
// hiding of the 3D hands leaves it alone. Slash Attack adds it at start.
public class HeldItemView : MonoBehaviour
{
    [Tooltip("Where it is held, from the camera (right, up, forward), metres.")]
    public Vector3 holdAt = new Vector3(0.3f, -0.3f, 0.56f);
    [Tooltip("Where its long end points, from the hand (camera space: right, up, forward).")]
    public Vector3 point = new Vector3(-0.45f, 0.5f, 1f);
    [Tooltip("Its longest side, metres.")]
    public float size = 0.22f;
    [Tooltip("Extra turn after it is aimed (degrees), to fine-tune.")]
    public Vector3 tilt = Vector3.zero;
    [Tooltip("Up and down bob, metres, and how fast.")]
    public float bob = 0.012f;
    public float bobSpeed = 1.4f;
    [Tooltip("How quickly it catches up when you turn (higher = stiffer).")]
    public float follow = 14f;
    [Tooltip("Seconds it takes to come up into view (and go down out of it).")]
    public float raiseSeconds = 0.18f;

    private PlayerInventory inventory;
    private Transform eye;
    private Transform anchor;
    private GameObject model;
    private ItemDefinition shown;
    private float raised;
    private Quaternion lagged;

    public void Setup(PlayerInventory owner, Transform cameraTransform)
    {
        inventory = owner;
        eye = cameraTransform;
    }

    private void Start()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
        if (eye == null)
        {
            Camera camera = GetComponentInChildren<Camera>();
            eye = camera != null ? camera.transform : Camera.main != null ? Camera.main.transform : null;
        }
        if (eye == null)
        {
            enabled = false;
            return;
        }
        anchor = new GameObject("HeldItem").transform;
        anchor.SetParent(eye, false);
        anchor.localPosition = holdAt;
        lagged = eye.rotation;
        if (inventory != null)
        {
            inventory.onChanged.AddListener(Refresh);
            inventory.onSelectionChanged.AddListener(OnSelected);
        }
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.onChanged.RemoveListener(Refresh);
            inventory.onSelectionChanged.RemoveListener(OnSelected);
        }
        if (anchor != null)
            Destroy(anchor.gameObject);
    }

    private void OnSelected(int index) => Refresh();

    // The selected item, if it is one to show in the hand.
    private ItemDefinition Wanted()
    {
        ItemDefinition item = inventory != null ? inventory.SelectedItem : null;
        if (item == null || item.Kind == ItemDefinition.Category.Weapon || item.WorldModel == null)
            return null;
        return item;
    }

    private void Refresh()
    {
        ItemDefinition wanted = Wanted();
        if (wanted == shown)
            return;
        shown = wanted;
        if (model != null)
            Destroy(model);
        model = null;
        raised = 0f;
        if (shown == null || anchor == null)
            return;

        // The model under a holder: the holder carries the turn, the model inside is scaled and centred on it.
        var holder = new GameObject(shown.name);
        holder.transform.SetParent(anchor, false);
        holder.transform.localRotation = Quaternion.identity;
        model = holder;
        GameObject art = Instantiate(shown.WorldModel, holder.transform);
        art.transform.localPosition = Vector3.zero;
        foreach (Collider c in art.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        foreach (Renderer r in art.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.gameObject.layer = eye.gameObject.layer;
        }
        Bounds bounds = Measure(art, holder.transform, out bool any);
        if (!any)
            return;
        float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        art.transform.localScale *= size / Mathf.Max(0.0001f, longest);
        bounds = Measure(art, holder.transform, out _);
        art.transform.localPosition -= bounds.center;
        bounds = Measure(art, holder.transform, out _);   // centred now

        // Aimed, then the wide end put in the hand (the anchor), the rest reaching out along Point.
        holder.transform.localRotation = Quaternion.Euler(tilt) * Aim(art, holder.transform, bounds);
        bounds = Measure(art, anchor, out _);
        Vector3 reach = point.sqrMagnitude > 0.0001f ? point.normalized : Vector3.forward;
        Vector3 grip = bounds.center - reach * (Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(reach.x), Mathf.Abs(reach.y), Mathf.Abs(reach.z))) * 0.7f);
        holder.transform.localPosition = -grip;
    }

    private void LateUpdate()
    {
        if (anchor == null)
            return;
        Refresh();   // cheap: only rebuilds when the selection changed
        float target = model != null ? 1f : 0f;
        raised = Mathf.MoveTowards(raised, target, Time.deltaTime / Mathf.Max(0.01f, raiseSeconds));

        // A little lag behind the view when turning, a gentle bob, and up into view when it comes out.
        lagged = Quaternion.Slerp(lagged, eye.rotation, 1f - Mathf.Exp(-follow * Time.deltaTime));
        Quaternion behind = Quaternion.Inverse(eye.rotation) * lagged;
        float lift = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bob;
        float down = (1f - Ease.OutCubic(raised)) * 0.25f;
        anchor.localPosition = holdAt + new Vector3(0f, lift - down, 0f);
        anchor.localRotation = Quaternion.Slerp(Quaternion.identity, behind, 0.6f);
    }

    // The turn (in the holder) that points the model's long end along Point and its flat side at the camera: the long
    // axis is its longest side, the flat axis its thinnest, and the long end the narrower one (the other, wider end,
    // a key's bow, is what the hand holds). From the mesh itself where it can be read (Read/Write on its import),
    // else the item's Held Tip Axis says which way the long end is.
    private Quaternion Aim(GameObject art, Transform space, Bounds bounds)
    {
        Vector3 size = bounds.size;
        Vector3 longAxis = size.x >= size.y && size.x >= size.z ? Vector3.right : size.y >= size.z ? Vector3.up : Vector3.forward;
        Vector3 flatAxis = size.x <= size.y && size.x <= size.z ? Vector3.right : size.y <= size.z ? Vector3.up : Vector3.forward;
        if (flatAxis == longAxis)
            flatAxis = longAxis == Vector3.up ? Vector3.forward : Vector3.up;
        Vector3 sideAxis = Vector3.Cross(longAxis, flatAxis);

        // Which end is narrower: how wide the model is across Side Axis in each end's last quarter.
        float half = Vector3.Dot(size, longAxis) * 0.5f;
        float widePlus = 0f, wideMinus = 0f;
        bool read = false;
        foreach (MeshFilter filter in art.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable)   // (Unity 6 refuses a model without Read/Write, even in the editor)
                continue;
            read = true;
            Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            int stride = Mathf.Max(1, vertices.Length / 4000);
            for (int i = 0; i < vertices.Length; i += stride)
            {
                Vector3 p = m.MultiplyPoint3x4(vertices[i]) - bounds.center;
                float along = Vector3.Dot(p, longAxis);
                float across = Mathf.Abs(Vector3.Dot(p, sideAxis));
                if (along > half * 0.5f)
                    widePlus = Mathf.Max(widePlus, across);
                else if (along < -half * 0.5f)
                    wideMinus = Mathf.Max(wideMinus, across);
            }
        }
        Vector3 tipAxis;
        if (read && Mathf.Abs(widePlus - wideMinus) > 0.0001f)
            tipAxis = widePlus < wideMinus ? longAxis : -longAxis;
        else
        {
            Vector3 hint = shown.HeldTipAxis;
            tipAxis = Vector3.Dot(hint, longAxis) >= 0f ? longAxis : -longAxis;
        }

        Vector3 aim = point.sqrMagnitude > 0.0001f ? point.normalized : Vector3.forward;
        Vector3 face = Vector3.ProjectOnPlane(Vector3.back, aim).normalized;   // the flat side towards the camera
        return Quaternion.LookRotation(aim, face) * Quaternion.Inverse(Quaternion.LookRotation(tipAxis, flatAxis));
    }

    private static Bounds Measure(GameObject root, Transform space, out bool any)
    {
        any = false;
        var result = new Bounds();
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any)
                    result = new Bounds(p, Vector3.zero);
                else
                    result.Encapsulate(p);
                any = true;
            }
        }
        return result;
    }
}
