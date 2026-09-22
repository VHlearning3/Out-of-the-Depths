using UnityEngine;
using UnityEngine.UI;

// One slot in the puzzle board's top row: a tile dropped or clicked into it sits centred on it. Made by the board.
public class PuzzleSlot : MonoBehaviour
{
    public int Index { get; private set; }
    public RectTransform Rect { get; private set; }
    public Image Image { get; private set; }

    public PuzzleSlot Init(int index)
    {
        Index = index;
        Rect = (RectTransform)transform;
        Image = GetComponent<Image>();
        return this;
    }
}
