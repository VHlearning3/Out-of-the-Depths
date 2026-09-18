using UnityEngine;
using UnityEngine.Events;

// A trigger volume that fires events when the player swims in or out. Wire anything to it in the Inspector:
// Fish Spawner → Activate, a door, music, a chase sequence...
[RequireComponent(typeof(Collider))]
public class PlayerAreaTrigger : MonoBehaviour
{
    [Tooltip("Fire On Player Enter only the first time.")]
    [SerializeField] private bool once = false;

    [Header("Events")]
    public UnityEvent onPlayerEnter = new UnityEvent();
    public UnityEvent onPlayerExit = new UnityEvent();

    private bool fired;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<DeathManager>() == null)
            return;
        if (once && fired)
            return;

        fired = true;
        onPlayerEnter.Invoke();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<DeathManager>() == null)
            return;
        onPlayerExit.Invoke();
    }

    private void OnDrawGizmosSelected()
    {
        var area = GetComponent<Collider>();
        if (area == null)
            return;
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.15f);
        Gizmos.DrawCube(area.bounds.center, area.bounds.size);
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.7f);
        Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
    }
}
