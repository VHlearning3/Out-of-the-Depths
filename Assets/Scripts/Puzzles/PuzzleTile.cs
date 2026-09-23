using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One tile (or loose piece) on the puzzle board: drag it into a slot, or click it (into a slot, or back out of the
// one it is in). Made by the board. It glides where the board sends it rather than jumping, growing, shrinking and
// turning on the way (a loose stone piece straightens and grows to its size in the disc).
public class PuzzleTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    public string Id { get; private set; }
    public Vector2 Home { get; private set; }      // where it sits in the pool
    public Vector2 HomeSize { get; private set; }
    public float HomeAngle { get; private set; }
    public PuzzleSlot Slot { get; set; }           // the slot it is in, or null
    public RectTransform Rect { get; private set; }
    public Image Image => image;

    private PuzzleBoard board;
    private RectTransform canvasRect;
    private Image image;
    private bool dragging;
    private bool gliding;
    private Vector2 targetPosition;
    private Vector2 targetSize;
    private float targetAngle;

    // Its home is where it is now: position, size and turn.
    public PuzzleTile Init(PuzzleBoard owner, string id, RectTransform canvas)
    {
        board = owner;
        Id = id;
        canvasRect = canvas;
        Rect = (RectTransform)transform;
        image = GetComponent<Image>();
        Home = Rect.anchoredPosition;
        HomeSize = Rect.sizeDelta;
        HomeAngle = Rect.localEulerAngles.z;
        targetPosition = Home;
        targetSize = HomeSize;
        targetAngle = HomeAngle;
        return this;
    }

    // Glide to a spot under a new parent (a slot, or the board for home), from wherever it is on screen now.
    public void GoTo(RectTransform parent, Vector2 position, Vector2 size, float angle)
    {
        Vector3 world = Rect.position;
        Quaternion turn = Rect.rotation;
        Rect.SetParent(parent, true);
        Rect.anchorMin = Rect.anchorMax = Rect.pivot = new Vector2(0.5f, 0.5f);
        Rect.position = world;
        Rect.rotation = turn;
        targetPosition = position;
        targetSize = size;
        targetAngle = angle;
        gliding = true;
    }

    // While it is held: the size and turn it eases to (the board asks for its size in place).
    public void Hold(Vector2 size, float angle)
    {
        targetSize = size;
        targetAngle = angle;
    }

    private void Update()
    {
        if (Rect == null)
            return;
        float k = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
        Rect.sizeDelta = Vector2.Lerp(Rect.sizeDelta, targetSize, k);
        Rect.localRotation = Quaternion.Slerp(Rect.localRotation, Quaternion.Euler(0f, 0f, targetAngle), k);
        if (gliding && !dragging)
        {
            Rect.anchoredPosition = Vector2.Lerp(Rect.anchoredPosition, targetPosition, k);
            if ((Rect.anchoredPosition - targetPosition).sqrMagnitude < 0.25f)
            {
                Rect.anchoredPosition = targetPosition;
                gliding = false;
            }
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!board.Interactive)
            return;
        dragging = true;
        gliding = false;
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
