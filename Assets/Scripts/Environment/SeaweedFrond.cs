using UnityEngine;

// The shape of one generated seaweed frond, kept on its object so Seaweed can rebuild the ribbon mesh any time.
public class SeaweedFrond : MonoBehaviour
{
    public float height = 2f;
    public float widthScale = 1f;
    public float curl = 0.08f;
    public float phase;
    public float shade;
}
