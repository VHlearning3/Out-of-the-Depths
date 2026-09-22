using System.Collections.Generic;
using UnityEngine;

// The numbers a two-part book needs to shut, measured from the meshes of its halves (the children of the book whose
// names say left and right; the mesh itself may sit on a child of those, wherever the importer put it) in the book's
// own space, whatever the importer did to the axes. The spine is the edge both halves share. Each half turns about
// it until it points along the line between the two, on the side the open pages face (the big faces that look up;
// the covers underneath look down), so the pages end up inside and the covers outside; each is also a slab of some
// thickness, so the Checkpoint slides them half a thickness apart as they shut, or the two would lie inside each
// other. The pages' horizontal direction is the front, for facing the player. The Checkpoint measures at start; the
// checkpoint model tool writes the same numbers to the prefab.
public struct BookHinge
{
    public Vector3 spineBottom;
    public Vector3 spineTop;
    public float closeAngle;      // degrees each half turns to shut
    public bool flip;             // the left half turns the positive way round the spine (bottom to top); the right the other way
    public float faceYawOffset;   // added to a yaw that points +Z at the viewer so the pages face the viewer instead
    public Vector3 pagesLeft;     // which way each half's pages face, open, unit length (zero when no page faces were found)
    public Vector3 pagesRight;
    public float thickness;       // of a half, along its pages' direction

    public static bool Measure(Transform book, out BookHinge hinge, out string problem)
    {
        hinge = default;
        problem = null;
        if (book == null)
        {
            problem = "no book";
            return false;
        }
        Transform leftPart = Part(book, "left");
        Transform rightPart = Part(book, "right");
        MeshFilter left = leftPart != null ? leftPart.GetComponentInChildren<MeshFilter>(true) : null;
        MeshFilter right = rightPart != null ? rightPart.GetComponentInChildren<MeshFilter>(true) : null;
        if (left == null || right == null || left.sharedMesh == null || right.sharedMesh == null)
        {
            problem = "the book needs two parts named left and right with a mesh in each";
            return false;
        }
        if (!Application.isEditor && (!left.sharedMesh.isReadable || !right.sharedMesh.isReadable))
        {
            problem = "the half meshes are not readable in a build (Read/Write on the model import)";
            return false;
        }

        Vector3[] lv = InBookSpace(book, left, out bool leftMirrored);
        Vector3[] rv = InBookSpace(book, right, out bool rightMirrored);

        // The spine: the points both halves share; its bottom and top are the middle of the lowest and highest of them.
        var shared = new List<Vector3>();
        foreach (Vector3 a in lv)
            foreach (Vector3 b in rv)
                if ((a - b).sqrMagnitude < 1e-6f)
                {
                    shared.Add(a);
                    break;
                }
        if (shared.Count < 2)
        {
            problem = "the halves share no edge to hinge on (keep the spine vertices in both files)";
            return false;
        }
        shared.Sort((a, b) => a.y.CompareTo(b.y));
        Vector3 bottom = Vector3.zero, top = Vector3.zero;
        int half = Mathf.Max(1, shared.Count / 2);
        for (int i = 0; i < half; i++)
            bottom += shared[i] / half;
        for (int i = shared.Count - half; i < shared.Count; i++)
            top += shared[i] / half;

        // Where each half's open pages look, and how thick the halves are that way.
        Vector3 nL = PageDirection(lv, left.sharedMesh.triangles, leftMirrored).normalized;
        Vector3 nR = PageDirection(rv, right.sharedMesh.triangles, rightMirrored).normalized;
        Vector3 pages = nL + nR;

        // The closing turn, toward the pages' side: a half that droops away from them turns through the flat position.
        Vector3 axis = (top - bottom).normalized;
        Vector3 dL = Vector3.ProjectOnPlane(Centroid(lv) - bottom, axis);
        Vector3 dR = Vector3.ProjectOnPlane(Centroid(rv) - bottom, axis);
        Vector3 pageSide = Vector3.ProjectOnPlane(pages, axis);
        Vector3 shut = dL.normalized + dR.normalized;
        if (shut.sqrMagnitude < 0.01f)
            shut = pageSide;                       // the halves lie flat: straight up toward the pages
        else if (Vector3.Dot(shut, pageSide) < 0f)
            shut = -shut;                          // the halves droop: over the top, not under
        if (shut.sqrMagnitude < 1e-6f)
            shut = Vector3.Cross(axis, dL);        // no page faces found either: a plain quarter turn
        float turnL = Vector3.SignedAngle(dL, shut, axis);
        float turnR = Vector3.SignedAngle(dR, shut, axis);
        if (turnL * turnR > 0f)
        {
            problem = "the halves would both turn the same way round the spine; are left and right on opposite sides of it?";
            return false;
        }

        Vector3 front = pages;
        front.y = 0f;

        hinge.spineBottom = bottom;
        hinge.spineTop = top;
        hinge.closeAngle = (Mathf.Abs(turnL) + Mathf.Abs(turnR)) * 0.5f;
        hinge.flip = turnL > 0f;
        hinge.faceYawOffset = front.sqrMagnitude > 1e-4f ? -Mathf.Atan2(front.x, front.z) * Mathf.Rad2Deg : 180f;
        hinge.pagesLeft = nL;
        hinge.pagesRight = nR;
        hinge.thickness = (Extent(lv, nL) + Extent(rv, nR)) * 0.5f;
        return true;
    }

    public override string ToString()
    {
        return $"spine {spineBottom:0.00} to {spineTop:0.00}, each half turns {closeAngle:0} degrees {(flip ? "forward" : "backward")} round it to shut with the pages inside, {thickness:0.000} thick, pages face yaw {-faceYawOffset:0}";
    }

    // The part of the book whose name says left / right (the checkpoint tool names the halves after their files).
    private static Transform Part(Transform root, string key)
    {
        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
            if (part != root && part.name.ToLowerInvariant().Contains(key))
                return part;
        return null;
    }

    // The mesh's points in the book's space (the half sits at identity under the book, but its mesh may be on a
    // child with a transform of its own). Mirrored = the transform flips handedness, which turns face normals over.
    private static Vector3[] InBookSpace(Transform book, MeshFilter filter, out bool mirrored)
    {
        Matrix4x4 m = book.worldToLocalMatrix * filter.transform.localToWorldMatrix;
        mirrored = m.determinant < 0f;
        Vector3[] v = filter.sharedMesh.vertices;
        for (int i = 0; i < v.Length; i++)
            v[i] = m.MultiplyPoint3x4(v[i]);
        return v;
    }

    private static Vector3 Centroid(Vector3[] points)
    {
        Vector3 sum = Vector3.zero;
        foreach (Vector3 p in points)
            sum += p;
        return points.Length > 0 ? sum / points.Length : Vector3.zero;
    }

    // How far the points reach along a direction (the slab's thickness along its page normal).
    private static float Extent(Vector3[] points, Vector3 direction)
    {
        if (direction.sqrMagnitude < 1e-6f || points.Length == 0)
            return 0f;
        float min = float.MaxValue, max = float.MinValue;
        foreach (Vector3 p in points)
        {
            float d = Vector3.Dot(p, direction);
            min = Mathf.Min(min, d);
            max = Mathf.Max(max, d);
        }
        return max - min;
    }

    // Area-weighted sum of the normals of the big faces that look upward (the pages of an open book on a lectern).
    private static Vector3 PageDirection(Vector3[] v, int[] tris, bool mirrored)
    {
        float sign = mirrored ? -1f : 1f;
        float largest = 0f;
        for (int i = 0; i + 2 < tris.Length; i += 3)
            largest = Mathf.Max(largest, Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]).magnitude);
        Vector3 sum = Vector3.zero;
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector3 n = sign * Vector3.Cross(v[tris[i + 1]] - v[tris[i]], v[tris[i + 2]] - v[tris[i]]);
            if (n.magnitude < largest * 0.4f || n.y <= 0f)
                continue;
            sum += n;
        }
        return sum;
    }
}
