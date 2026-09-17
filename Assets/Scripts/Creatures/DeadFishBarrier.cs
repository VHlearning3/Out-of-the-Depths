using UnityEngine;

// Invisible ceiling for dead fish: stretch this object's trigger collider across the water surface or a room's top.
// A dead fish that drifts into it fades out (see FishController). Live fish and the player are unaffected.
[RequireComponent(typeof(Collider))]
public class DeadFishBarrier : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnDrawGizmos()
    {
        var collider = GetComponent<Collider>();
        if (collider == null)
            return;

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.15f);
        Gizmos.DrawCube(collider.bounds.center, collider.bounds.size);
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
