using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One tile on the puzzle board: drag it into a slot, or click it (into the next free slot, or back out of the one it
// is in). Made by the board; the picture is a child Image.
public class PuzzleTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public string Id { get; private set; }
    public Vector2 Home { get; private set; }      // where it sits in the pool
    public PuzzleSlot Slot { get; set; }           // the slot it is in, or null
    public RectTransform Rect { get; private set; }

    private PuzzleBoard board;
    private RectTransform canvasRect;
    private Image image;
    private bool dragging;

    public PuzzleTile Init(PuzzleBoard owner, string id, Vector2 home, RectTransform canvas)
    {
        board = owner;
        Id = id;
        Home = home;
        canvasRect = canvas;
        Rect = (RectTransform)transform;
        image = GetComponent<Image>();
        return this;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!board.Interactive)
            return;
        dragging = true;
        board.BeginDrag(this);
        if (image != null)
            image.raycastTarget = false;   // so the slot under the pointer is what the raycast finds
        Rect.SetParent(board.DragLayer, true);
        Rect.SetAsLastSibling();
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, eventData.position, eventData.pressEventCamera, out Vector2 point))
            Rect.anchoredPosition = point;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;
        dragging = false;
        if (image != null)
            image.raycastTarget = true;
        GameObject under = eventData.pointerCurrentRaycast.gameObject;
        board.Dropped(this, under != null ? under.GetComponentInParent<PuzzleSlot>() : null);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.dragging)
            return;
        board.Clicked(this);
    }
}
