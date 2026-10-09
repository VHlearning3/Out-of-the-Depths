using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Room 1, the spawn room, in the team's materials: its floor deck (the thin Grid Floor layer, like the middle room's tile
// floor) laid with wood (Art/Materials/wood_texture 1) repeating every Floor Tile metres, and stone
// (Art/Materials/stone_texture) on every wall and on the ceiling.
// The walls and the roof are shared with the rooms next door (the hull runs on past rooms 3 and 7, the roof covers the
// whole ship, the east wall is the middle room's too), so they keep their own look: the room gets thin linings instead,
// a Tiled Surface over each face of wall and roof that faces into it, 1 cm in front, cut to the room. Doorways and the
// portholes stay open, since a lining only covers wall that is there; door frames, doors and windows keep their own
// look. Linings have no colliders (the walls behind still stop you) and tile the stone in metres, lined up from piece
// to piece, so it keeps one size on every wall.
// ShipGreyboxBuilder dresses the room after a rebuild; Tools > Out of the Depths > Dress Spawn Room does the open scene.
// Safe to run again: the old linings go first.
// Any other floor deck gets the same wood, or the stone, with Tools > Out of the Depths > Wood / Stone Floor On Selected
// Grid Floors (or the Grid Floor component's own menu): select the decks (or the rooms they are in) and run it. Those
// decks are written down in Assets/Settings/WoodFloors.txt or StoneFloors.txt (room and middle of each deck), so a
// rebuild lays them again. Every command here saves the scene it changed, and none runs in Play mode (lost on stop).
public static class RoomDressTools
{
    public const string FloorMaterialPath = "Assets/Art/Materials/wood_texture 1.mat";
    public const string WallMaterialPath = "Assets/Art/Materials/stone_texture.mat";
    public const string SpawnRoomName = "Room_1_Spawn";
    // The spawn room's walls (their middle lines) and its floor and ceiling, as ShipGreyboxBuilder lays them out.
    public static readonly Rect SpawnRoomArea = Rect.MinMaxRect(-29f, 8.5f, -12.25f, 28.75f);
    public const float FloorY = 0f;
    public const float CeilingY = 5f;

    private const float FloorTile = 3f;    // metres of floor per repeat of the wood: planks about 25 cm wide
    private const float StoneTile = 2f;    // metres of wall per repeat of the stone
    private const float Lift = 0.01f;      // how far a lining stands off what it covers
    private const float DeckLift = 0.03f;  // the floor deck over the hull slab, as the builder lays it
    private const float Near = 0.05f;      // how close a wall's sides must come to the room's line to count as its wall
    private const string LiningsName = "Linings";

    [MenuItem("Tools/Out of the Depths/Dress Spawn Room (Wood Floor, Stone Walls)")]
    public static void DressSpawnRoomInOpenScene()
    {
        if (!EditMode("Room dress"))
            return;
        GameObject room = GameObject.Find(SpawnRoomName);
        if (room == null)
        {
            Debug.LogWarning($"Room dress: no {SpawnRoomName} in the open scene.");
            return;
        }
        if (Dress(room.transform, SpawnRoomArea, FloorY, CeilingY, true))
        {
            SaveScene(room.scene);
            Debug.Log($"Room dress: {SpawnRoomName} has its wood floor and stone walls and ceiling (scene saved).", room);
        }
    }

    // For the builder, after the whole ship is up (walls, roof and the portholes cut through the hull): the spawn room,
    // then every deck written down in Settings/WoodFloors.txt and Settings/StoneFloors.txt.
    public static void DressShip(Transform ship)
    {
        DressSpawnRoom(ship);
        FloorLooksFromLists(ship);
    }

    public static void DressSpawnRoom(Transform ship)
    {
        Transform room = ship != null ? Find(ship, SpawnRoomName) : null;
        if (room != null)
            Dress(room, SpawnRoomArea, FloorY, CeilingY, false);
    }

    // Every floor look again in the open scene, as a rebuild would lay them, and the scene saved: the middle room's
    // tile floor, the spawn room's wood and stone, and every deck in WoodFloors.txt and StoneFloors.txt. For after a
    // scene merge (or anyone's hand) has put a floor back to the grid, or switched its Grid Floor off.
    [MenuItem("Tools/Out of the Depths/Reapply All Floor Looks")]
    public static void ReapplyAllFloors()
    {
        if (!EditMode("Floors"))
            return;
        GameObject spawn = GameObject.Find(SpawnRoomName);
        GameObject middle = GameObject.Find("Room_2_Middle");
        Transform ship = spawn != null ? spawn.transform.root : middle != null ? middle.transform.root : null;
        if (ship == null)
        {
            Debug.LogWarning("Floors: no ship (Room_1_Spawn / Room_2_Middle) in the open scene.");
            return;
        }
        if (middle != null)
            foreach (GridFloor floor in middle.GetComponentsInChildren<GridFloor>(true))
                if (floor.transform.parent == middle.transform)
                    TileFloorTools.Apply(floor);
        if (spawn != null)
            Dress(spawn.transform, SpawnRoomArea, FloorY, CeilingY, true);
        FloorLooksFromLists(ship, true);
        SaveScene(ship.gameObject.scene);
        Debug.Log("Floors: every floor look laid again and the scene saved.", ship);
    }

    // ---- wood or stone on chosen decks ----------------------------------------------------------------------------

    // A floor look for any deck: its material, how many metres of floor a repeat of the texture covers, and the list in
    // Assets/Settings it is remembered in (so a rebuild lays it again). A deck is in one list at a time.
    private sealed class FloorLook
    {
        public string Name, MaterialPath, ListPath;
        public float Tile;
    }

    private static readonly FloorLook Wood = new FloorLook
    {
        Name = "wood", MaterialPath = FloorMaterialPath, ListPath = "Assets/Settings/WoodFloors.txt", Tile = FloorTile,
    };
    private static readonly FloorLook Stone = new FloorLook
    {
        Name = "stone", MaterialPath = WallMaterialPath, ListPath = "Assets/Settings/StoneFloors.txt", Tile = StoneTile,
    };
    private static readonly FloorLook[] Looks = { Wood, Stone };

    [MenuItem("Tools/Out of the Depths/Wood Floor On Selected Grid Floors")]
    public static void WoodOnSelection() => LookOn(SelectedFloors(), Wood);

    [MenuItem("Tools/Out of the Depths/Stone Floor On Selected Grid Floors")]
    public static void StoneOnSelection() => LookOn(SelectedFloors(), Stone);

    [MenuItem("Tools/Out of the Depths/Wood Floor On Selected Grid Floors", true)]
    [MenuItem("Tools/Out of the Depths/Stone Floor On Selected Grid Floors", true)]
    private static bool SelectionHasFloors()
    {
        foreach (GameObject go in Selection.gameObjects)
            if (go.GetComponentInChildren<GridFloor>(true) != null)
                return true;
        return false;
    }

    [MenuItem("CONTEXT/GridFloor/Wood Floor (wood_texture 1)")]
    private static void WoodOnThis(MenuCommand command)
    {
        if (command.context is GridFloor floor)
            LookOn(new List<GridFloor> { floor }, Wood);
    }

    [MenuItem("CONTEXT/GridFloor/Stone Floor (stone_texture)")]
    private static void StoneOnThis(MenuCommand command)
    {
        if (command.context is GridFloor floor)
            LookOn(new List<GridFloor> { floor }, Stone);
    }

    // The selected decks, and every deck in a selected room.
    private static List<GridFloor> SelectedFloors()
    {
        var floors = new List<GridFloor>();
        foreach (GameObject go in Selection.gameObjects)
            foreach (GridFloor floor in go.GetComponentsInChildren<GridFloor>(true))
                if (!floors.Contains(floor) && !EditorUtility.IsPersistent(floor))
                    floors.Add(floor);
        return floors;
    }

    private static void LookOn(List<GridFloor> floors, FloorLook look)
    {
        if (!EditMode($"{look.Name} floor"))
            return;
        if (floors.Count == 0)
        {
            Debug.LogWarning($"{look.Name} floor: select one or more Grid Floor decks (or the rooms they are in) first.");
            return;
        }
        var material = AssetDatabase.LoadAssetAtPath<Material>(look.MaterialPath);
        if (material == null)
        {
            Debug.LogWarning($"{look.Name} floor: no material at {look.MaterialPath}.");
            return;
        }
        var names = new List<string>();
        var scenes = new List<UnityEngine.SceneManagement.Scene>();
        foreach (GridFloor floor in floors)
        {
            Undo.RecordObject(floor, $"{look.Name} floor");
            floor.enabled = true;   // switched off, a Grid Floor has no mesh at all
            floor.SetLook(GridFloor.Mapping.Tile, Color.white, material, look.Tile);
            EditorUtility.SetDirty(floor);
            if (!scenes.Contains(floor.gameObject.scene))
                scenes.Add(floor.gameObject.scene);
            names.Add(DeckKey(floor));
        }
        List<string> switched = Remember(floors, look);
        Save(scenes);
        Debug.Log($"{look.Name} floor: {floors.Count} deck(s) laid and saved, remembered in {look.ListPath} for rebuilds: {string.Join("; ", names)}");
        // Wood and stone go on different decks: say so if any of these had the other look until now.
        if (switched.Count > 0)
            Debug.LogWarning($"{look.Name} floor: {switched.Count} of these deck(s) had another floor until now and lost it (Ctrl+Z puts it back): {string.Join("; ", switched)}");
    }

    // Written into this look's list and out of every other one. Returns the decks that were in another look's list
    // ("stone, was wood: Room_7_BoxRoom 13.375 15").
    private static List<string> Remember(List<GridFloor> floors, FloorLook look)
    {
        var switched = new List<string>();
        foreach (FloorLook other in Looks)
        {
            List<string> list = ReadList(other);
            int before = list.Count;
            if (other != look)
                foreach (string line in list)
                    if (floors.Exists(floor => SameDeck(line, floor)))
                        switched.Add($"{line} (was {other.Name})");
            list.RemoveAll(line => floors.Exists(floor => SameDeck(line, floor)));
            if (other == look)
                foreach (GridFloor floor in floors)
                    list.Add(DeckKey(floor));
            if (other == look || list.Count != before)
                WriteList(other, list);
        }
        return switched;
    }

    // A deck given another look by hand since (the middle room's tile floor) is not laid with wood or stone again by a
    // rebuild.
    public static void ForgetFloorLooks(GridFloor floor)
    {
        if (floor == null)
            return;
        foreach (FloorLook look in Looks)
        {
            List<string> list = ReadList(look);
            int before = list.Count;
            list.RemoveAll(line => SameDeck(line, floor));
            if (list.Count != before)
                WriteList(look, list);
        }
    }

    // After a rebuild: every deck in the lists laid again with its wood or stone.
    public static void FloorLooksFromLists(Transform ship, bool undoable = false)
    {
        if (ship == null)
            return;
        GridFloor[] floors = ship.GetComponentsInChildren<GridFloor>(true);
        foreach (FloorLook look in Looks)
        {
            List<string> list = ReadList(look);
            var material = AssetDatabase.LoadAssetAtPath<Material>(look.MaterialPath);
            if (list.Count == 0 || material == null)
                continue;
            int done = 0;
            foreach (GridFloor floor in floors)
                foreach (string line in list)
                    if (SameDeck(line, floor))
                    {
                        if (undoable)
                            Undo.RecordObject(floor, "Reapply Floors");
                        floor.enabled = true;
                        floor.SetLook(GridFloor.Mapping.Tile, Color.white, material, look.Tile);
                        EditorUtility.SetDirty(floor);
                        done++;
                        break;
                    }
            Debug.Log($"{look.Name} floor: {done} of the {list.Count} deck(s) in {look.ListPath} laid.");
        }
    }

    // "Room_3_FishRoom -20.625 33.5": the deck's room and the x and z of its middle (always with a point, any language).
    private static string DeckKey(GridFloor floor)
    {
        Vector3 p = floor.transform.position;
        string room = floor.transform.parent != null ? floor.transform.parent.name : "-";
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} {1:0.###} {2:0.###}", room, p.x, p.z);
    }

    private static bool SameDeck(string line, GridFloor floor)
    {
        string[] parts = line.Split(' ');
        if (parts.Length < 3)
            return false;
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        if (!float.TryParse(parts[parts.Length - 2], System.Globalization.NumberStyles.Float, invariant, out float x)
            || !float.TryParse(parts[parts.Length - 1], System.Globalization.NumberStyles.Float, invariant, out float z))
            return false;
        string room = string.Join(" ", parts, 0, parts.Length - 2);
        Transform parent = floor.transform.parent;
        Vector3 p = floor.transform.position;
        return parent != null && parent.name == room && Mathf.Abs(p.x - x) < 0.05f && Mathf.Abs(p.z - z) < 0.05f;
    }

    private static List<string> ReadList(FloorLook look)
    {
        var list = new List<string>();
        if (!System.IO.File.Exists(look.ListPath))
            return list;
        foreach (string raw in System.IO.File.ReadAllLines(look.ListPath))
        {
            string line = raw.Trim();
            if (line.Length > 0 && !line.StartsWith("#") && !list.Contains(line))
                list.Add(line);
        }
        return list;
    }

    private static void WriteList(FloorLook look, List<string> list)
    {
        var unique = new List<string>();
        foreach (string line in list)
            if (!unique.Contains(line))
                unique.Add(line);
        unique.Sort(System.StringComparer.Ordinal);
        string header =
            $"# Floor decks (Grid Floor) laid with {look.Name} ({look.MaterialPath}), written by Room Dress Tools.\n" +
            "# One deck a line: its room, then the x and z of its middle. ShipGreyboxBuilder lays them again after a rebuild.\n";
        System.IO.File.WriteAllText(look.ListPath, header + string.Join("\n", unique) + "\n");
        AssetDatabase.ImportAsset(look.ListPath);
    }

    // ---- keeping it ---------------------------------------------------------------------------------------------------

    // Changes made in Play mode are thrown away when it stops: these tools only run in Edit mode.
    private static bool EditMode(string what)
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            return true;
        Debug.LogWarning($"{what}: stop Play mode first (anything changed in Play mode is lost when it stops).");
        return false;
    }

    // The changed scenes saved straight away, so nothing is lost by forgetting to.
    private static void Save(List<UnityEngine.SceneManagement.Scene> scenes)
    {
        foreach (var scene in scenes)
        {
            if (!scene.IsValid())
                continue;
            EditorSceneManager.MarkSceneDirty(scene);
            if (string.IsNullOrEmpty(scene.path))
            {
                Debug.LogWarning($"{scene.name} has never been saved, so it was not saved now: save it once yourself.");
                continue;
            }
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }

    public static void SaveScene(UnityEngine.SceneManagement.Scene scene) =>
        Save(new List<UnityEngine.SceneManagement.Scene> { scene });

    // area: the room's walls (middle lines, x and z); floorY / ceilingY: its floor and the underside of its roof.
    public static bool Dress(Transform room, Rect area, float floorY, float ceilingY, bool undoable)
    {
        var wood = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
        var stone = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
        if (wood == null)
            Debug.LogWarning($"Room dress: no material at {FloorMaterialPath}, the floor stays as it is.");
        if (stone == null)
            Debug.LogWarning($"Room dress: no material at {WallMaterialPath}, the walls stay as they are.");
        if (wood == null && stone == null)
            return false;

        if (wood != null)
            WoodFloor(room, area, floorY, wood, undoable);
        if (stone != null)
            StoneLinings(room, area, floorY, ceilingY, stone, undoable);
        return true;
    }

    // ---- the floor ------------------------------------------------------------------------------------------------

    // The room's own floor deck (made again if it is gone), switched on, laid with the wood.
    private static void WoodFloor(Transform room, Rect area, float floorY, Material wood, bool undoable)
    {
        GridFloor deck = null;
        foreach (GridFloor floor in room.GetComponentsInChildren<GridFloor>(true))
            if (floor.transform.parent == room)
            {
                deck = floor;
                break;
            }

        if (deck == null)
        {
            var go = new GameObject("Deck");
            go.transform.SetParent(room, false);
            go.transform.position = new Vector3(area.center.x, floorY + DeckLift, area.center.y);
            deck = go.AddComponent<GridFloor>();
            deck.Setup(area.size, Color.white, wood);
            if (undoable)
                Undo.RegisterCreatedObjectUndo(go, "Dress Spawn Room");
        }
        else if (undoable)
        {
            Undo.RecordObject(deck, "Dress Spawn Room");
            Undo.RecordObject(deck.gameObject, "Dress Spawn Room");
        }

        deck.gameObject.SetActive(true);
        deck.enabled = true;   // switched off, it has no mesh at all (the floor showed the bare hull slab)
        deck.SetLook(GridFloor.Mapping.Tile, Color.white, wood, FloorTile);
        EditorUtility.SetDirty(deck);
    }

    // ---- the walls and the ceiling ----------------------------------------------------------------------------------

    private static void StoneLinings(Transform room, Rect area, float floorY, float ceilingY, Material stone, bool undoable)
    {
        Transform old = room.Find(LiningsName);
        if (old != null)
        {
            if (undoable)
                Undo.DestroyObjectImmediate(old.gameObject);
            else
                Object.DestroyImmediate(old.gameObject);
        }
        var linings = new GameObject(LiningsName).transform;
        linings.SetParent(room, false);

        List<Bounds> boxes = Boxes(room.gameObject.scene);
        var west = new List<Bounds>();
        var east = new List<Bounds>();
        var south = new List<Bounds>();
        var north = new List<Bounds>();
        var roof = new List<Bounds>();
        foreach (Bounds b in boxes)
        {
            bool inHeight = Overlap(b.min.y, b.max.y, floorY, ceilingY);
            if (inHeight && OnLine(b, 0, area.xMin) && Overlap(b.min.z, b.max.z, area.yMin, area.yMax))
                west.Add(b);
            if (inHeight && OnLine(b, 0, area.xMax) && Overlap(b.min.z, b.max.z, area.yMin, area.yMax))
                east.Add(b);
            if (inHeight && OnLine(b, 2, area.yMin) && Overlap(b.min.x, b.max.x, area.xMin, area.xMax))
                south.Add(b);
            if (inHeight && OnLine(b, 2, area.yMax) && Overlap(b.min.x, b.max.x, area.xMin, area.xMax))
                north.Add(b);
            if (Mathf.Abs(b.min.y - ceilingY) < Near && b.max.y > ceilingY + Near && b.size.y < 1.5f
                && Overlap(b.min.x, b.max.x, area.xMin, area.xMax) && Overlap(b.min.z, b.max.z, area.yMin, area.yMax))
                roof.Add(b);
        }

        // The room's inside faces: where its walls stop.
        float x0 = Face(west, b => b.max.x, true, area.xMin + 0.25f);
        float x1 = Face(east, b => b.min.x, false, area.xMax - 0.25f);
        float z0 = Face(south, b => b.max.z, true, area.yMin + 0.25f);
        float z1 = Face(north, b => b.min.z, false, area.yMax - 0.25f);

        int count = 0;
        foreach (Bounds b in west)
            count += Wall(linings, "Lining_W", Vector3.right, b.max.x + Lift, b.min.z, b.max.z, z0, z1, b, floorY, ceilingY, stone);
        foreach (Bounds b in east)
            count += Wall(linings, "Lining_E", Vector3.left, b.min.x - Lift, b.min.z, b.max.z, z0, z1, b, floorY, ceilingY, stone);
        foreach (Bounds b in south)
            count += Wall(linings, "Lining_S", Vector3.forward, b.max.z + Lift, b.min.x, b.max.x, x0, x1, b, floorY, ceilingY, stone);
        foreach (Bounds b in north)
            count += Wall(linings, "Lining_N", Vector3.back, b.min.z - Lift, b.min.x, b.max.x, x0, x1, b, floorY, ceilingY, stone);
        foreach (Bounds b in roof)
        {
            float a0 = Mathf.Max(b.min.x, x0), a1 = Mathf.Min(b.max.x, x1);
            float c0 = Mathf.Max(b.min.z, z0), c1 = Mathf.Min(b.max.z, z1);
            if (a1 - a0 < 0.01f || c1 - c0 < 0.01f)
                continue;
            var centre = new Vector3((a0 + a1) * 0.5f, b.min.y - Lift, (c0 + c1) * 0.5f);
            Quaternion facing = Quaternion.LookRotation(Vector3.forward, Vector3.down);   // its face down, its X along -x
            Panel(linings, "Lining_Ceiling", centre, facing, new Vector2(a1 - a0, c1 - c0), new Vector2(-centre.x, centre.z), stone);
            count++;
        }

        if (undoable)
            Undo.RegisterCreatedObjectUndo(linings.gameObject, "Dress Spawn Room");
        Debug.Log($"Room dress: {room.name} lined with {count} stone panel(s): walls W {west.Count}, E {east.Count}, S {south.Count}, N {north.Count}, ceiling {roof.Count} piece(s).", linings);
    }

    // One wall piece's face into the room: along `along` from a0 to a1 (cut to the room's inside, lo..hi), floor to
    // ceiling as far as the piece goes. Returns 1 for a panel laid, 0 if nothing of it shows in the room.
    private static int Wall(Transform parent, string name, Vector3 normal, float plane, float a0, float a1, float lo, float hi,
                            Bounds b, float floorY, float ceilingY, Material stone)
    {
        a0 = Mathf.Max(a0, lo);
        a1 = Mathf.Min(a1, hi);
        float y0 = Mathf.Max(b.min.y, floorY), y1 = Mathf.Min(b.max.y, ceilingY);
        if (a1 - a0 < 0.01f || y1 - y0 < 0.01f)
            return 0;
        float a = (a0 + a1) * 0.5f, y = (y0 + y1) * 0.5f;
        Vector3 centre = Mathf.Abs(normal.x) > 0.5f ? new Vector3(plane, y, a) : new Vector3(a, y, plane);
        Quaternion facing = Quaternion.LookRotation(Vector3.up, normal);   // its face out of the wall, its Z up the wall
        Vector3 across = facing * Vector3.right;
        Panel(parent, name, centre, facing, new Vector2(a1 - a0, y1 - y0), new Vector2(Vector3.Dot(centre, across), centre.y), stone);
        return 1;
    }

    private static void Panel(Transform parent, string name, Vector3 centre, Quaternion facing, Vector2 size, Vector2 middle, Material material)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(centre, facing);
        go.AddComponent<TiledSurface>().Setup(size, StoneTile, middle, material);
    }

    // Every solid box in the scene (a scaled cube, square to the world): walls, wall pieces round holes, roof slabs.
    // Door frames, doors, windows and pickups are left out: they keep their own look.
    private static List<Bounds> Boxes(UnityEngine.SceneManagement.Scene scene)
    {
        var boxes = new List<Bounds>();
        foreach (GameObject top in scene.GetRootGameObjects())
            foreach (MeshRenderer r in top.GetComponentsInChildren<MeshRenderer>())
            {
                if (!r.enabled || r.GetComponent<TiledSurface>() != null || r.GetComponent<GridFloor>() != null)
                    continue;
                var filter = r.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount != 24)
                    continue;
                if (r.name.StartsWith("Frame_") || r.GetComponentInParent<Door>() != null || r.GetComponentInParent<DoubleDoor>() != null
                    || r.GetComponentInParent<FishWindow>() != null || r.GetComponentInParent<PickupItem>() != null)
                    continue;
                if (r.TryGetComponent(out Collider collider) && collider.isTrigger)
                    continue;
                if (!Square(r.transform))
                    continue;
                boxes.Add(r.bounds);
            }
        return boxes;
    }

    // Its sides straddle the room's line on that axis and it is wall-thin there (not a slab the line runs through).
    private static bool OnLine(Bounds b, int axis, float line) =>
        b.min[axis] < line + Near && b.max[axis] > line - Near && b.size[axis] < 1.5f;

    private static bool Overlap(float a0, float a1, float b0, float b1) => Mathf.Min(a1, b1) - Mathf.Max(a0, b0) > 0.01f;

    private static float Face(List<Bounds> walls, System.Func<Bounds, float> side, bool highest, float fallback)
    {
        if (walls.Count == 0)
            return fallback;
        float best = side(walls[0]);
        foreach (Bounds b in walls)
            best = highest ? Mathf.Max(best, side(b)) : Mathf.Min(best, side(b));
        return best;
    }

    private static bool Square(Transform t)
    {
        foreach (Vector3 axis in new[] { t.right, t.up, t.forward })
            if (Mathf.Max(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z)) < 0.999f)
                return false;
        return true;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name)
                return t;
        return null;
    }
}
