using System;
using UnityEngine;

// A drag-and-click puzzle for the board (PuzzleBoard): the tiles on offer, which of them go in the slots and in what
// order, and every picture the board draws with, so the real art is a matter of dropping sprites onto this asset.
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

    [Header("Tiles")]
    public Tile[] tiles = new Tile[0];
    [Tooltip("The tile ids that solve it, slot by slot. There are as many slots as entries here.")]
    public string[] solution = new string[0];
    [Tooltip("Mix the tiles up each time the board opens.")]
    public bool shuffle = true;

    public int SlotCount => solution != null ? solution.Length : 0;
}
