using UnityEngine;

// An invisible trigger that shows a tutorial card (Tutorial Cards) the first time the player swims into it, e.g. the
// pushing tip on entering the box room. Needs a trigger collider on this object covering the area. Each card shows
// once ever (and never with tips switched off in Settings), so walking in again does nothing.
[RequireComponent(typeof(Collider))]
public class TutorialZone : MonoBehaviour
{
    [Tooltip("Which card to show: push, swim, dash, goals, interact, items, locks, fight, eat, checkpoint, danger.")]
    public string lesson = "push";

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (PlayerBody.Is(other))
            TutorialCards.Show(lesson);
    }
}
