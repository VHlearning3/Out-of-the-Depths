using UnityEngine;
using UnityEngine.Events;

// Something in the world you solve a puzzle at: the pedestal where the stone tablet is pieced together, the code lock
// where the runes go in. E opens the board with Puzzle. Required Item / Amount is what you must be carrying to start
// (the three stone fragments), taken from you when it is solved if Consume is on. On Solved fires once (a door opens
// or unlocks); solved stays solved. Needs a collider, and an Interactable Highlight makes it light up like the rest.
public class PuzzleStation : MonoBehaviour, IInteractable
{
    [SerializeField] private PuzzleDefinition puzzle;
    [Tooltip("What E says.")]
    [SerializeField] private string prompt = "examine";
    [SerializeField] private string solvedPrompt = "solved";

    [Header("What you need to bring")]
    [Tooltip("Empty = nothing, the board opens straight away.")]
    [SerializeField] private ItemDefinition requiredItem;
    [SerializeField, Min(0)] private int requiredAmount = 0;
    [Tooltip("Take the items out of the inventory when it is solved.")]
    [SerializeField] private bool consume = true;

    [Header("Solved")]
    [SerializeField] private AudioClip solvedSound;
    [SerializeField, Range(0f, 1f)] private float solvedVolume = 0.7f;
    public UnityEvent onSolved = new UnityEvent();

    public bool IsSolved { get; private set; }

    public string Prompt
    {
        get
        {
            if (IsSolved)
                return solvedPrompt;
            if (!HasItems(out int have))
                return $"needs {requiredItem.DisplayName} ({have}/{requiredAmount})";
            return prompt;
        }
    }

    public void Interact(GameObject interactor)
    {
        if (IsSolved || puzzle == null || !HasItems(out _))
            return;
        PuzzleBoard.Show(puzzle, Solved);
    }

    private bool HasItems(out int have)
    {
        have = 0;
        if (requiredItem == null || requiredAmount <= 0)
            return true;
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        have = inventory != null ? inventory.Count(requiredItem) : 0;
        return have >= requiredAmount;
    }

    private void Solved()
    {
        if (IsSolved)
            return;
        IsSolved = true;
        if (consume && requiredItem != null && requiredAmount > 0)
        {
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            if (inventory != null)
                inventory.Remove(requiredItem, requiredAmount);
        }
        if (solvedSound != null)
            AudioSource.PlayClipAtPoint(solvedSound, transform.position, solvedVolume);
        onSolved.Invoke();
    }

    // For the admin page: solve it as if the board had been done.
    public void SolveNow() => Solved();
}
