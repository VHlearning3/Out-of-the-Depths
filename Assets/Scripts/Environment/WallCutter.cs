using UnityEngine;

// Cuts a rectangular hole through a box wall (a scaled cube) where an opening sits: the wall is replaced by up to four
// boxes around the hole, keeping its material, tint, layer, tag and collider. Only cube meshes can be cut - a modelled hull
// needs the hole in the model. Used by Fish Window at Play and from its inspector button.
public static class WallCutter
{
    private const float MaxWallDistance = 1.5f;
    private static readonly RaycastHit[] hits = new RaycastHit[16];

    // Returns true when a hole was cut; 'thickness' is the wall's depth along the opening's forward axis.
    // 'message' explains the result; it is empty when there was simply no wall behind the opening.
    public static bool Cut(Transform opening, Vector2 holeSize, bool undoable, out string message, out float thickness)
    {
        message = string.Empty;
        thickness = 0f;
        GameObject wall = FindWall(opening);
        if (wall == null)
            return false;

        var filter = wall.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount != 24)
        {
            message = $"'{wall.name}' is not a box wall, so the hole must be modelled into it.";
            return false;
        }

        Transform w = wall.transform;
        int u = ClosestAxis(w, opening.right);
        int v = ClosestAxis(w, opening.up);
        if (u == v)
        {
            message = $"'{wall.name}' is not facing the window squarely; rotate the window to sit flat on it.";
            return false;
        }

        Vector3 centre = w.InverseTransformPoint(opening.position);
        Vector3 scale = w.lossyScale;
        int n = 3 - u - v;
        thickness = Mathf.Abs(scale[n]);
        float halfW = holeSize.x * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(scale[u]));
        float halfH = holeSize.y * 0.5f / Mathf.Max(0.0001f, Mathf.Abs(scale[v]));
        float cu = Mathf.Clamp(centre[u], -0.5f + halfW, 0.5f - halfW);
        float cv = Mathf.Clamp(centre[v], -0.5f + halfH, 0.5f - halfH);

        Piece(wall, "Below", u, v, -0.5f, 0.5f, -0.5f, cv - halfH, undoable);
        Piece(wall, "Above", u, v, -0.5f, 0.5f, cv + halfH, 0.5f, undoable);
        Piece(wall, "Left", u, v, -0.5f, cu - halfW, cv - halfH, cv + halfH, undoable);
        Piece(wall, "Right", u, v, cu + halfW, 0.5f, cv - halfH, cv + halfH, undoable);

        // Read the name before the wall is gone: a destroyed object throws on any access.
        message = $"Cut a {holeSize.x} x {holeSize.y} hole in '{wall.name}'.";

#if UNITY_EDITOR
        if (undoable)
            UnityEditor.Undo.DestroyObjectImmediate(wall);
        else
#endif
        if (Application.isPlaying)
            wall.SetActive(false);
        else
            Object.DestroyImmediate(wall);

        return true;
    }

    // The nearest solid, non-fish, non-window collider straight behind the opening.
    private static GameObject FindWall(Transform opening)
    {
        Vector3 origin = opening.position + opening.forward * 0.3f;
        int count = Physics.RaycastNonAlloc(origin, -opening.forward, hits, MaxWallDistance + 0.3f, ~0, QueryTriggerInteraction.Ignore);
        GameObject best = null;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance >= bestDistance || hit.transform.IsChildOf(opening))
                continue;
            if (hit.collider.GetComponentInParent<FishController>() != null)
                continue;
            bestDistance = hit.distance;
            best = hit.collider.gameObject;
        }
        return best;
    }

    private static int ClosestAxis(Transform t, Vector3 direction)
    {
        float x = Mathf.Abs(Vector3.Dot(direction, t.right));
        float y = Mathf.Abs(Vector3.Dot(direction, t.up));
        float z = Mathf.Abs(Vector3.Dot(direction, t.forward));
        return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
    }

    // One box of the cut wall, given its range in the unit-cube space of the original wall (u sideways, v up; full thickness).
    private static void Piece(GameObject wall, string label, int u, int v, float u0, float u1, float v0, float v1, bool undoable)
    {
        if (u1 - u0 <= 0.001f || v1 - v0 <= 0.001f)
            return;

        Vector3 centre = Vector3.zero;
        Vector3 size = Vector3.one;
        centre[u] = (u0 + u1) * 0.5f;
        centre[v] = (v0 + v1) * 0.5f;
        size[u] = u1 - u0;
        size[v] = v1 - v0;

        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = wall.name + "_" + label;
        piece.layer = wall.layer;
        piece.tag = wall.tag;
        piece.isStatic = wall.isStatic;
        piece.transform.SetParent(wall.transform, false);
        piece.transform.localPosition = centre;
        piece.transform.localScale = size;
        piece.transform.SetParent(wall.transform.parent, true);

        var wallRenderer = wall.GetComponent<MeshRenderer>();
        if (wallRenderer != null)
            piece.GetComponent<MeshRenderer>().sharedMaterial = wallRenderer.sharedMaterial;

        var tint = wall.GetComponent<RendererTint>();
        if (tint != null)
            piece.AddComponent<RendererTint>().Tint = tint.Tint;

        var wallCollider = wall.GetComponent<Collider>();
        if (wallCollider != null)
            piece.GetComponent<Collider>().isTrigger = wallCollider.isTrigger;

#if UNITY_EDITOR
        if (undoable)
            UnityEditor.Undo.RegisterCreatedObjectUndo(piece, "Cut hole in wall");
#endif
    }
}
