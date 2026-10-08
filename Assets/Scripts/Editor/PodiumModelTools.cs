using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Puts the team's podium (Assets/Prefabs/Podium_Prefab) on the stone-tablet pedestal. The Pedestal object keeps its
// name, its collider and its Puzzle Station, so nothing that finds or wires it changes; the grey placeholder cube goes
// and the podium becomes its Visual: stood upright on the floor (its long side up, the flat foot down), its slanted
// top turned towards the way the player comes in, at Podium Scale times its modelled size, and the collider fitted
// round it. The podium stays a prefab instance, so changes to Podium_Prefab show up here too.
// The finished tablet (Assets/Prefabs/Fragment_Full_prefab) goes in as the child Tablet: found the hole in the slanted
// top (the recess shaped like the tablet), fitted to it, its gem face looking out, seated a little into the recess,
// and hidden. A Puzzle Solved Tablet on the pedestal brings it in from the board when the puzzle is solved.
// Both scene builders call ApplyTo for the pedestals they make; Tools > Out of the Depths > Use Podium For Pedestals
// does the open scene without a rebuild. Safe to run again.
public static class PodiumModelTools
{
    public const string PrefabPath = "Assets/Prefabs/Podium_Prefab.prefab";
    public const string TabletPrefabPath = "Assets/Prefabs/Fragment_Full_prefab.prefab";
    // How much bigger than modelled the podium stands in the level.
    public const float PodiumScale = 1.5f;
    private const string PedestalName = "Pedestal";
    private const string VisualName = "Visual";
    private const string TabletName = "Tablet";

    [MenuItem("Tools/Out of the Depths/Use Podium For Pedestals")]
    public static void ApplyToOpenScene()
    {
        int done = 0;
        foreach (PuzzleStation station in Object.FindObjectsByType<PuzzleStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (station.gameObject.name != PedestalName || EditorUtility.IsPersistent(station))
                continue;
            if (ApplyTo(station.gameObject, DefaultFacing(station.transform)))
                done++;
        }
        if (done == 0)
        {
            Debug.LogWarning($"Podium: no Pedestal (a Puzzle Station named {PedestalName}) in the open scene, or no prefab at {PrefabPath}.");
            return;
        }
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Podium: {done} pedestal(s) now show {PrefabPath}. Save the scene to keep it.");
    }

    // Main_Scene: the stone room's pedestal faces the door in from the middle room. Anywhere else: south, towards
    // where the player comes from in the arena.
    private static Vector3 DefaultFacing(Transform pedestal)
    {
        if (pedestal.parent != null && pedestal.parent.name == "Room_6_StoneRoom")
        {
            GameObject door = GameObject.Find("Door_StoneRoom");
            if (door != null)
                return door.transform.position;
        }
        return pedestal.position + Vector3.back * 5f;
    }

    // Makes the podium the pedestal's look. faceToward: a world point the slanted top turns towards.
    public static bool ApplyTo(GameObject pedestal, Vector3 faceToward)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (pedestal == null || prefab == null)
        {
            if (prefab == null)
                Debug.LogWarning($"Podium: no prefab at {PrefabPath}, the pedestal keeps its placeholder.");
            return false;
        }
        Transform root = pedestal.transform;
        Undo.RegisterFullObjectHierarchyUndo(pedestal, "Use Podium For Pedestal");

        // The floor: the bottom of whatever the pedestal shows now (the placeholder cube, or the podium of an earlier run).
        float floorY = root.position.y - 0.5f * root.lossyScale.y;
        if (WorldBounds(root, out Bounds shown))
            floorY = shown.min.y;
        else if (pedestal.TryGetComponent(out Collider existing) && existing.enabled)
            floorY = existing.bounds.min.y;

        // Out with the old look. The tint would paint the podium grey, so it goes too.
        Transform oldVisual = root.Find(VisualName);
        if (oldVisual != null)
            Undo.DestroyObjectImmediate(oldVisual.gameObject);
        Transform oldTablet = root.Find(TabletName);
        if (oldTablet != null)
            Undo.DestroyObjectImmediate(oldTablet.gameObject);
        if (pedestal.TryGetComponent(out RendererTint tint))
            Undo.DestroyObjectImmediate(tint);
        if (pedestal.TryGetComponent(out MeshRenderer cubeRenderer))
            Undo.DestroyObjectImmediate(cubeRenderer);
        if (pedestal.TryGetComponent(out MeshFilter cubeFilter))
            Undo.DestroyObjectImmediate(cubeFilter);
        root.localScale = Vector3.one;   // the podium at its own size, not stretched by the old cube's scale

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
        Undo.RegisterCreatedObjectUndo(visual, "Use Podium For Pedestal");
        visual.name = VisualName;
        Transform v = visual.transform;
        v.localPosition = Vector3.zero;
        v.localScale *= PodiumScale;   // on top of whatever scale the model imports with

        // Upright: the longest side points up...
        List<Vector3> points = LocalPoints(v, root);
        if (points.Count == 0)
        {
            Debug.LogWarning("Podium: the prefab has no mesh.", pedestal);
            return false;
        }
        Bounds b = Encapsulate(points);
        int longest = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.z >= b.size.y ? 2 : 1;
        if (longest != 1)
        {
            v.localRotation = Quaternion.FromToRotation(longest == 0 ? Vector3.right : Vector3.forward, Vector3.up) * v.localRotation;
            points = LocalPoints(v, root);
            b = Encapsulate(points);
        }
        // ...and the flat foot down: the foot is a flat face square to the long side; the top is slanted.
        float height = Mathf.Max(b.size.y, 1e-4f);
        if (FlatArea(v, root, 1f) > FlatArea(v, root, -1f))
        {
            v.localRotation = Quaternion.AngleAxis(180f, Vector3.right) * v.localRotation;
            points = LocalPoints(v, root);
            b = Encapsulate(points);
        }

        // The slanted top faces the way in: the top slopes down from its high back edge towards the reader.
        Vector3 topAll = Vector3.zero, topHigh = Vector3.zero;
        int topCount = 0, highCount = 0;
        foreach (Vector3 p in points)
        {
            if (p.y > b.max.y - height * 0.25f) { topAll += p; topCount++; }
            if (p.y > b.max.y - height * 0.02f) { topHigh += p; highCount++; }
        }
        Vector3 front = topCount > 0 && highCount > 0 ? topAll / topCount - topHigh / highCount : Vector3.zero;
        front.y = 0f;
        Vector3 want = root.InverseTransformDirection(faceToward - root.position);
        want.y = 0f;
        if (front.sqrMagnitude > 1e-8f && want.sqrMagnitude > 1e-8f)
        {
            v.localRotation = Quaternion.AngleAxis(Vector3.SignedAngle(front, want, Vector3.up), Vector3.up) * v.localRotation;
            points = LocalPoints(v, root);
            b = Encapsulate(points);
        }

        // On the floor, centred under the pedestal.
        float floorLocal = root.InverseTransformPoint(new Vector3(root.position.x, floorY, root.position.z)).y;
        v.localPosition += new Vector3(-b.center.x, floorLocal - b.min.y, -b.center.z);
        b = Encapsulate(LocalPoints(v, root));

        // The pedestal's own box (what E and the swimmer hit) wraps the podium.
        if (!pedestal.TryGetComponent(out BoxCollider box))
            box = Undo.AddComponent<BoxCollider>(pedestal);
        box.center = b.center;
        box.size = b.size;

        PlaceTablet(pedestal, v, root);

        EditorUtility.SetDirty(pedestal);
        return true;
    }

    // ---- the tablet in the hole -------------------------------------------------------------------------------------

    // The finished tablet as the hidden child Tablet, resting in the podium's hole, and the Puzzle Solved Tablet that
    // brings it in when the puzzle is solved. The Tablet object's own axes are the hole's: Z out of the hole, Y up the
    // slope; the model sits inside it centred, its outline across X/Y and its gem face (the model's +Z) out.
    private static void PlaceTablet(GameObject pedestal, Transform visual, Transform root)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabletPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Podium: no tablet at {TabletPrefabPath}, nothing goes into the podium's hole when the puzzle is solved.", pedestal);
            return;
        }
        bool foundHole = FindHole(visual, root, out Hole hole);
        if (!foundHole && hole.normal == Vector3.zero)
        {
            Debug.LogWarning("Podium: the podium has no slanted top to put the tablet on.", pedestal);
            return;
        }

        var holder = new GameObject(TabletName).transform;
        Undo.RegisterCreatedObjectUndo(holder.gameObject, "Use Podium For Pedestal");
        holder.SetParent(root, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
        Undo.RegisterCreatedObjectUndo(model, "Use Podium For Pedestal");
        Transform m = model.transform;
        m.localPosition = Vector3.zero;
        m.localRotation = Quaternion.identity;   // its own import scale stays

        // The model's thinnest side is its thickness (face out), its longest the way up its face.
        List<Vector3> points = LocalPoints(m, holder);
        if (points.Count == 0)
        {
            Debug.LogWarning($"Podium: {TabletPrefabPath} has no mesh.", pedestal);
            Undo.DestroyObjectImmediate(holder.gameObject);
            return;
        }
        Bounds t = Encapsulate(points);
        int thin = t.size.x <= t.size.y && t.size.x <= t.size.z ? 0 : t.size.y <= t.size.z ? 1 : 2;
        int tall = t.size.x >= t.size.y && t.size.x >= t.size.z ? 0 : t.size.y >= t.size.z ? 1 : 2;
        if (thin == tall)
        {
            thin = 2;
            tall = 1;
        }
        Quaternion turn = Quaternion.Inverse(Quaternion.LookRotation(Axis(thin), Axis(tall)));
        m.localRotation = turn * m.localRotation;
        points = LocalPoints(m, holder);
        t = Encapsulate(points);

        // Centred on its outline, the outline's plane (where it is widest, its rim) through the holder.
        float halfX = Mathf.Max(t.extents.x, 1e-5f), halfY = Mathf.Max(t.extents.y, 1e-5f);
        float rimZ = 0f;
        int rimCount = 0;
        foreach (Vector3 p in points)
        {
            float dx = (p.x - t.center.x) / halfX, dy = (p.y - t.center.y) / halfY;
            if (dx * dx + dy * dy >= 0.81f)
            {
                rimZ += p.z;
                rimCount++;
            }
        }
        rimZ = rimCount > 0 ? rimZ / rimCount : t.center.z;
        m.localPosition -= new Vector3(t.center.x, t.center.y, rimZ);

        // Fitted to the hole with a hair of room, and sunk into its sloping walls until it nearly touches them.
        float fit = PodiumScale, seat = 0f;
        if (foundHole)
        {
            fit = 0.97f * Mathf.Min(hole.halfWidth / halfX, hole.halfLength / halfY);
            float wallX = (hole.halfWidth - halfX * fit) / Mathf.Max(hole.halfWidth - hole.floorHalfWidth, 1e-5f);
            float wallY = (hole.halfLength - halfY * fit) / Mathf.Max(hole.halfLength - hole.floorHalfLength, 1e-5f);
            seat = 0.9f * Mathf.Clamp01(Mathf.Min(wallX, wallY)) * hole.depth;
        }
        else
            Debug.LogWarning("Podium: no hole found in the podium's slanted top; the tablet lies on the middle of the slope at the podium's scale.", pedestal);

        holder.localPosition = hole.centre - hole.normal * seat;
        holder.localRotation = Quaternion.LookRotation(hole.normal, hole.up);
        holder.localScale = Vector3.one * fit;
        holder.gameObject.SetActive(false);

        if (!pedestal.TryGetComponent(out PuzzleSolvedTablet solved))
            solved = Undo.AddComponent<PuzzleSolvedTablet>(pedestal);
        var so = new SerializedObject(solved);
        so.FindProperty("tablet").objectReferenceValue = holder;
        so.FindProperty("station").objectReferenceValue = pedestal.GetComponent<PuzzleStation>();
        so.ApplyModifiedProperties();

        Debug.Log($"Podium: the tablet rests in the hole of {pedestal.name} (scale {fit:0.###}, {seat * 100f:0.#} cm into the recess), hidden until the puzzle is solved.", pedestal);
    }

    private static Vector3 Axis(int index) => index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;

    // The hole in the slanted top, in the root's space. centre: the middle of its opening, on the top's plane;
    // normal: out of the top; up: up the slope; half sizes of the opening (rim) and of its floor; depth of the recess.
    private struct Hole
    {
        public Vector3 centre, normal, up;
        public float halfWidth, halfLength, floorHalfWidth, floorHalfLength, depth;
    }

    // The slanted top is the biggest area of faces sharing one tilted, upward normal. The hole's floor is the part of
    // it that lies deeper than the rest; its rim, the top's vertices just round the floor. False with only centre,
    // normal and up set (the middle of the slope) when there is a top but no hole; normal zero when there is no top.
    private static bool FindHole(Transform visual, Transform root, out Hole hole)
    {
        hole = default;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            Matrix4x4 mat = toRoot * filter.transform.localToWorldMatrix;
            int offset = verts.Count;
            foreach (Vector3 p in mesh.vertices)
                verts.Add(mat.MultiplyPoint3x4(p));
            foreach (int i in mesh.triangles)
                tris.Add(offset + i);
        }
        if (verts.Count == 0)
            return false;
        float eps = Encapsulate(verts).size.magnitude * 0.0015f;

        // Every face's normal (turned to point up), area and middle.
        int count = tris.Count / 3;
        var normals = new Vector3[count];
        var areas = new float[count];
        var middles = new Vector3[count];
        for (int f = 0; f < count; f++)
        {
            Vector3 a = verts[tris[3 * f]], b = verts[tris[3 * f + 1]], c = verts[tris[3 * f + 2]];
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float twice = cross.magnitude;
            if (twice < 1e-12f)
                continue;
            Vector3 n = cross / twice;
            normals[f] = n.y < 0f ? -n : n;
            areas[f] = twice * 0.5f;
            middles[f] = (a + b + c) / 3f;
        }

        // The tilted direction with the most area behind it (normals binned, then the best bin's faces averaged).
        var binArea = new Dictionary<Vector3Int, float>();
        var binNormal = new Dictionary<Vector3Int, Vector3>();
        for (int f = 0; f < count; f++)
        {
            Vector3 n = normals[f];
            if (areas[f] <= 0f || n.y < 0.2f || n.y > 0.97f)
                continue;
            var key = Vector3Int.RoundToInt(n * 12f);
            binArea.TryGetValue(key, out float sum);
            binArea[key] = sum + areas[f];
            binNormal.TryGetValue(key, out Vector3 dir);
            binNormal[key] = dir + n * areas[f];
        }
        if (binArea.Count == 0)
            return false;
        Vector3Int best = default;
        float bestArea = -1f;
        foreach (var pair in binArea)
            if (pair.Value > bestArea)
            {
                best = pair.Key;
                bestArea = pair.Value;
            }
        Vector3 guess = binNormal[best].normalized;
        Vector3 normal = Vector3.zero;
        var slope = new List<int>();
        for (int f = 0; f < count; f++)
            if (areas[f] > 0f && Vector3.Dot(normals[f], guess) > 0.98f)
            {
                slope.Add(f);
                normal += normals[f] * areas[f];
            }
        normal.Normalize();
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        Vector3 right = Vector3.Cross(up, normal);

        float top = float.MinValue;
        Vector3 slopeMiddle = Vector3.zero;
        float slopeArea = 0f;
        foreach (int f in slope)
        {
            top = Mathf.Max(top, Vector3.Dot(normal, middles[f]));
            slopeMiddle += middles[f] * areas[f];
            slopeArea += areas[f];
        }
        hole.normal = normal;
        hole.up = up;
        slopeMiddle /= slopeArea;
        hole.centre = slopeMiddle + normal * (top - Vector3.Dot(normal, slopeMiddle));

        // The floor: the highest of the slope's faces that lie deeper than the top.
        float floor = float.MinValue;
        foreach (int f in slope)
        {
            float d = Vector3.Dot(normal, middles[f]);
            if (d < top - eps)
                floor = Mathf.Max(floor, d);
        }
        if (floor == float.MinValue)
            return false;
        Vector3 floorMiddle = Vector3.zero;
        float floorArea = 0f;
        var floorFaces = new List<int>();
        foreach (int f in slope)
            if (Mathf.Abs(Vector3.Dot(normal, middles[f]) - floor) < eps)
            {
                floorFaces.Add(f);
                floorMiddle += middles[f] * areas[f];
                floorArea += areas[f];
            }
        floorMiddle /= floorArea;
        float floorHalfW = 0f, floorHalfL = 0f;
        foreach (int f in floorFaces)
            for (int k = 0; k < 3; k++)
            {
                Vector3 q = verts[tris[3 * f + k]] - floorMiddle;
                floorHalfW = Mathf.Max(floorHalfW, Mathf.Abs(Vector3.Dot(q, right)));
                floorHalfL = Mathf.Max(floorHalfL, Mathf.Abs(Vector3.Dot(q, up)));
            }
        if (floorHalfW < 1e-5f || floorHalfL < 1e-5f)
            return false;

        // The rim: the top's vertices just round the floor (the top's own outer edge lies well outside).
        float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
        int rim = 0;
        foreach (Vector3 p in verts)
        {
            if (Mathf.Abs(Vector3.Dot(normal, p) - top) > eps)
                continue;
            Vector3 q = p - floorMiddle;
            float u = Vector3.Dot(q, right), v = Vector3.Dot(q, up);
            if ((u / floorHalfW) * (u / floorHalfW) + (v / floorHalfL) * (v / floorHalfL) > 1.44f)
                continue;
            minU = Mathf.Min(minU, u);
            maxU = Mathf.Max(maxU, u);
            minV = Mathf.Min(minV, v);
            maxV = Mathf.Max(maxV, v);
            rim++;
        }
        Vector2 middle = Vector2.zero;
        float halfW = floorHalfW * 1.08f, halfL = floorHalfL * 1.08f;   // no rim found: a typical bevel
        if (rim >= 3)
        {
            middle = new Vector2((minU + maxU) * 0.5f, (minV + maxV) * 0.5f);
            halfW = Mathf.Max((maxU - minU) * 0.5f, floorHalfW);
            halfL = Mathf.Max((maxV - minV) * 0.5f, floorHalfL);
        }

        hole.depth = top - floor;
        hole.centre = floorMiddle + right * middle.x + up * middle.y + normal * hole.depth;
        hole.halfWidth = halfW;
        hole.halfLength = halfL;
        hole.floorHalfWidth = floorHalfW;
        hole.floorHalfLength = floorHalfL;
        return true;
    }

    private static List<Vector3> LocalPoints(Transform visual, Transform root)
    {
        var points = new List<Vector3>();
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            foreach (Vector3 p in mesh.vertices)
                points.Add(m.MultiplyPoint3x4(p));
        }
        return points;
    }

    // Area of the faces that look straight up (sign 1) or straight down (sign -1), in the root's space.
    private static float FlatArea(Transform visual, Transform root, float sign)
    {
        float area = 0f;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = m.MultiplyPoint3x4(verts[tris[i]]);
                Vector3 c = Vector3.Cross(m.MultiplyPoint3x4(verts[tris[i + 1]]) - a, m.MultiplyPoint3x4(verts[tris[i + 2]]) - a);
                float twice = c.magnitude;
                if (twice > 1e-12f && Mathf.Abs(c.y / twice) > 0.95f && Mathf.Sign(c.y) == sign)
                    area += twice * 0.5f;
            }
        }
        return area;
    }

    private static Bounds Encapsulate(List<Vector3> points)
    {
        var b = new Bounds(points[0], Vector3.zero);
        for (int i = 1; i < points.Count; i++)
            b.Encapsulate(points[i]);
        return b;
    }

    private static bool WorldBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(false))
        {
            if (!r.enabled || r is ParticleSystemRenderer)
                continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }
}
