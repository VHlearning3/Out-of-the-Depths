using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The drag-and-click puzzle board (the GDD's stone fragment and rune puzzles): a row of slots along the top and the
// tiles on offer below. Drag a tile into a slot or click it to send it to the next free one; click a placed tile to
// take it back. The moment every slot holds the right tile it is solved. Everything it draws comes from a
// PuzzleDefinition (board, slot and tile pictures, the tiles' art), with plain placeholders when the asset has none.
// Built on a screen-space canvas of its own the first time it is needed; freezes the player and frees the cursor
// while it is up; Escape or the cross closes it. PuzzleBoard.Show(puzzle, onSolved) opens it (a PuzzleStation does).
public class PuzzleBoard : MonoBehaviour
{
    private static PuzzleBoard instance;
    private static Sprite fallbackBoard;
    private static Sprite fallbackSlot;
    private static Sprite fallbackTile;

    public static bool IsOpen => instance != null && instance.open;

    private PuzzleDefinition puzzle;
    private Action onSolved;
    private bool open;
    private bool solved;
    private RectTransform canvasRect;
    private RectTransform root;
    private RectTransform board;
    private Text hint;
    private readonly List<PuzzleSlot> slots = new List<PuzzleSlot>();
    private PuzzleTile[] placed = new PuzzleTile[0];
    private SwimController swimmer;
    private PlayerInteractor interactor;
    private PlayerInventory inventory;
    private SlashAttack slash;
    private bool attackWas;

    public bool Interactive => open && !solved;
    public RectTransform DragLayer => root;

    public static void Show(PuzzleDefinition puzzle, Action onSolved)
    {
        if (puzzle == null)
            return;
        if (instance == null)
            instance = new GameObject("PuzzleBoard").AddComponent<PuzzleBoard>();
        instance.Open(puzzle, onSolved);
    }

    public static void Hide()
    {
        if (instance != null)
            instance.Close();
    }

    private void Awake()
    {
        instance = this;
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        canvasRect = (RectTransform)transform;

        root = NewRect("Root", canvasRect);
        Stretch(root);
        root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (open && !solved && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void Open(PuzzleDefinition definition, Action solvedCallback)
    {
        puzzle = definition;
        onSolved = solvedCallback;
        solved = false;
        Build();
        root.gameObject.SetActive(true);
        open = true;

        // The player stands still and cannot look round, interact, swap items or swing the dagger at the tiles.
        swimmer = FindFirstObjectByType<SwimController>();
        interactor = FindFirstObjectByType<PlayerInteractor>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        slash = FindFirstObjectByType<SlashAttack>();
        if (swimmer != null)
        {
            swimmer.Frozen = true;
            swimmer.LookLocked = true;
        }
        if (interactor != null)
            interactor.Busy = true;
        if (inventory != null)
            inventory.InputBlocked = true;
        if (slash != null)
        {
            attackWas = slash.CanAttack;
            slash.SetCanAttack(false);
        }
        HitMarker.SetReticleVisible(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PauseMenu.CaptureInput(true);
    }

    private void Close()
    {
        if (!open)
            return;
        open = false;
        root.gameObject.SetActive(false);
        if (swimmer != null)
        {
            swimmer.Frozen = false;
            swimmer.LookLocked = false;
        }
        if (interactor != null)
            interactor.Busy = false;
        if (inventory != null)
            inventory.InputBlocked = false;
        if (slash != null)
            slash.SetCanAttack(attackWas);
        HitMarker.SetReticleVisible(true);
        PauseMenu.CaptureInput(false);
        SetGameCursor();
        StartCoroutine(RestoreCursor());
    }

    // Back to the game's cursor, locked and hidden, unless the pause menu has it. Not to whatever was captured on
    // opening: that could be a leftover free cursor from an earlier board, which is how it stayed on screen after a
    // solve.
    private static void SetGameCursor()
    {
        bool free = PauseMenu.IsOpen;
        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    // The editor lets go of the cursor lock on Escape after this has set it back: keep setting it for a moment.
    private IEnumerator RestoreCursor()
    {
        float until = Time.unscaledTime + 0.4f;
        while (Time.unscaledTime < until && !open)
        {
            SetGameCursor();
            yield return null;
        }
    }

    // ---- building it ------------------------------------------------------------------------------------------------

    private void Build()
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Destroy(root.GetChild(i).gameObject);
        slots.Clear();
        placed = new PuzzleTile[puzzle.SlotCount];

        Image backdrop = NewImage("Backdrop", root, null, new Color(0f, 0f, 0f, 0.62f));
        Stretch(backdrop.rectTransform);

        Image boardImage = NewImage("Board", root, puzzle.board != null ? puzzle.board : FallbackBoard, puzzle.boardTint);
        board = boardImage.rectTransform;
        Place(board, new Vector2(0.5f, 0.5f), Vector2.zero, puzzle.boardSize);
        float w = puzzle.boardSize.x, h = puzzle.boardSize.y;

        Text title = NewText("Title", board, puzzle.title, 40, puzzle.textColor);
        Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(w - 200f, 60f));
        hint = NewText("Hint", board, puzzle.hint, 22, puzzle.textColor);
        Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(w - 160f, 70f));

        Image close = NewImage("Close", board, puzzle.tileFrame != null ? puzzle.tileFrame : FallbackTile, Color.white);
        Place(close.rectTransform, new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(56f, 56f));
        Text cross = NewText("X", close.rectTransform, "X", 30, puzzle.textColor);
        Stretch(cross.rectTransform);
        cross.raycastTarget = false;
        close.gameObject.AddComponent<Button>().onClick.AddListener(Close);

        // The slots along the top.
        float slotSize = puzzle.tileSize + 16f;
        float slotY = h * 0.5f - 200f;
        int count = puzzle.SlotCount;
        for (int i = 0; i < count; i++)
        {
            Image slot = NewImage("Slot" + i, board, puzzle.slot != null ? puzzle.slot : FallbackSlot, Color.white);
            Place(slot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2((i - (count - 1) * 0.5f) * (slotSize + 12f), slotY), new Vector2(slotSize, slotSize));
            slots.Add(slot.gameObject.AddComponent<PuzzleSlot>().Init(i));
        }

        // The tiles on offer, in rows below, mixed up.
        var order = new List<PuzzleDefinition.Tile>();
        foreach (PuzzleDefinition.Tile tile in puzzle.tiles)
            if (tile != null)
                order.Add(tile);
        if (puzzle.shuffle)
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        int columns = Mathf.Max(1, Mathf.Min(puzzle.tilesPerRow, order.Count));
        float step = puzzle.tileSize + 24f;
        float topY = slotY - slotSize * 0.5f - 70f - puzzle.tileSize * 0.5f;
        for (int k = 0; k < order.Count; k++)
        {
            int row = k / columns, column = k % columns;
            int inRow = Mathf.Min(columns, order.Count - row * columns);
            var home = new Vector2((column - (inRow - 1) * 0.5f) * step, topY - row * step);
            Image frame = NewImage("Tile_" + order[k].id, board, puzzle.tileFrame != null ? puzzle.tileFrame : FallbackTile, Color.white);
            Place(frame.rectTransform, new Vector2(0.5f, 0.5f), home, new Vector2(puzzle.tileSize, puzzle.tileSize));
            if (order[k].art != null)
            {
                Image art = NewImage("Art", frame.rectTransform, order[k].art, Color.white);
                art.preserveAspect = true;
                art.raycastTarget = false;
                Place(art.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(puzzle.tileSize - 24f, puzzle.tileSize - 24f));
            }
            else
            {
                Text label = NewText("Label", frame.rectTransform, string.IsNullOrEmpty(order[k].label) ? order[k].id : order[k].label, 26, puzzle.textColor);
                Stretch(label.rectTransform);
                label.raycastTarget = false;
            }
            frame.gameObject.AddComponent<PuzzleTile>().Init(this, order[k].id, home, canvasRect);
        }
    }

    // ---- the tiles talk to it ----------------------------------------------------------------------------------------

    public void BeginDrag(PuzzleTile tile)
    {
        if (tile.Slot != null)
        {
            placed[tile.Slot.Index] = null;
            tile.Slot = null;
        }
    }

    public void Dropped(PuzzleTile tile, PuzzleSlot slot)
    {
        if (slot != null && Interactive)
            PlaceTile(tile, slot);
        else
            ReturnToPool(tile);
        Check();
    }

    public void Clicked(PuzzleTile tile)
    {
        if (!Interactive)
            return;
        if (tile.Slot != null)
        {
            placed[tile.Slot.Index] = null;
            tile.Slot = null;
            ReturnToPool(tile);
        }
        else
        {
            for (int i = 0; i < placed.Length; i++)
                if (placed[i] == null)
                {
                    PlaceTile(tile, slots[i]);
                    break;
                }
        }
        Check();
    }

    private void PlaceTile(PuzzleTile tile, PuzzleSlot slot)
    {
        PuzzleTile other = placed[slot.Index];
        if (other != null && other != tile)
        {
            other.Slot = null;
            ReturnToPool(other);
        }
        placed[slot.Index] = tile;
        tile.Slot = slot;
        RectTransform rect = tile.Rect;
        rect.SetParent(slot.Rect, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
    }

    private void ReturnToPool(PuzzleTile tile)
    {
        RectTransform rect = tile.Rect;
        rect.SetParent(board, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = tile.Home;
    }

    private void Check()
    {
        if (solved)
            return;
        for (int i = 0; i < placed.Length; i++)
            if (placed[i] == null || placed[i].Id != puzzle.solution[i])
                return;
        solved = true;
        hint.text = puzzle.solvedText;
        foreach (PuzzleSlot slot in slots)
            if (slot.Image != null)
                slot.Image.color = puzzle.highlight;
        StartCoroutine(Finish());
    }

    private IEnumerator Finish()
    {
        yield return new WaitForSecondsRealtime(0.9f);
        Close();
        onSolved?.Invoke();
    }

    // ---- small uGUI helpers ------------------------------------------------------------------------------------------

    private static RectTransform NewRect(string name, RectTransform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    private static Text NewText(string name, RectTransform parent, string content, int size, Color color)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<Text>();
        text.text = content;
        text.font = GameFont.Font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // ---- placeholders when the puzzle asset has no pictures ------------------------------------------------------------

    private static Sprite FallbackBoard => fallbackBoard != null ? fallbackBoard : (fallbackBoard = Rounded(96, 24f, new Color(0.42f, 0.28f, 0.14f), new Color(0.22f, 0.13f, 0.06f), 6f, 32));
    private static Sprite FallbackSlot => fallbackSlot != null ? fallbackSlot : (fallbackSlot = Rounded(64, 12f, new Color(0.2f, 0.13f, 0.07f), new Color(0.1f, 0.06f, 0.03f), 3f, 16));
    private static Sprite FallbackTile => fallbackTile != null ? fallbackTile : (fallbackTile = Rounded(64, 10f, new Color(0.93f, 0.87f, 0.7f), new Color(0.55f, 0.45f, 0.3f), 3f, 16));

    // A rounded square with a rim, 9-sliceable by its border.
    private static Sprite Rounded(int size, float radius, Color fill, Color rim, float rimWidth, int border)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float py = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
                Color c = d < -rimWidth ? fill : rim;
                c.a = Mathf.Clamp01(0.5f - d);
                pixels[y * size + x] = c;
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
