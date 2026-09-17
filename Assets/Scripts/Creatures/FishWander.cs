using UnityEngine;

// Drives the fish's root transform, so the visual mesh underneath can be swapped freely.
public class FishWander : MonoBehaviour
{
    [SerializeField] private float wanderRadius = 3f;
    [SerializeField] private float verticalRange = 1f;
    [SerializeField] private float speed = 1.2f;
    [SerializeField] private float turnSpeed = 2.5f;
    [SerializeField] private float targetReachDistance = 0.4f;
    [SerializeField] private float idleBobAmount = 0.08f;

    private Vector3 home;
    private Vector3 target;
    private float bobOffset;

    private void Awake()
    {
        home = transform.position;
        bobOffset = Random.value * 10f;
        PickNewTarget();
    }

    public void ResetHome(Vector3 position)
    {
        home = position;
        PickNewTarget();
    }

    private void Update()
    {
        Vector3 toTarget = target - transform.position;
        if (toTarget.magnitude < targetReachDistance)
        {
            PickNewTarget();
            toTarget = target - transform.position;
        }

        Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desired, turnSpeed * Time.deltaTime);

        Vector3 bob = Vector3.up * (Mathf.Sin(Time.time * 2f + bobOffset) * idleBobAmount * Time.deltaTime);
        transform.position += transform.forward * (speed * Time.deltaTime) + bob;
    }

    private void PickNewTarget()
    {
        Vector2 flat = Random.insideUnitCircle * wanderRadius;
        float y = Random.Range(-verticalRange, verticalRange);
        target = home + new Vector3(flat.x, y, flat.y);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(Application.isPlaying ? home : transform.position, wanderRadius);
    }
}
