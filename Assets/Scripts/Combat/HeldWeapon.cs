using UnityEngine;

// The weapon in your hand: the model of the weapon you hold (the selected hotbar slot when it is a weapon, else the
// one you last had out, else the first weapon you own), fitted to its item's Held Length and gripped at Held Grip,
// pointing forward from the hand. The placeholder stick under Hands hides while a model shows. Slash Attack adds it to
// the Hands object at start; the hand animator swings the Hands, so the model swings with them.
public class HeldWeapon : MonoBehaviour
{
    private PlayerInventory inventory;
    private Renderer[] placeholder = new Renderer[0];
    private ItemDefinition current;
    private GameObject model;

    public ItemDefinition Current => current;

    public void Setup(PlayerInventory owner, GameObject placeholderStick)
    {
        inventory = owner;
        placeholder = placeholderStick != null ? placeholderStick.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }

    private void Start()
    {
        if (inventory == null)
            inventory = GetComponentInParent<PlayerInventory>();
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
    }

    private void OnSelected(int index) => Refresh();

    private void Refresh()
    {
        ItemDefinition wanted = Pick();
        if (wanted != current)
        {
            current = wanted;
            Build();
        }
    }

    // The selected slot if it holds a weapon; else the one already out, while you still have it; else the first owned.
    private ItemDefinition Pick()
    {
        if (inventory == null)
            return null;
        ItemDefinition selected = inventory.SelectedItem;
        if (IsWeapon(selected))
            return selected;
        if (current != null && inventory.Has(current))
            return current;
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            ItemDefinition item = inventory.GetSlot(i).item;
            if (IsWeapon(item))
                return item;
        }
        return null;
    }

    private static bool IsWeapon(ItemDefinition item) => item != null && item.Kind == ItemDefinition.Category.Weapon;

    private void Build()
    {
        if (model != null)
            Destroy(model);
        model = null;

        bool hasModel = current != null && current.WorldModel != null;
        foreach (Renderer r in placeholder)
            if (r != null)
                r.enabled = !hasModel;
        if (!hasModel)
            return;

        // The model under a pivot: the pivot turns its tip to point forward, the model inside is scaled and shifted so
        // the grip sits on the hand.
        var pivot = new GameObject("HeldModel");
        pivot.transform.SetParent(transform, false);
        pivot.transform.localRotation = Quaternion.Euler(current.HeldRotation);
        model = pivot;

        GameObject art = Instantiate(current.WorldModel, pivot.transform);
        art.name = current.WorldModel.name;
        foreach (Collider c in art.GetComponentsInChildren<Collider>(true))
            Destroy(c);
        foreach (Renderer r in art.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.gameObject.layer = gameObject.layer;
        }
        Vector3 tip = current.HeldTipAxis.sqrMagnitude > 0.0001f ? current.HeldTipAxis.normalized : Vector3.up;
        art.transform.localRotation = Quaternion.FromToRotation(tip, Vector3.forward);
        art.transform.localPosition = Vector3.zero;
        art.transform.localScale = Vector3.one;

        // Fit: its length along forward (in the pivot's space) to Held Length, then the grip point onto the hand.
        Bounds bounds = LocalBounds(art, pivot.transform, out bool any);
        if (!any)
            return;
        float length = Mathf.Max(0.0001f, bounds.size.z);
        float scale = current.HeldLength / length;
        art.transform.localScale = Vector3.one * scale;
        bounds = LocalBounds(art, pivot.transform, out _);
        Vector3 grip = new Vector3(bounds.center.x, bounds.center.y, Mathf.Lerp(bounds.min.z, bounds.max.z, current.HeldGrip));
        art.transform.localPosition -= grip;
    }

    // The renderers' box in `space` (from their mesh bounds, so it is right however the model is turned).
    private static Bounds LocalBounds(GameObject root, Transform space, out bool any)
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
        foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Bounds b = skinned.bounds;   // world space
            foreach (Vector3 p in new[] { b.min, b.max })
            {
                Vector3 local = space.InverseTransformPoint(p);
                if (!any)
                    result = new Bounds(local, Vector3.zero);
                else
                    result.Encapsulate(local);
                any = true;
            }
        }
        return result;
    }
}
