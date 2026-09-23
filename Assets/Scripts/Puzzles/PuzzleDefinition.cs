using System;
using UnityEngine;

// A drag-and-click puzzle for the board (PuzzleBoard): the tiles on offer, which of them go in the slots and in what
// order, and every picture the board draws with, so the real art is a matter of dropping sprites onto this asset.
// Two layouts: Row (slots in a row, square tiles below: the runes) and Picture (loose pieces in their own shapes that
// go into a picture: the stone disc).
// The two the game ships with live in Assets/Puzzles (Tools > Out of the Depths > Create Puzzle Assets makes them
// with placeholder art in Art/UI/Puzzle); Assets > Create > Out of the Depths > Puzzle makes another.
[CreateAssetMenu(fileName = "Puzzle", menuName = "Out of the Depths/Puzzle")]
public class PuzzleDefinition : ScriptableObject
{
    [Serializable]
    public class Tile
    {
        [Tooltip("What the solution calls it.")]
        public string id = "";
        [Tooltip("The picture on the tile. Empty = the label as text.")]
        public Sprite art;
        public string label = "";
        [Tooltip("Picture layout: how the loose piece lies, in degrees; it turns upright as it goes in.")]
        public float looseAngle;
    }

    public enum Layout { Row, Picture }

    // Picture layout: where a piece sits on the picture.
    [Serializable]
    public class Spot
    {
        [Tooltip("Its middle, as shares of the picture from the picture's middle (0.25 = a quarter of the picture to the right / up).")]
        public Vector2 center;
        [Tooltip("Its size, as shares of the picture. The piece's picture should be cropped to the piece.")]
        public Vector2 size = new Vector2(0.4f, 0.4f);
        [Tooltip("Its turn in degrees.")]
        public float angle;
    }

    [Header("Words")]
    public string title = "Arrange the pieces";
    [TextArea] public string hint = "Drag a tile into a slot, or click it to send it to the next free slot. Click a placed tile to take it back.";
    public string solvedText = "It fits.";

    [Header("Art (drop the real pictures here)")]
    [Tooltip("The board behind everything. A sprite with a border is stretched by its edges (9-sliced).")]
    public Sprite board;
    public Color boardTint = Color.white;
    [Tooltip("An empty slot in the top row.")]
    public Sprite slot;
    [Tooltip("The frame every tile sits in; its picture goes on top of it.")]
    public Sprite tileFrame;
    [Tooltip("The slots turn this colour when it is solved.")]
    public Color highlight = new Color(0.35f, 0.85f, 0.95f);
    public Color textColor = new Color(0.16f, 0.1f, 0.05f);
    [Tooltip("The board on a 1920 x 1080 screen, in pixels.")]
    public Vector2 boardSize = new Vector2(1280f, 760f);
    public float tileSize = 120f;
    public int tilesPerRow = 7;

    [Header("Layout")]
    [Tooltip("Row: slots in a row along the top, square tiles below (the runes). Picture: the pieces go into Picture on the left; the loose pieces lie on the right in their own shapes, no tile frames (the stone disc).")]
    public Layout layout = Layout.Row;
    [Tooltip("Picture layout: what the pieces go into.")]
    public Sprite picture;
    [Tooltip("Picture layout: how big the picture is drawn, in board pixels.")]
    public float pictureSize = 520f;
    [Tooltip("Picture layout: the colour the picture is drawn in. Dark = it reads as the empty shape to fill (for a picture of the whole finished thing, like the stone tablet); white = as drawn (a frame with a hollow).")]
    public Color pictureTint = Color.white;
    [Tooltip("Picture layout: where each piece goes on the picture, one per Solution entry in the same order.")]
    public Spot[] spots = new Spot[0];
    [Tooltip("Picture layout: a faint silhouette of the piece that goes there marks each empty spot.")]
    public bool showSilhouettes = true;
    [Tooltip("Picture layout: only the right piece goes into a spot; a wrong one slides back.")]
    public bool rightPieceOnly = true;
    [Tooltip("Picture layout: the loose pieces are drawn this much smaller than in place.")]
    [Range(0.3f, 1f)] public float looseScale = 0.7f;
    [Tooltip("Picture layout: how near its spot (board pixels) a dropped piece has to land to go in.")]
    public float snapDistance = 140f;
    [Tooltip("Picture layout: said for a moment when a piece is dropped on the wrong spot.")]
    public string wrongPieceText = "That piece does not fit there.";

    [Header("Tiles")]
    public Tile[] tiles = new Tile[0];
    [Tooltip("The tile ids that solve it, slot by slot. There are as many slots as entries here.")]
    public string[] solution = new string[0];
    [Tooltip("Mix the tiles up each time the board opens.")]
    public bool shuffle = true;

    public int SlotCount => solution != null ? solution.Length : 0;
}
