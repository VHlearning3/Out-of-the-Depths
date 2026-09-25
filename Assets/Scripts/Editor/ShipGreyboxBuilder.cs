using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TestArenaBuilder;
using static LightingTools;

// Tools > Out of the Depths > Rebuild Ship Greybox (Main_Scene). Clears the level content in Main_Scene (the ROOMS and
// Placeholder groups and the loose doors / fish / plates at the root) and rebuilds the ship from the GDD floor plans at
// 1 map pixel = 5 cm: the nine first-floor rooms, the basement under the hatches, every door, pickup, socket, fish pack
// and the chase, wired in the GDD's lock-and-key order. Player, HUD, lighting, managers and ambience are kept.
// Safe to run again any time; everything it makes lives under SHIP. All numbers are metres (x east, z north).
//
// Route: 1 spawn (cyan) -> one-way door east into 2 the middle room (red) -> west into 3 the fish room (orange), hatch
// down to 4 the basement (the symbol key) -> back up the hatch -> through the window beside the bone key door into 5 the
// chest room (green, no door) -> 6 the stone room (blue) east of the middle room, pedestal door south into 7 the box room (yellow, the
// whole south strip) -> 8 the symbol room north of the middle room -> the rune door, 9 the hallway and the column.
public static class ShipGreyboxBuilder
{
    private const string ScenePath = "Assets/Scenes/Main_Scene.unity";
    private const string RootName = "SHIP";
    private static readonly string[] OldRoots = { RootName, "ROOMS", "Placeholder" };
    private static readonly string[] LoosePrefixes =
        { "Door", "DeadFish", "Fish_Wanderer", "FishSchool", "WallFish", "Hazard", "Pufferfish", "Trapdoor", "RespawnPlate", "ChasePufferfish", "FishWindow", "Pickup" };

    // Decks: the first floor stands on y 0 with 5 m of headroom; the basement floor is 6 m down and its ceiling is the
    // first floor's floor slab.
    private const float Ceiling = 5f;
    private const float BasementFloor = -6f;
    private const float BasementCeiling = -0.5f;
    private const float SlabT = 0.5f;
    private const float WallT = 0.5f;
    private const float DoorTop = 3.4f;     // the door frame's top; walls continue above it
    private const float DoorGap = 2.8f;     // door + frame posts
    private static readonly Vector3 Spawn = new Vector3(-26f, 1.6f, 11.25f);
    // The spawn checkpoint: north-west of the net by the west wall, behind the player's left shoulder as they start
    // (facing north-east), clear of the way to the drawer and the door.
    private static readonly Vector3 SpawnCheckpoint = new Vector3(-27.5f, 0.05f, 15.5f);

    private static PuzzleStation runeStation;   // the code lock in room 8, wired to the rune door once that exists

    // Ship extents from the map: x -29..29, z 0..58.5. The basement runs z 0..44.25 under the rooms.
    private const float HullW = -29f, HullE = 29f, HullS = 0f, HullN = 58.5f, BasementN = 44.25f;

    // The hatch through the deck: room 3 down to the basement (room 5 is reached through the Chest Window instead).
    private static readonly Rect HatchFish = new Rect(-27f, 40.25f, 2f, 2f);
    // The window you swim through from the middle room into the chest room: in the wall between them (z 38.25), just
    // east of the bone key double door, clear of the pillar in the middle room's north-east corner (x from, x to, sill
    // height, top).
    private static readonly Vector4 ChestWindow = new Vector4(3f, 4.8f, 1f, 2.8f);

    // The middle room's roof is broken open: ragged holes of different sizes (centre, radii, turn in degrees, seed for
    // the ragged edge) that show the surface and the sun and let the light and the fish in. Only the room's roof has them.
    private static readonly Rect MiddleRoof = new Rect(-12.25f, 19.75f, 19.25f, 18.5f);
    private static readonly (Vector2 centre, Vector2 radii, float turn, int seed)[] RoofBreaks =
    {
        (new Vector2(-6.5f, 25.2f), new Vector2(2.3f, 1.5f), 20f, 1),    // the big one, over the stone fragment
        (new Vector2(2.2f, 24.6f), new Vector2(1.3f, 1.1f), -10f, 2),
        (new Vector2(-2.6f, 33.9f), new Vector2(0.95f, 0.8f), 40f, 3),
        (new Vector2(3.4f, 31.2f), new Vector2(2.4f, 0.42f), -35f, 4),   // a long split in the plating
        (new Vector2(-9.2f, 33.6f), new Vector2(0.6f, 0.55f), 0f, 5),
    };
    private static readonly Color Hull = new Color(0.13f, 0.15f, 0.19f);
    private static readonly Color Prop = new Color(0.36f, 0.4f, 0.46f);
    private static readonly Color Wood = new Color(0.4f, 0.28f, 0.16f);
    private static readonly Color Stone = new Color(0.7f, 0.65f, 0.5f);
    private static readonly Color Bone = new Color(0.85f, 0.8f, 0.6f);
    private static readonly Color Rock = new Color(0.3f, 0.27f, 0.24f);
    private static readonly Color Seabed = new Color(0.05f, 0.08f, 0.1f);

    // Room colours, as on the GDD map.
    private static readonly Color Room1Cyan = new Color(0.2f, 0.55f, 0.6f);
    private static readonly Color Room2Red = new Color(0.55f, 0.2f, 0.2f);
    private static readonly Color Room3Orange = new Color(0.65f, 0.4f, 0.15f);
    private static readonly Color Room5Green = new Color(0.2f, 0.5f, 0.25f);
    private static readonly Color Room6Blue = new Color(0.2f, 0.3f, 0.6f);
    private static readonly Color Room7Yellow = new Color(0.6f, 0.55f, 0.2f);

    private static Transform ship;
    private static Door finalDoor;
    private static Door columnDoor;
    private static ItemSocket hallwayTablet;   // made by the hallway, wired to the column door by the column step

    [MenuItem("Tools/Out of the Depths/Rebuild Ship Greybox (Main_Scene)")]
    private static void Rebuild()
    {
        bool go = EditorUtility.DisplayDialog("Rebuild Ship Greybox",
            "This replaces the level in Main_Scene: the ROOMS and Placeholder groups, the loose doors, fish and plates " +
            "at the root, and any previous SHIP. Player, HUD, lighting, managers and ambience are kept.\n\n" +
            "The scene is saved when done.", "Rebuild", "Cancel");
        if (!go || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        InteractablePrefabTools.AddIndicatorsToInteractablePrefabs();
        ItemTools.EnsureItemDefinitions();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemoveOldShip(scene);
        RemoveStrayPlaceholders(scene);
        ship = new GameObject(RootName).transform;
        BeginBuild(ship, Spawn, "Ship");
        finalDoor = null;
        columnDoor = null;
        hallwayTablet = null;

        Step("Checkpoint model", CheckpointModelTools.Apply);
        Step("Decks", BuildDecks);
        Step("Roof", BuildRoof);
        Step("Hull", BuildHull);
        Step("Room 1: spawn", BuildSpawnRoom);
        Step("Room 2: middle", BuildMiddleRoom);
        Step("Room 3: fish room + hatch", BuildFishRoom);
        Step("Room 4: basement", BuildBasement);
        Step("Room 5: chest room", BuildChestRoom);
        Step("Room 6: stone room", BuildStoneRoom);
        Step("Room 7: box room", BuildBoxRoom);
        Step("Room 8: symbol room", BuildSymbolRoom);
        Step("Room 9: hallway + chase", BuildHallway);
        Step("Trident corridor", BuildColumn);
        Step("Pufferfish nests", BuildNests);
        Step("Collectibles", BuildCollectibles);
        Step("Room lights", BuildLights);
        Step("Item models", ItemModelTools.ApplyItemModelsInOpenScene);
        Step("Player", PlacePlayer);
        Step("Inventory HUD", ItemTools.EnsureInventoryHud);
        Step("Chase HUD", ChaseTools.EnsureDangerHud);
        Step("Admin panel", WireAdminPanel);
        Step("Pause menu theme", PauseMenuTools.EnsureTheme);
        Step("Pause menu pages", PauseMenuTools.EnsurePages);
        Step("Game font", FontTools.ApplyToOpenSceneQuietly);
        Step("Underwater look", UnderwaterTools.ApplyToOpenScene);
        Step("Sun and surface", BuildSunAndSurface);
        Step("Clearance check", CheckClearance);
        Step("Map bounds", SetMapBounds);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Ship greybox rebuilt and saved: {ScenePath} ({FailedSteps} step(s) failed - see errors above)".Replace(" (0 step(s) failed - see errors above)", ""));
    }

    // The old level: named groups and the loose level prefabs at the root. GAMEPLAY / HUD / LIGHTING / MANAGERS / Ambience stay.
    private static void RemoveOldShip(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            bool remove = false;
            foreach (string name in OldRoots)
                remove |= string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase);
            foreach (string prefix in LoosePrefixes)
                remove |= root.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase);
            if (remove)
                Object.DestroyImmediate(root);
        }
    }

    // ---- structure ------------------------------------------------------------------------------------------------

    // Floor slabs: the basement footprint (also the basement ceiling) with the hatch hole, the northern strip
    // under the hallway, symbol room, chest room and the top of the column, and the basement floor.
    private static void BuildDecks()
    {
        Transform decks = Group("Decks");
        SlabWithHoles("Deck", HullW, HullS, HullE, BasementN, 0f, new[] { HatchFish }, decks);
        Slab("Deck_N", HullW, BasementN, HullE, HullN, 0f, decks);
        Slab("BasementFloor", HullW, HullS, HullE, BasementN, BasementFloor, decks);
    }

    // The first-floor roof, all under one Roof object so hiding it in the editor is one click: slabs everywhere but
    // over the middle room, whose roof is the broken plating (Broken Roof).
    private static void BuildRoof()
    {
        Transform roof = Group("Roof");
        SlabWithHoles("Roof", HullW, HullS, HullE, HullN, Ceiling + SlabT, new[] { MiddleRoof }, roof);
        BrokenRoof(roof);
    }

    // The middle room's roof: one mesh of 25 cm plates with the Roof Breaks torn out of it (a ragged staircase edge),
    // solid to swim into (Mesh Collider), and a few bent plates hanging into the room round each hole. An invisible lid
    // over each hole keeps the player inside (the fish coming in through them ignore it).
    private static void BrokenRoof(Transform parent)
    {
        const float cell = 0.25f;
        int nx = Mathf.RoundToInt(MiddleRoof.width / cell), nz = Mathf.RoundToInt(MiddleRoof.height / cell);
        var solid = new bool[nx, nz];
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                solid[i, j] = !InRoofBreak(MiddleRoof.xMin + (i + 0.5f) * cell, MiddleRoof.yMin + (j + 0.5f) * cell);

        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float bottom = Ceiling, top = Ceiling + SlabT;
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int start = verts.Count;
            foreach (Vector3 v in new[] { a, b, c, d })
            {
                verts.Add(v);
                normals.Add(normal);
                uvs.Add(Mathf.Abs(normal.y) > 0.5f ? new Vector2(v.x, v.z) * 0.5f : new Vector2(v.x + v.z, v.y) * 0.5f);
            }
            tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        // Top and bottom: a quad per run of solid cells along each row.
        for (int j = 0; j < nz; j++)
        {
            int i = 0;
            while (i < nx)
            {
                if (!solid[i, j])
                {
                    i++;
                    continue;
                }
                int from = i;
                while (i < nx && solid[i, j])
                    i++;
                float x0 = MiddleRoof.xMin + from * cell, x1 = MiddleRoof.xMin + i * cell;
                float z0 = MiddleRoof.yMin + j * cell, z1 = z0 + cell;
                Quad(new Vector3(x0, top, z0), new Vector3(x0, top, z1), new Vector3(x1, top, z1), new Vector3(x1, top, z0), Vector3.up);
                Quad(new Vector3(x0, bottom, z0), new Vector3(x1, bottom, z0), new Vector3(x1, bottom, z1), new Vector3(x0, bottom, z1), Vector3.down);
            }
        }
        // The torn edges: a side face wherever a solid cell meets a hole.
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
            {
                if (!solid[i, j])
                    continue;
                float x0 = MiddleRoof.xMin + i * cell, x1 = x0 + cell;
                float z0 = MiddleRoof.yMin + j * cell, z1 = z0 + cell;
                if (i + 1 < nx && !solid[i + 1, j])
                    Quad(new Vector3(x1, bottom, z0), new Vector3(x1, top, z0), new Vector3(x1, top, z1), new Vector3(x1, bottom, z1), Vector3.right);
                if (i > 0 && !solid[i - 1, j])
                    Quad(new Vector3(x0, bottom, z1), new Vector3(x0, top, z1), new Vector3(x0, top, z0), new Vector3(x0, bottom, z0), Vector3.left);
                if (j + 1 < nz && !solid[i, j + 1])
                    Quad(new Vector3(x1, bottom, z1), new Vector3(x1, top, z1), new Vector3(x0, top, z1), new Vector3(x0, bottom, z1), Vector3.forward);
                if (j > 0 && !solid[i, j - 1])
                    Quad(new Vector3(x0, bottom, z0), new Vector3(x0, top, z0), new Vector3(x1, top, z0), new Vector3(x1, bottom, z0), Vector3.back);
            }

        var mesh = new Mesh { name = "BrokenRoof" };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        var roof = new GameObject("Roof_Broken");
        roof.transform.SetParent(parent, false);
        roof.AddComponent<MeshFilter>().sharedMesh = mesh;
        roof.AddComponent<MeshRenderer>().sharedMaterial = DefaultMaterial();
        roof.AddComponent<MeshCollider>().sharedMesh = mesh;
        roof.AddComponent<RendererTint>().Tint = Hull;

        Color bent = new Color(0.16f, 0.17f, 0.2f);
        var random = new System.Random(7);
        float Range(float a, float b) => a + (float)random.NextDouble() * (b - a);
        foreach (var hole in RoofBreaks)
        {
            // The lid: a box over the whole ragged outline, turned with it.
            var lid = new GameObject("RoofLid");
            lid.transform.SetParent(parent, false);
            lid.transform.SetPositionAndRotation(new Vector3(hole.centre.x, Ceiling + SlabT * 0.5f, hole.centre.y), Quaternion.Euler(0f, -hole.turn, 0f));
            lid.AddComponent<BoxCollider>().size = new Vector3(hole.radii.x * 2.7f, SlabT, hole.radii.y * 2.7f);

            // Bent plates hanging off the rim, more round the bigger holes.
            int plates = Mathf.Clamp(Mathf.RoundToInt((hole.radii.x + hole.radii.y) * 2f), 2, 8);
            for (int k = 0; k < plates; k++)
            {
                float angle = (k + Range(-0.3f, 0.3f)) / plates * Mathf.PI * 2f;
                Vector2 local = new Vector2(Mathf.Cos(angle) * hole.radii.x, Mathf.Sin(angle) * hole.radii.y) * 1.05f;
                float turn = hole.turn * Mathf.Deg2Rad;
                Vector2 at = hole.centre + new Vector2(local.x * Mathf.Cos(turn) - local.y * Mathf.Sin(turn), local.x * Mathf.Sin(turn) + local.y * Mathf.Cos(turn));
                Vector3 outward = new Vector3(at.x - hole.centre.x, 0f, at.y - hole.centre.y).normalized;
                float length = Range(0.5f, 1.1f);
                GameObject plate = Decor("BentPlate", Vector3.zero, new Vector3(Range(0.35f, 0.8f), 0.05f, length), bent, parent);
                // Hinged at the rim, drooping down into the room.
                Quaternion droop = Quaternion.LookRotation(-outward, Vector3.up) * Quaternion.Euler(Range(35f, 70f), Range(-15f, 15f), Range(-10f, 10f));
                plate.transform.rotation = droop;
                plate.transform.position = new Vector3(at.x, Ceiling - 0.02f, at.y) + droop * new Vector3(0f, 0f, length * 0.5f);
            }
        }
    }

    // Inside one of the Roof Breaks: an ellipse with a ragged edge.
    private static bool InRoofBreak(float x, float z)
    {
        foreach (var hole in RoofBreaks)
        {
            Vector2 d = new Vector2(x, z) - hole.centre;
            float a = -hole.turn * Mathf.Deg2Rad;
            var local = new Vector2(d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a), d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
            float r = new Vector2(local.x / hole.radii.x, local.y / hole.radii.y).magnitude;
            float angle = Mathf.Atan2(local.y, local.x);
            float edge = 1f + 0.16f * Mathf.Sin(angle * 5f + hole.seed * 1.7f) + 0.1f * Mathf.Sin(angle * 11f + hole.seed * 3.1f) + 0.06f * Mathf.Sin(angle * 23f + hole.seed);
            if (r < edge)
                return true;
        }
        return false;
    }

    // The material the primitives get (the render pipeline's default), for meshes made here.
    private static Material DefaultMaterial()
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material material = probe.GetComponent<MeshRenderer>().sharedMaterial;
        Object.DestroyImmediate(probe);
        return material;
    }

    // The outer hull, minus the pieces other rooms build with holes in them (the west hull by the hallway: the vent).
    private static void BuildHull()
    {
        Transform hull = Group("Hull");
        WallZ("Hull_W", HullW, 8.5f, 50f, hull);                 // rooms 1 and 3; the fish windows cut their own holes
        WallX("Hull_N", HullN, HullW, HullE, hull);
        WallZ("Hull_E", HullE, HullS, HullN, hull);
        WallX("Hull_S", 8.5f, HullW, 19.75f, hull);              // under rooms 1 and 7
        WallX("Hull_S_Column", HullS, 19.75f, HullE, hull);

        // Basement hull: same footprint, open plan inside.
        BasementWallZ("Cellar_W", HullW, HullS, BasementN, hull);
        BasementWallZ("Cellar_E", HullE, HullS, BasementN, hull);
        BasementWallX("Cellar_S", HullS, HullW, HullE, hull);
        BasementWallX("Cellar_N", BasementN, HullW, HullE, hull);

        // Windows: Fish Window prefabs, which cut their own sleeved holes through the hull. Only the fish room's two
        // (made with the room) have a spawner; these are just windows. Put a Fish Spawner over any of them, with the
        // windows as its children, to have fish come in there too.
        Transform windows = Group("Windows");
        GameObject windowPrefab = EnsureFishWindowPrefab();
        foreach (float z in new[] { 10.75f, 18f, 24.5f })                      // room 1, west hull (portholes 3.2 m across: kept apart)
            Window(windowPrefab, new Vector3(HullW + WallT * 0.5f, 3f, z), 90f, windows);
        foreach (float x in new[] { -20.25f, -13f, -1f, 4f, 9f, 22f })          // north hull: hallway, room 9b, column
            Window(windowPrefab, new Vector3(x, 3f, HullN - WallT * 0.5f), 180f, windows);
        foreach (float z in new[] { 50.5f, 37f, 21f, 8.75f })                   // column, east hull
            Window(windowPrefab, new Vector3(HullE - WallT * 0.5f, 3f, z), -90f, windows);
        foreach (float z in new[] { 6f, 16f, 26.3f, 36.4f })                        // basement, east hull (the cellar plan's window wall)
            Window(windowPrefab, new Vector3(HullE - WallT * 0.5f, -3.25f, z), -90f, windows);

        // Outside the hull, so the windows look onto something: a dark seabed far below and rocks around the ship.
        Transform outside = Group("Outside");
        Box("Outside_Seabed", new Vector3(0f, -9f, 30f), new Vector3(140f, 0.2f, 120f), Seabed, outside);
        foreach (var rock in new[] { (-36f, -2f, 14f, 5f), (-38f, -4f, 30f, 6f), (-34f, 0f, 46f, 4f), (-15f, -1f, 64f, 5f), (6f, -3f, 66f, 7f), (22f, 0f, 63f, 4f), (35f, -2f, 8f, 5f), (37f, -4f, 26f, 6f), (34f, 0f, 41f, 4f), (36f, -3f, 54f, 5f), (-20f, -5f, -8f, 6f), (14f, -4f, -9f, 5f) })
            Box("Outside_Rock", new Vector3(rock.Item1, rock.Item2, rock.Item3), new Vector3(rock.Item4, rock.Item4 * 1.3f, rock.Item4 * 0.8f), Seabed, outside);
    }

    // A Fish Window prefab on the room-side face of a hull wall, its arrow (yaw) pointing into the room; it cuts the hole now.
    private static GameObject Window(GameObject prefab, Vector3 position, float yaw, Transform parent)
    {
        var window = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        window.transform.SetParent(parent, true);
        window.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        window.name = "Window";
        window.GetComponent<FishWindow>().CutHole(false);
        return window;
    }

    // ---- rooms ----------------------------------------------------------------------------------------------------

    // Room 1 (cyan): the spawn room (x -29..-12.25, z 8.5..28.75) with the net, the key in a drawer in the south-east
    // corner, and its only way out: the one-way door east into the middle room.
    private static void BuildSpawnRoom()
    {
        Transform room = Group("Room_1_Spawn");
        Deck(room, HullW, 8.5f, -12.25f, 28.75f, 0f, Room1Cyan);
        WallX("R1_N", 28.75f, HullW, -12.25f, room);
        // East wall, up to the top of the middle room: the one-way exit (z 24..26.8) and, further north, the door from
        // the middle room into the fish room (z 30..32.8). Below z 19.75 it is solid: room 7 is on the other side.
        WallZ("R1_E", -12.25f, 8.5f, 38.25f, room, 24f, 24f + DoorGap, 30f, 30f + DoorGap);

        // The net you start in (button-mash escape later): a cage of posts and rails round the spawn, wide enough to
        // float in, nothing at eye height, no colliders.
        Transform net = Group("Net");
        net.SetParent(room, false);
        Color rope = new Color(0.35f, 0.3f, 0.2f);
        const int posts = 8;
        const float radius = 2.1f;
        for (int i = 0; i < posts; i++)
        {
            float a = i * Mathf.PI * 2f / posts;
            float b = (i + 1) * Mathf.PI * 2f / posts;
            var p = new Vector3(Spawn.x + Mathf.Cos(a) * radius, 1.6f, Spawn.z + Mathf.Sin(a) * radius);
            var q = new Vector3(Spawn.x + Mathf.Cos(b) * radius, 1.6f, Spawn.z + Mathf.Sin(b) * radius);
            Decor("NetPost", p, new Vector3(0.06f, 3.2f, 0.06f), rope, net);
            foreach (float y in new[] { 0.3f, 3.1f })   // rails at the ankles and above the head
            {
                Vector3 from = new Vector3(p.x, y, p.z), to = new Vector3(q.x, y, q.z);
                GameObject rail = Decor("NetRail", (from + to) * 0.5f, new Vector3(0.05f, 0.05f, Vector3.Distance(from, to)), rope, net);
                rail.transform.rotation = Quaternion.LookRotation(to - from);
            }
        }

        // The key, in one of the six drawers of the VetoLaatikko dresser by the south wall (Vili's model), a different
        // drawer each run: E opens any drawer, and the key rides out with its drawer. The placeholder cabinet stands in
        // if the dresser prefab is missing.
        GameObject key = CreatePickup(new Vector3(-13.4f, 0.75f, 10.3f), "Item_FirstRoomKey", room);
        GameObject drawerPearl = CreatePickup(new Vector3(-13.4f, 0.75f, 10.3f), "Item_Pearl", room);   // in another drawer
        if (!Dresser("Dresser_Key", new Vector3(-15.14f, 0f, 10.58f), 0f, key, room, drawerPearl))
        {
            Object.DestroyImmediate(drawerPearl);
            key.SetActive(false);
            Door drawer = Drawer("Drawer_Key", new Vector3(-13.2f, 0f, 10.3f), Vector3.left, room);
            UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(drawer.onOpened, key.SetActive, true);
        }

        // The one-way door: unlocked by the key (lock box beside it), shuts and locks behind you.
        Door oneWay = DoorZ("Door_Room1_Exit", -12.25f, 24f, true, true, room);
        SetField(oneWay, "lockedHint", p => p.stringValue = "Locked. The lock box beside it takes the key hidden in this room.");
        GameObject lockBox = Box("Lock_FirstRoomKey", new Vector3(-12.6f, 1.3f, 23.4f), new Vector3(0.25f, 0.5f, 0.5f), new Color(0.8f, 0.7f, 0.3f), room);
        OpenOnFilled(Socket(lockBox, "Item_FirstRoomKey", 1, true, "unlock with", null, null), oneWay);

        CreateSchool("Fish_Wanderer", 3, new Vector3(-22f, 2.5f, 18f), room);
        Spawn("DeadFish", new Vector3(-16f, 1f, 14f), room, 30f);
        Spawn("DeadFish", new Vector3(-25f, 1f, 21f), room, -60f);
        CreatePickup(new Vector3(-20f, 0.8f, 26f), "Item_Pearl", room);
    }

    // Room 2 (red): the hub (x -12.25..7, z 19.75..38.25) with pillars in the corners, symbol 2 on the floor, a stone
    // fragment, the seaweed that ties bone fragments into the bone key, and doors west (3), east (6) and north (8).
    private static void BuildMiddleRoom()
    {
        Transform room = Group("Room_2_Middle");
        Deck(room, -12.25f, 19.75f, 7f, 38.25f, 0f, Room2Red);
        WallX("R2_S", 19.75f, -12.25f, 7f, room);                                   // room 7 below, no door
        WallXBig("R2_N", 38.25f, -12.25f, 19.75f, room, -5.05f, -5.05f + DoubleDoorGap, ChestWindow.x, ChestWindow.y);   // rooms 3, 8, 5 above: the symbol room's big double door, and the window into the chest room
        ChestRoomWindow(room);
        DoubleDoor toSymbol = SpawnDoubleDoor("Door_SymbolRoom", new Vector3(-2.25f, 0f, 38.25f), 0f, true, room);
        SetField(toSymbol, "lockedHint", p => p.stringValue = "Locked. The bone key goes in the lock plate: tie the three bone fragments together at the seaweed.");
        WallZ("R2_E", 7f, 19.75f, 38.25f, room, 29f, 29f + DoorGap);               // room 6 east (and the stub down to its south wall)
        DoorZ("Door_StoneRoom", 7f, 29f, false, false, room);
        DoorZ("Door_FishRoom", -12.25f, 30f, false, false, room);                  // in room 1's east wall, north of room 1

        foreach (Vector3 corner in new[] { new Vector3(-11f, 0f, 21f), new Vector3(5.75f, 0f, 21f), new Vector3(-11f, 0f, 37f), new Vector3(5.75f, 0f, 37f) })
            Box("Pillar", corner + Vector3.up * (Ceiling * 0.5f), new Vector3(1.5f, Ceiling, 1.5f), Prop, room);
        // On the floor, above the deck grid (which is 3 cm up), so the two never fight.
        Decor("Symbol2_Floor", new Vector3(-2.5f, 0.055f, 29.25f), new Vector3(2.4f, 0.02f, 2.4f), new Color(0.15f, 0.2f, 0.9f), room);
        CreatePickup(new Vector3(-6.75f, 0.8f, 22f), "Item_StoneFragment", room).name = "Pickup_StoneFragment_Pedestal_MiddleRoom";   // one of the three for the pedestal

        // The bone key goes into the lock plate in the middle of the double door.
        OpenOnFilled(Socket(toSymbol.LockPlate, "Item_BoneKey", 1, true, "unlock with", null, null), toSymbol);

        SeaweedSocket(SeaweedSpot, room);   // clear of the pillar in the north-east corner

        // Fish drop in through the holes in the roof when you come in (the bigger holes are the spawner's openings,
        // arrows pointing down), join one pack and wander the room. While you are away they pause where they are.
        FishSchool school = CreateSchool("Fish_Wanderer", 0, new Vector3(-2.6f, 2.5f, 29f), room);
        var spawner = new GameObject("FishSpawner_RoofHoles");
        spawner.transform.SetParent(room, false);
        spawner.transform.position = new Vector3(-2.6f, 2.5f, 29f);
        for (int h = 0; h < RoofBreaks.Length; h++)
        {
            var hole = RoofBreaks[h];
            if (Mathf.Min(hole.radii.x, hole.radii.y) < 0.7f)
                continue;   // too narrow for a fish
            var opening = new GameObject("RoofHole_" + (char)('A' + h));
            opening.transform.SetParent(spawner.transform, false);
            opening.transform.SetPositionAndRotation(new Vector3(hole.centre.x, Ceiling, hole.centre.y), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            var window = opening.AddComponent<FishWindow>();
            SetField(window, "cutHoleAtStart", p => p.boolValue = false);
            SetField(window, "holeSize", p => p.vector2Value = hole.radii * 2f);
            SetField(window, "startDepth", p => p.floatValue = 1.5f);
            SetField(window, "startDrop", p => p.floatValue = 5f);
            SetField(window, "exitDistance", p => p.floatValue = 2.5f);
            SetField(window, "openingScatter", p => p.vector2Value = hole.radii * 0.4f);
            SetField(window, "exitScatter", p => p.vector2Value = new Vector2(40f, 35f));
        }
        var area = spawner.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.center = new Vector3(MiddleRoof.center.x, Ceiling * 0.5f, MiddleRoof.center.y) - spawner.transform.position;
        area.size = new Vector3(MiddleRoof.width - 0.5f, Ceiling, MiddleRoof.height - 0.5f);
        var fish = spawner.AddComponent<FishSpawner>();
        SetField(fish, "fishPrefab", p => p.objectReferenceValue = FindPrefab("Fish_Wanderer"));
        SetField(fish, "count", p => p.intValue = 5);
        SetField(fish, "joinSchool", p => p.objectReferenceValue = school);
        SetField(fish, "startMode", p => p.enumValueIndex = (int)FishSpawner.StartMode.PlayerEntersTrigger);
        SetField(fish, "stagger", p => p.floatValue = 1.2f);

        // Sunlit debris under the big hole: bits of the roof on the floor.
        Color bent = new Color(0.16f, 0.17f, 0.2f);
        GameObject slab = Box("FallenPlate", new Vector3(-8.2f, 0.12f, 26.6f), new Vector3(1.4f, 0.1f, 0.9f), bent, room);
        slab.transform.rotation = Quaternion.Euler(4f, 25f, 8f);
        slab = Box("FallenPlate", new Vector3(-4.6f, 0.1f, 23.9f), new Vector3(0.8f, 0.08f, 0.6f), bent, room);
        slab.transform.rotation = Quaternion.Euler(-6f, -40f, 3f);
    }

    private static readonly Vector3 SeaweedSpot = new Vector3(3.2f, 0f, 35.2f);

    // Room 3 (orange): the fish room, L-shaped (x -29..-12.25 from z 28.75, x -29..-7 from z 38.25, up to the mural
    // wall at z 44), entered from the middle room. The dagger, the wall of fish over the hatch, the pack coming in
    // through two hull windows, and the hatch down to the basement. Above the mural wall is the symbol 3 nook,
    // reached from the hallway.
    private static void BuildFishRoom()
    {
        Transform room = Group("Room_3_FishRoom");
        Deck(room, HullW, 28.75f, -12.25f, 38.25f, 0f, Room3Orange);
        Deck(room, HullW, 38.25f, -7f, 44f, 0f, Room3Orange, HatchFish);
        Deck(room, HullW, 44f, -7f, 50f, 0f, new Color(0.22f, 0.3f, 0.42f));
        WallZ("R3_E", -7f, 38.25f, 50f, room);   // shared with the symbol room; the z 50 line above is built by the hallway

        // The nook under the hallway (z 44..50): walled off from the fish room by the mural wall, entered from the
        // hallway, with symbol 3 and the checkpoint the map marks there.
        WallX("Nook_S", 44f, HullW, -7f, room);
        MuralAt(new Vector3(-18f, 2.4f, 44.3f), new Vector2(20f, 1.6f), Vector3.forward, room);
        MuralAt(new Vector3(-18f, 2.4f, 43.7f), new Vector2(20f, 1.6f), Vector3.back, room);
        Decor("Symbol3_Wall", new Vector3(HullW + 0.3f, 2.6f, 46.5f), new Vector3(0.06f, 2f, 2f), new Color(0.2f, 0.85f, 0.35f), room);
        GameObject nookPlate = Spawn("RespawnPlate", new Vector3(-12f, 0.05f, 48.8f), room);   // along the north wall, clear of the way in from the door
        if (nookPlate != null)
            nookPlate.name = "Checkpoint_Symbol3";

        // The dagger, a few steps from the hatch, just outside the wall of fish it is for.
        CreatePickup(new Vector3(-23.3f, 0.9f, 39.4f), "Item_Dagger", room);

        // The hatch: the 2 x 2 hole in the deck, covered by the trapdoor; symbol 1 is painted on the basement floor right under it (nothing sits in the hole itself).
        SpawnTrapdoor("Hatch_Basement", new Vector3(HatchFish.xMin, 0f, HatchFish.yMin), room);
        Decor("Symbol1_UnderHatch", new Vector3(HatchFish.center.x, BasementFloor + 0.06f, HatchFish.center.y), new Vector3(1.6f, 0.04f, 1.6f), new Color(0.9f, 0.15f, 0.1f), room);

        // The wall of fish over the hatch: hack through it with the dagger.
        for (int i = 0; i < 7; i++)
        {
            float a = i * Mathf.PI * 2f / 7f;
            Spawn("WallFish", new Vector3(HatchFish.center.x + Mathf.Cos(a) * 1.7f, 1.2f + (i % 2) * 0.8f, HatchFish.center.y + Mathf.Sin(a) * 1.7f), room, a * Mathf.Rad2Deg + 90f);
        }

        // The pack from the windows: two Fish Windows in the west hull; they cut their own holes.
        FishSchool school = CreateSchool("Fish_Wanderer", 0, new Vector3(-18f, 2.5f, 36f), room);
        var spawner = new GameObject("FishSpawner_Windows");
        spawner.transform.SetParent(room, false);
        spawner.transform.position = new Vector3(-18f, 2.5f, 39f);
        GameObject windowPrefab = EnsureFishWindowPrefab();
        foreach (float z in new[] { 33.5f, 38f })
        {
            var window = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab, room.gameObject.scene);
            window.transform.SetParent(spawner.transform, true);
            window.transform.SetPositionAndRotation(new Vector3(HullW + WallT * 0.5f, 3f, z), Quaternion.Euler(0f, 90f, 0f));
            window.name = "FishWindow_" + z;
            window.GetComponent<FishWindow>().CutHole(false);
        }
        var area = spawner.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.center = new Vector3(-18f, 2.5f, 36.4f) - spawner.transform.position;
        area.size = new Vector3(22f, Ceiling, 15.25f);
        var component = spawner.AddComponent<FishSpawner>();
        var so = new SerializedObject(component);
        so.FindProperty("fishPrefab").objectReferenceValue = FindPrefab("Fish_Wanderer");
        so.FindProperty("count").intValue = 6;
        so.FindProperty("joinSchool").objectReferenceValue = school;
        so.FindProperty("startMode").enumValueIndex = (int)FishSpawner.StartMode.PlayerEntersTrigger;
        so.ApplyModifiedPropertiesWithoutUndo();

        Spawn("Pufferfish", new Vector3(-14f, 2.5f, 41f), room, 200f);
        Spawn("DeadFish", new Vector3(-20f, 1f, 42f), room, 40f);
    }

    // Room 4: the cellar, one open hold under everything (x -29..29, z 0..44.25, floor at -6, ceiling at -0.5), laid
    // out as the concept art draws it. The plan is drawn with south at the top: the window wall is the east hull (four
    // portholes), the murals are on the south and west walls, the hatch you come down through is in the north-west
    // corner (symbol 1 under it) and the north wall is blank. The floor is sand and sea grass in bands running corner
    // to corner, with a sand hill in the south-east corner and a ring-shaped mound in the middle; thick tall giant
    // kelp stands in forests (along the south wall, down the west side, and a big one across the north half, clear of
    // the hatches); whale skeletons lie along the south wall and across the middle, a small one by the symbol key. All
    // walls and the ceiling are the same stone. Also: the stone fragment, the bone fragment, pearls, the current along
    // the south wall to ride, fish, and the hatch up into room 5.
    private static void BuildBasement()
    {
        Transform room = Group("Room_4_Basement");
        CellarFloor(room);
        MuralAt(new Vector3(0f, -3.3f, HullS + 0.3f), new Vector2(40f, 1.6f), Vector3.forward, room);    // south wall
        MuralAt(new Vector3(HullW + 0.3f, -3.3f, 22f), new Vector2(34f, 1.6f), Vector3.right, room);      // west wall

        // The kelp forests: clumps of giant kelp on a loose grid through each patch, reaching up to the ceiling,
        // kept clear of the hatches, the checkpoint and the things to pick up.
        Transform kelp = Group("KelpForest");
        kelp.SetParent(room, false);
        var random = new System.Random(41);
        int seedNo = 200;
        foreach (Rect patch in KelpPatches)
        {
            const float step = 3f;   // clumps about 3 m apart: a thick forest
            for (float x = patch.xMin + step * 0.5f; x < patch.xMax; x += step)
                for (float z = patch.yMin + step * 0.5f; z < patch.yMax; z += step)
                {
                    // Anywhere in its grid cell (so no rows show), and now and then none at all (so it thins out).
                    float px = x + ((float)random.NextDouble() - 0.5f) * step;
                    float pz = z + ((float)random.NextDouble() - 0.5f) * step;
                    if (random.NextDouble() < 0.15 || !patch.Contains(new Vector2(px, pz)) || !KelpMayGrow(px, pz))
                        continue;
                    Vector3 foot = CellarGround(px, pz);
                    Seaweed weed = SeaweedAt(foot, Seaweed.Kind.GiantKelp, seedNo++, kelp);
                    float room01 = BasementCeiling - foot.y - 0.35f;   // up to just under the ceiling
                    SetField(weed, "height", p => p.floatValue = Mathf.Min(5f, room01));
                    weed.Build();
                }
        }

        // The sea-grass meadow: the green band from the south wall (between the big skeleton and the key) down to the
        // stone fragment on the east side, round the ring mound, carpeted with low tufts (Meadow seaweed) on a loose
        // grid, thinner in the mound's pale middle, not under the kelp or on the things to pick up.
        Transform meadow = Group("SeaGrass");
        meadow.SetParent(room, false);
        var grassRandom = new System.Random(77);
        int grassSeed = 600;
        const float grassStep = 1.7f;
        for (float x = HullW + grassStep * 0.5f; x < HullE; x += grassStep)
            for (float z = HullS + grassStep * 0.5f; z < BasementN; z += grassStep)
            {
                float px = x + ((float)grassRandom.NextDouble() - 0.5f) * grassStep;
                float pz = z + ((float)grassRandom.NextDouble() - 0.5f) * grassStep;
                float weight = MeadowWeight(px, pz);
                float ring = new Vector2((px - 3f) / 6.2f, (pz - 12f) / 4.8f).magnitude;
                if (ring < 0.8f)
                    weight *= 0.4f;   // the mound's pale middle: only a few
                if (grassRandom.NextDouble() > weight || InKelp(px, pz) || !GrassMayGrow(px, pz))
                    continue;
                SeaweedAt(CellarGround(px, pz), Seaweed.Kind.Meadow, grassSeed++, meadow);
            }

        // Whale skeletons: a big one along the south wall (the bone fragment by its east end), one across the middle (the stone fragment by its east end), a small one by the symbol key.
        Carcass(CellarGround(2.6f, 3.8f), 0f, room, 1.3f, 13f);     // 17 m, x -6..11
        Carcass(CellarGround(2.9f, 24.6f), 0f, room, 1.2f, 15f);    // 18 m, x -6..12
        Carcass(CellarGround(-24.6f, 5f), 90f, room, 0.9f, 7f);     // 6 m, beside the symbol key

        CreatePickup(CellarGround(-19f, 5.5f) + Vector3.up * 0.8f, "Item_SymbolKey", room);
        CreatePickup(CellarGround(19f, 24.5f) + Vector3.up * 0.8f, "Item_StoneFragment", room).name = "Pickup_StoneFragment_Pedestal_Basement";     // past the middle skeleton's east end
        CreatePickup(CellarGround(14f, 5.6f) + Vector3.up * 0.8f, "Item_BoneKeyFragment", room);    // past the big skeleton's east end
        CreatePickup(CellarGround(10f, 15f) + Vector3.up * 0.8f, "Item_Pearl", room);
        CreatePickup(CellarGround(-24f, 30f) + Vector3.up * 0.8f, "Item_Pearl", room);
        CreatePickup(CellarGround(22f, 38f) + Vector3.up * 0.8f, "Item_Shell", room);   // hidden in the big kelp forest

        // Schools of fish where the concept paints them: by the sand hill, over the east side, over the west kelp,
        // and over the big forest.
        CreateSchool("Fish_Wanderer", 3, new Vector3(20f, BasementFloor + 3.5f, 7f), room);
        CreateSchool("Fish_Wanderer", 4, new Vector3(16f, BasementFloor + 3f, 17f), room);
        CreateSchool("Fish_Wanderer", 3, new Vector3(-16f, BasementFloor + 3.5f, 19f), room);
        CreateSchool("Fish_Wanderer", 4, new Vector3(-8f, BasementFloor + 3.5f, 34f), room);
        Spawn("DeadFish", CellarGround(0f, 30f) + Vector3.up, room, 10f);
        Spawn("DeadFish", CellarGround(-20f, 15f) + Vector3.up, room, -80f);
        GameObject plate = Spawn("RespawnPlate", CellarGround(-22.5f, 43.2f) + Vector3.up * 0.03f, room);   // by the north wall, beside where the hatch drops you, not under it
        if (plate != null)
            plate.name = "Checkpoint_Basement";

        StoneCellar();
    }

    // Where the kelp forests grow (x, z, width, depth): along the south wall between the big skeleton and the symbol
    // key, down the west side, a strip on the east side, and the big forest across the north half.
    private static readonly Rect[] KelpPatches =
    {
        new Rect(-14.5f, 1f, 8.5f, 8f),
        new Rect(-25f, 11f, 10.5f, 10f),
        new Rect(15.5f, 19.5f, 11f, 4f),
        new Rect(-2f, 26f, 28f, 16.5f),
    };

    // Kept clear of kelp: the two hatches, the checkpoint, the things to pick up (x, z, radius).
    private static readonly Vector3[] KelpClear =
    {
        new Vector3(-26f, 41.25f, 4.5f), new Vector3(8f, 41f, 4.5f), new Vector3(-22.5f, 43.2f, 3f),
        new Vector3(-19f, 5.5f, 2.5f), new Vector3(19f, 24.5f, 2.5f), new Vector3(14f, 5.6f, 2.5f),
        new Vector3(10f, 15f, 1.5f), new Vector3(-24f, 30f, 1.5f), new Vector3(0f, 30f, 1.5f),
        new Vector3(-8f, 24f, 2.2f), new Vector3(12f, 34f, 2.2f),   // the two pufferfish nests
    };

    private static bool KelpMayGrow(float x, float z)
    {
        foreach (Vector3 clear in KelpClear)
            if (new Vector2(x - clear.x, z - clear.y).magnitude < clear.z)
                return false;
        return x < 26.5f;   // leave the window wall's light clear
    }

    // Level ground round the hatches and the checkpoint (x, z, radius flat, radius where it is back to normal).
    private static readonly Vector4[] CellarLevel =
    {
        new Vector4(-26f, 41.25f, 3f, 6f), new Vector4(8f, 41f, 3f, 6f), new Vector4(-22.5f, 43.2f, 2f, 4f),
    };

    // The cellar floor's height above the flat floor at (x, z), in metres: a sand hill in the south-east corner, a
    // ring-shaped mound in the middle, low dunes in bands running corner to corner, a little unevenness, and level
    // round the hatches.
    private static float CellarHeight(float x, float z)
    {
        float hill = Vector2.Distance(new Vector2(x, z), new Vector2(27f, 1.5f));
        float h = 2.2f * Smooth01(1f - hill / 11f);
        float ring = new Vector2((x - 3f) / 6.2f, (z - 12f) / 4.8f).magnitude;
        h += 0.75f * Mathf.Exp(-Mathf.Pow((ring - 1f) / 0.35f, 2f)) + 0.2f * Smooth01(1f - ring);
        h += 0.22f * (Mathf.Sin(CellarBand(x, z) * 0.7f) * 0.5f + 0.5f);
        h += 0.14f * (Mathf.PerlinNoise(x * 0.18f + 11f, z * 0.18f + 7f) - 0.5f);
        foreach (Vector4 level in CellarLevel)
        {
            float d = new Vector2(x - level.x, z - level.y).magnitude;
            h *= Smooth01((d - level.z) / (level.w - level.z));
        }
        return Mathf.Max(0f, h);
    }

    // The sea-grass meadow on the plan (the green band round the ring mound), as a polygon of (x, z) points.
    private static readonly Vector2[] Meadow =
    {
        new Vector2(-5.2f, 0f), new Vector2(-29f, 1.3f), new Vector2(-29f, 11.1f), new Vector2(-14.5f, 16.8f),
        new Vector2(-5.2f, 20.4f), new Vector2(4f, 25.8f), new Vector2(12.2f, 26f), new Vector2(29f, 26f),
        new Vector2(29f, 15.5f), new Vector2(19.1f, 11.1f), new Vector2(9.9f, 5.3f), new Vector2(2.3f, 1.3f),
    };

    // 1 well inside the meadow, fading to 0 over its last 2.5 m and outside it.
    private static float MeadowWeight(float x, float z)
    {
        var p = new Vector2(x, z);
        bool inside = false;
        float edge = float.MaxValue;
        for (int i = 0, j = Meadow.Length - 1; i < Meadow.Length; j = i++)
        {
            Vector2 a = Meadow[i], b = Meadow[j];
            if ((a.y > z) != (b.y > z) && x < (b.x - a.x) * (z - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            edge = Mathf.Min(edge, Vector2.Distance(p, a + ab * t));
        }
        return inside ? Smooth01(edge / 2.5f) : 0f;
    }

    private static bool InKelp(float x, float z)
    {
        foreach (Rect patch in KelpPatches)
            if (patch.Contains(new Vector2(x, z)))
                return true;
        return false;
    }

    // Kept clear of grass: the things to pick up and the checkpoint (the kelp's own clear list), a little tighter.
    private static bool GrassMayGrow(float x, float z)
    {
        foreach (Vector3 clear in KelpClear)
            if (new Vector2(x - clear.x, z - clear.y).magnitude < Mathf.Min(clear.z, 1.6f))
                return false;
        return true;
    }

    // Across the plan's bands, in metres: they curve round the sand hill in the south-east corner like contour lines.
    private static float CellarBand(float x, float z) => Vector2.Distance(new Vector2(x, z), new Vector2(HullE, HullS));

    // The point on the cellar floor at (x, z).
    private static Vector3 CellarGround(float x, float z) => new Vector3(x, BasementFloor + 0.02f + CellarHeight(x, z), z);

    private static float Smooth01(float t) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

    // The cellar floor: a 1 m grid shaped by Cellar Height, painted with CellarFloor.png (sand and sea grass bands,
    // the pale middle of the ring mound, dark ground under the kelp), solid to swim on (Mesh Collider).
    private static void CellarFloor(Transform parent)
    {
        int nx = 59, nz = 45;
        var verts = new Vector3[nx * nz];
        var uvs = new Vector2[nx * nz];
        for (int j = 0; j < nz; j++)
            for (int i = 0; i < nx; i++)
            {
                float x = HullW + (HullE - HullW) * i / (nx - 1f);
                float z = HullS + (BasementN - HullS) * j / (nz - 1f);
                verts[j * nx + i] = CellarGround(x, z);
                uvs[j * nx + i] = new Vector2(i / (nx - 1f), j / (nz - 1f));
            }
        var tris = new int[(nx - 1) * (nz - 1) * 6];
        int t = 0;
        for (int j = 0; j < nz - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }
        var mesh = new Mesh { name = "CellarFloor" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var floor = new GameObject("CellarFloor");
        floor.transform.SetParent(parent, false);
        floor.AddComponent<MeshFilter>().sharedMesh = mesh;
        floor.AddComponent<MeshRenderer>().sharedMaterial = CellarMaterial(CellarFloorMaterialPath, CellarFloorTexturePath, PaintCellarFloor, 0.08f, true);
        floor.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private const string CellarFloorTexturePath = "Assets/Art/Textures/CellarFloor.png";
    private const string CellarFloorMaterialPath = "Assets/Art/Materials/CellarFloor.mat";
    private const string StoneTexturePath = "Assets/Art/Textures/CellarStone.png";
    private const string StoneMaterialPath = "Assets/Art/Materials/CellarStone.mat";
    private const float StoneTile = 2.5f;   // metres of wall one tile of the stone covers

    // All the cellar's walls and its ceiling (the underside of the deck) in the same stone: each box gets a copy of
    // the cube mesh with its texture tiled by its size (Stone Tile metres a tile), so big walls are not one stretched
    // picture. The cut pieces round the portholes are included.
    private static void StoneCellar()
    {
        Material stone = CellarMaterial(StoneMaterialPath, StoneTexturePath, PaintStone, 0.18f);
        foreach (MeshRenderer renderer in ship.GetComponentsInChildren<MeshRenderer>(true))
        {
            GameObject go = renderer.gameObject;
            bool cellarWall = go.name.StartsWith("Cellar_");
            bool ceiling = go.name == "Deck" && go.GetComponent<GridFloor>() == null;
            if (!cellarWall && !ceiling)
                continue;
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null)
                continue;
            filter.sharedMesh = TiledCube(go.transform.lossyScale, StoneTile);
            renderer.sharedMaterial = stone;
            var tint = go.GetComponent<RendererTint>();
            if (tint != null)
                tint.Tint = Color.white;
        }
    }

    // A unit cube (24 vertices, like Unity's, so the wall cutter still takes it) whose UVs are tiled by the box's size.
    private static Mesh TiledCube(Vector3 size, float tile)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 n in axes)
        {
            Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.Cross(Vector3.up, n);
            Vector3 v = Vector3.Cross(n, u);
            float su = Mathf.Abs(Vector3.Dot(u, size)) / tile, sv = Mathf.Abs(Vector3.Dot(v, size)) / tile;
            int start = verts.Count;
            foreach (var (a, b) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
            {
                verts.Add((n + u * a + v * b) * 0.5f);
                normals.Add(n);
                uvs.Add(new Vector2((a + 1f) * 0.5f * su, (b + 1f) * 0.5f * sv));
            }
            tris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }
        var mesh = new Mesh { name = "StoneBox" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    // A URP Lit material with a painted texture, both saved once (paint = fills the texture's pixels).
    private static Material CellarMaterial(string materialPath, string texturePath, System.Func<Texture2D> paint, float smoothness, bool repaint = false)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (material != null && texture != null && !repaint)
            return material;
        if (texture == null || repaint)
        {
            Texture2D painted = paint();
            System.IO.File.WriteAllBytes(texturePath, painted.EncodeToPNG());
            Object.DestroyImmediate(painted);
            AssetDatabase.ImportAsset(texturePath);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }
        if (material != null)
        {
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            return material;
        }
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = System.IO.Path.GetFileNameWithoutExtension(materialPath) };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    // The cellar floor's paint, over the whole floor (x -29..29 across, z 0..44.25 up): sand and sea grass in wavy
    // bands running corner to corner, sand up the hill in the south-east corner, the ring mound's pale middle and
    // darker rim, darker ground under the kelp forests, plain sand round the hatches, and a fine grain over it all.
    private static Texture2D PaintCellarFloor()
    {
        const int w = 1024, h = 782;
        var texture = new Texture2D(w, h, TextureFormat.RGB24, true);
        var pixels = new Color[w * h];
        var sand = new Color(0.64f, 0.58f, 0.38f);
        var paleSand = new Color(0.76f, 0.72f, 0.5f);
        var grass = new Color(0.2f, 0.37f, 0.16f);
        var darkGrass = new Color(0.1f, 0.22f, 0.1f);
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                float x = HullW + (HullE - HullW) * px / (w - 1f);
                float z = HullS + (BasementN - HullS) * py / (h - 1f);
                float wobble = (Mathf.PerlinNoise(x * 0.12f + 3f, z * 0.12f + 9f) - 0.5f) * 3f;
                float band = Mathf.Sin((CellarBand(x, z) + wobble) * Mathf.PI * 2f / 9f);
                Color c = Color.Lerp(grass, sand, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.25f, 0.25f, band)));

                float hill = Vector2.Distance(new Vector2(x, z), new Vector2(27f, 1.5f));
                c = Color.Lerp(c, sand, Smooth01(1.3f - hill / 9f));
                // The meadow: one green band, lighter and darker in patches like grass on the hills.
                float lush = Mathf.PerlinNoise(x * 0.25f + 40f, z * 0.25f + 13f);
                Color meadowGreen = Color.Lerp(new Color(0.16f, 0.32f, 0.13f), new Color(0.3f, 0.48f, 0.2f), lush);
                c = Color.Lerp(c, meadowGreen, MeadowWeight(x, z));
                float ring = new Vector2((x - 3f) / 6.2f, (z - 12f) / 4.8f).magnitude;
                c = Color.Lerp(c, darkGrass, 0.8f * Mathf.Exp(-Mathf.Pow((ring - 1f) / 0.3f, 2f)));
                c = Color.Lerp(c, paleSand, Smooth01((0.85f - ring) / 0.25f) * 0.8f);

                float under = 0f;
                foreach (Rect patch in KelpPatches)
                {
                    float dx = Mathf.Max(patch.xMin - x, 0f, x - patch.xMax);
                    float dz = Mathf.Max(patch.yMin - z, 0f, z - patch.yMax);
                    under = Mathf.Max(under, Smooth01(1f - new Vector2(dx, dz).magnitude / 2.5f));
                }
                float clumps = Mathf.PerlinNoise(x * 0.35f + 1f, z * 0.35f + 5f);
                c = Color.Lerp(c, darkGrass, under * Mathf.Lerp(0.45f, 0.9f, clumps));
                foreach (Vector4 level in CellarLevel)
                    c = Color.Lerp(c, sand, Smooth01(1f - new Vector2(x - level.x, z - level.y).magnitude / level.w) * 0.7f);

                float grain = 0.85f + 0.3f * Mathf.PerlinNoise(x * 3.1f + 17f, z * 3.1f + 23f);
                float speck = Mathf.PerlinNoise(x * 9f, z * 9f) > 0.72f ? 0.85f : 1f;
                pixels[py * w + px] = c * grain * speck;
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // The stone: a tiling wall of rounded blocks (a jittered grid of cells, 6 across a tile), each its own shade of
    // grey-brown, darker toward its edges, with dark mortar between and a fine grain.
    private static Texture2D PaintStone()
    {
        const int size = 512, cells = 6;
        var random = new System.Random(9);
        var centres = new Vector2[cells, cells];
        var shades = new Color[cells, cells];
        for (int i = 0; i < cells; i++)
            for (int j = 0; j < cells; j++)
            {
                centres[i, j] = new Vector2(i + 0.2f + 0.6f * (float)random.NextDouble(), j + 0.2f + 0.6f * (float)random.NextDouble());
                float k = (float)random.NextDouble();
                shades[i, j] = Color.Lerp(new Color(0.34f, 0.35f, 0.37f), new Color(0.44f, 0.4f, 0.33f), k) * Mathf.Lerp(0.85f, 1.1f, (float)random.NextDouble());
            }
        var mortar = new Color(0.1f, 0.1f, 0.11f);
        var texture = new Texture2D(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[size * size];
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                var p = new Vector2(px / (float)size * cells, py / (float)size * cells);
                float d1 = float.MaxValue, d2 = float.MaxValue;
                Color shade = Color.gray;
                int ci = Mathf.FloorToInt(p.x), cj = Mathf.FloorToInt(p.y);
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        int i = ci + di, j = cj + dj;
                        int wi = ((i % cells) + cells) % cells, wj = ((j % cells) + cells) % cells;
                        Vector2 c = centres[wi, wj] + new Vector2(i - wi, j - wj);
                        float d = Vector2.Distance(p, c);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                            shade = shades[wi, wj];
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.09f, d2 - d1));
                float grain = 0.88f + 0.24f * Tileable(px, py, size, 64, 3) ;
                Color stone = shade * Mathf.Lerp(1.05f, 0.8f, Mathf.Clamp01(d1 * 1.1f)) * grain;
                pixels[py * size + px] = Color.Lerp(mortar, stone, edge);
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    // Tiling value noise 0..1 over a size x size picture, `cells` lattice cells across, from `seed`.
    private static float Tileable(int px, int py, int size, int cells, int seed)
    {
        float fx = px / (float)size * cells, fy = py / (float)size * cells;
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
        float Hash(int x, int y)
        {
            x = ((x % cells) + cells) % cells;
            y = ((y % cells) + cells) % cells;
            unchecked
            {
                int n = x * 374761393 + y * 668265263 + seed * 144269504;
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }
        float a = Mathf.Lerp(Hash(x0, y0), Hash(x0 + 1, y0), tx);
        float b = Mathf.Lerp(Hash(x0, y0 + 1), Hash(x0 + 1, y0 + 1), tx);
        return Mathf.Lerp(a, b, ty);
    }

    // Room 5 (green): the chest room (x 2.5..19.75, z 38.25..50). No doors: the way in and out is the broken window
    // from the middle room, beside the bone key door. The chest opens with the symbol key and holds a stone fragment and a bone fragment.
    private static void BuildChestRoom()
    {
        Transform room = Group("Room_5_ChestRoom");
        Deck(room, 2.5f, 38.25f, 19.75f, 50f, 0f, Room5Green);
        WallZ("R8_E", 2.5f, 38.25f, 50f, room);   // shared with the symbol room

        GameObject chest = Box("Chest", new Vector3(16f, 0.5f, 44f), new Vector3(1.8f, 1f, 1.1f), Wood, room);
        Decor("ChestLid", new Vector3(16f, 1.06f, 44f), new Vector3(1.9f, 0.12f, 1.2f), Wood * 0.8f, room);
        GameObject stone = CreatePickup(new Vector3(15.5f, 1.4f, 44f), "Item_StoneFragment", room);
        stone.name = "Pickup_StoneFragment_Pedestal_Chest";
        GameObject bone = CreatePickup(new Vector3(16.5f, 1.4f, 44f), "Item_BoneKeyFragment", room);
        PickupVariant(bone, 1);   // the second piece of the key (the basement has the first, the box room the third)
        stone.SetActive(false);
        bone.SetActive(false);
        ItemSocket lockSocket = Socket(chest, "Item_SymbolKey", 1, true, "unlock chest with", null, null);
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(lockSocket.onFilled, stone.SetActive, true);
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(lockSocket.onFilled, bone.SetActive, true);
    }

    // Room 6 (blue): the stone room (x 7..19.75, z 21.5..38.25), from the middle room. Piecing the stone tablet
    // together at the pedestal (the puzzle board, with the three fragments on you) opens the door south into the box room.
    private static void BuildStoneRoom()
    {
        Transform room = Group("Room_6_StoneRoom");
        Deck(room, 7f, 21.5f, 19.75f, 38.25f, 0f, Room6Blue);
        WallX("R6_S", 21.5f, 7f, 19.75f, room, 10.5f, 10.5f + DoorGap);
        Door toBoxRoom = DoorX("Door_BoxRoom", 21.5f, 10.5f, true, false, room);
        SetField(toBoxRoom, "lockedHint", p => p.stringValue = "Locked. Piece the stone tablet together at the pedestal to open it.");

        GameObject pedestal = Box("Pedestal", new Vector3(13.5f, 0.6f, 34.25f), new Vector3(1.2f, 1.2f, 1.2f), Prop, room);
        PuzzleStation tablet = PuzzleBuildTools.AddStation(pedestal, "Puzzle_StoneTablet", "Item_StoneFragment", 3, true, "piece the tablet together");
        PuzzleBuildTools.SolveOpens(tablet, toBoxRoom);
        MuralAt(new Vector3(19.45f, 2.4f, 30f), new Vector2(8f, 1.6f), Vector3.left, room);
    }

    // Room 7 (yellow): the box room, the whole south strip (x -12.25..19.75, z 8.5..21.5) up against room 1's wall.
    // The closet in the south-east corner holds the last bone fragment. Its puzzle (box pushing) is being made by a
    // teammate; until then the closet door simply opens with E.
    private static void BuildBoxRoom()
    {
        Transform room = Group("Room_7_BoxRoom");
        Deck(room, -12.25f, 8.5f, 7f, 19.75f, 0f, Room7Yellow);    // west of the stone room: up to the middle room's wall
        Deck(room, 7f, 8.5f, 19.75f, 21.5f, 0f, Room7Yellow);      // east: up to the stone room's wall

        // The closet: x 15..19.75, z 8.5..12.5, its door in its north wall.
        WallZ("Closet_W", 15f, 8.5f, 12.5f, room);
        WallX("Closet_N", 12.5f, 15f, 19.75f, room, 16f, 16f + DoorGap);
        DoorX("Door_Closet", 12.5f, 16f, false, false, room);   // unlocked until the box puzzle is in
        Box("OpenChest", new Vector3(17.4f, 0.4f, 10f), new Vector3(1.4f, 0.8f, 0.9f), Wood, room);
        PickupVariant(CreatePickup(new Vector3(17.4f, 1.1f, 10f), "Item_BoneKeyFragment", room), 2);

        CreateSchool("Fish_Wanderer", 3, new Vector3(-5f, 2.5f, 15f), room);
    }

    // Room 8: the symbol room (x -7..2.5, z 38.25..50) before the hallway: the three symbols and the code lock
    // (placeholder: the rune door simply opens with E) beside the rune door north.
    private static void BuildSymbolRoom()
    {
        Transform room = Group("Room_8_SymbolRoom");
        Deck(room, -7f, 38.25f, 2.5f, 50f, 0f, new Color(0.3f, 0.22f, 0.36f));
        Decor("Symbol1_Lock", new Vector3(-3.4f, 3.2f, 49.7f), new Vector3(0.7f, 0.7f, 0.06f), new Color(0.9f, 0.15f, 0.1f), room);
        Decor("Symbol2_Lock", new Vector3(-2.6f, 3.2f, 49.7f), new Vector3(0.7f, 0.7f, 0.06f), new Color(0.15f, 0.2f, 0.9f), room);
        Decor("Symbol3_Lock", new Vector3(-3.4f, 2.3f, 49.7f), new Vector3(0.7f, 0.7f, 0.06f), new Color(0.2f, 0.85f, 0.35f), room);
        // The rune puzzle: the three symbols in order, on the board; solving it unlocks the rune door (wired in the hallway).
        GameObject codeLock = Box("CodeLock", new Vector3(-2.6f, 2.3f, 49.6f), new Vector3(0.7f, 0.7f, 0.3f), new Color(0.8f, 0.7f, 0.3f), room);
        runeStation = PuzzleBuildTools.AddStation(codeLock, "Puzzle_Runes", null, 0, false, "enter the runes");
    }

    // Room 9: the hallway (x -29..-4.75, z 50..58.5) and the room east of it (x -4.75..19.75) that the rune door opens
    // into (9b). The pack waits in a vent at the hallway's far west end. The chase starts as you come through the rune
    // door into 9b (the vent is in plain view down the hallway from there), and everything you need is ahead of you,
    // away from the pack: two stone fragments in 9b, the tablet beside the door east into the column, which it unlocks.
    private static void BuildHallway()
    {
        Transform room = Group("Room_9_Hallway");
        Deck(room, HullW, 50f, -4.75f, HullN, 0f, new Color(0.24f, 0.24f, 0.34f));
        Deck(room, -4.75f, 50f, 19.75f, HullN, 0f, new Color(0.26f, 0.3f, 0.34f));

        // The z 50 line: rooms 3 / 8 / 5 below, the hallway and room 9b above: the door down into the nook, and the
        // rune door at the right end of the symbol room.
        WallX("Z50", 50f, HullW, 19.75f, room, -25f, -25f + DoorGap, -0.5f, -0.5f + DoorGap);
        DoorX("Door_Nook", 50f, -25f, false, false, room);
        finalDoor = DoorX("Door_Final", 50f, -0.5f, true, true, room);   // locked until the rune puzzle at the code lock is solved
        SetField(finalDoor, "lockedHint", p => p.stringValue = "Locked. Enter the three symbols on the rune lock beside it.");
        SetField(finalDoor, "lockBehind", p => p.boolValue = false);
        SetField(finalDoor, "openPrompt", p => p.stringValue = "open the rune door");
        if (runeStation != null)
        {
            PuzzleBuildTools.SolveUnlocks(runeStation, finalDoor);
            PuzzleBuildTools.SolveOpens(runeStation, finalDoor);   // and swings it open, so solving the runes is what lets the chase begin
        }
        // Hallway / room 9b divider with an open doorway.
        WallZ("Hall_Div", -4.75f, 50f, HullN, room, 51f, 54.5f);

        // West end of the hull with the vent hole (y 2.4..4, z 52.5..54.9) and the hollow duct outside it.
        float x = HullW;
        Piece("Hull_W_Hall_Low", false, x, 50f, HullN, 0f, 2.4f, room);
        Piece("Hull_W_Hall_High", false, x, 50f, HullN, 4f, Ceiling, room);
        Piece("Hull_W_Hall_S", false, x, 50f, 52.5f, 2.4f, 4f, room);
        Piece("Hull_W_Hall_N", false, x, 54.9f, HullN, 2.4f, 4f, room);
        Color duct = new Color(0.03f, 0.03f, 0.04f);
        Box("Vent_Back", new Vector3(x - 2.85f, 3.2f, 53.7f), new Vector3(0.2f, 2f, 2.8f), duct, room);
        Box("Vent_Floor", new Vector3(x - 1.5f, 2.3f, 53.7f), new Vector3(2.5f, 0.2f, 2.8f), duct, room);
        Box("Vent_Ceiling", new Vector3(x - 1.5f, 4.1f, 53.7f), new Vector3(2.5f, 0.2f, 2.8f), duct, room);
        Box("Vent_S", new Vector3(x - 1.5f, 3.2f, 52.4f), new Vector3(2.5f, 2f, 0.2f), duct, room);
        Box("Vent_N", new Vector3(x - 1.5f, 3.2f, 55f), new Vector3(2.5f, 2f, 0.2f), duct, room);
        MuralAt(new Vector3(-22f, 2.4f, 50.3f), new Vector2(6f, 1.6f), Vector3.forward, room);
        MuralAt(new Vector3(-12f, 2.4f, 50.3f), new Vector2(5f, 1.6f), Vector3.forward, room);

        // The pack in the duct, the grate over the hole, and the sequence that runs them.
        var chaseGo = new GameObject("ChaseSequence");
        chaseGo.transform.SetParent(room, false);
        chaseGo.transform.position = new Vector3(x - 1.4f, 3.2f, 53.7f);
        var chase = chaseGo.AddComponent<ChaseSequence>();
        SetField(chase, "startSound", p => p.objectReferenceValue = Sfx("Deep Sea Monster sound effect.mp3"));
        SetField(chase, "endSound", p => p.objectReferenceValue = Sfx("Puzzle Completed Sound effect.mp3"));
        SetField(chase, "grateSound", p => p.objectReferenceValue = Sfx("Hit impact.wav"));
        SetField(chase, "startDoor", p => p.objectReferenceValue = finalDoor);
        // The rune door arms it; the cutscene plays as soon as the player is through it, in the west part of 9b: from
        // there the vent is straight down the hallway through the divider doorway, and the way on is east.
        var start = new GameObject("ChaseStart");
        start.transform.SetParent(room, false);
        // Half a metre in from the rune door and the divider, so it starts once the player is through, not in the doorway.
        start.transform.position = new Vector3(0.125f, Ceiling * 0.5f, (50.5f + HullN - 0.25f) * 0.5f);
        var startBox = start.AddComponent<BoxCollider>();
        startBox.isTrigger = true;
        startBox.size = new Vector3(8.75f, Ceiling, HullN - 0.25f - 50.5f);
        SetField(chase, "startTrigger", p => p.objectReferenceValue = start.AddComponent<PlayerAreaTrigger>());
        GameObject pursuerPrefab = ChaseTools.EnsurePursuerPrefab();
        Vector3[] slots = { new Vector3(x - 2.2f, 3.2f, 53.1f), new Vector3(x - 1.5f, 3.2f, 53.7f), new Vector3(x - 0.8f, 3.2f, 54.3f) };
        for (int i = 0; i < slots.Length; i++)
        {
            var fish = (GameObject)PrefabUtility.InstantiatePrefab(pursuerPrefab, room.gameObject.scene);
            fish.transform.SetParent(chaseGo.transform, true);
            fish.transform.SetPositionAndRotation(slots[i], Quaternion.Euler(0f, 90f, 0f));
            fish.name = "ChasePufferfish_" + (i + 1);
            fish.SetActive(false);
        }
        var grate = new GameObject("VentGrate");
        grate.transform.SetParent(room, false);
        grate.transform.SetPositionAndRotation(new Vector3(x + WallT * 0.5f, 3.2f, 53.7f), Quaternion.Euler(0f, 90f, 0f));
        Color iron = new Color(0.2f, 0.2f, 0.22f);
        foreach (float z in new[] { 53.05f, 53.7f, 54.35f })
            Decor("Bar", new Vector3(x + WallT * 0.5f, 3.2f, z), new Vector3(0.08f, 1.6f, 0.08f), iron, room).transform.SetParent(grate.transform, true);
        foreach (float y in new[] { 2.85f, 3.55f })
            Decor("Bar", new Vector3(x + WallT * 0.5f, y, 53.7f), new Vector3(0.08f, 0.08f, 2.4f), iron, room).transform.SetParent(grate.transform, true);
        SetField(chase, "grate", p => p.objectReferenceValue = grate.transform);

        // Under pressure, and always ahead of the pack: two stone fragments in 9b on the way east (one up by the ceiling,
        // one on the floor), for the tablet on the wall beside the column door. No other fish in 9b, so the only fish
        // there are the ones after you.
        CreatePickup(new Vector3(5f, 4.2f, 57.5f), "Item_StoneFragment", room).name = "Pickup_StoneFragment_ChaseTablet_High";   // not for the pedestal: the chase tablet
        CreatePickup(new Vector3(12f, 0.6f, 52f), "Item_StoneFragment", room).name = "Pickup_StoneFragment_ChaseTablet_Low";
        GameObject tablet = Box("RuneTablet", new Vector3(19.35f, 1.8f, 55.5f), new Vector3(0.2f, 1.3f, 1.1f), Stone, room);
        var slotted = new GameObject[2];
        for (int i = 0; i < slotted.Length; i++)
        {
            slotted[i] = Box("Placed_Stone_" + (i + 1), new Vector3(19.2f, 1.8f, 55.2f + i * 0.6f), new Vector3(0.16f, 0.35f, 0.3f), new Color(0.4f, 0.8f, 0.85f), room);
            slotted[i].SetActive(false);
        }
        hallwayTablet = Socket(tablet, "Item_StoneFragment", 2, true, "slot", null, slotted);   // the column door it opens is made by BuildColumn
    }

    // The column down the east side (x 19.75..29): the trident corridor north of the divider, the rubble that seals it,
    // the trident on its pedestal, then the exit corridor south with the first trident fights and the one-way exit.
    private static void BuildColumn()
    {
        Transform room = Group("Column_Trident");
        Deck(room, 19.75f, HullS, HullE, HullN, 0f, new Color(0.28f, 0.3f, 0.3f));
        WallZ("Column_W", 19.75f, HullS, HullN, room, 51f, 51f + DoorGap);
        columnDoor = DoorZ("Door_Column", 19.75f, 51f, true, false, room);
        SetField(columnDoor, "lockedHint", p => p.stringValue = "Locked. Put two stone fragments into the tablet beside it.");
        if (hallwayTablet != null)
            OpenOnFilled(hallwayTablet, columnDoor);
        WallX("Column_Div", 23.75f, 19.75f, HullE, room, 23f, 23f + DoorGap);
        DoorX("Door_ColumnDivider", 23.75f, 23f, false, false, room);
        WallX("Column_ExitWall", 3f, 19.75f, HullE, room, 22.5f, 22.5f + DoorGap);
        Door exit = SpawnDoor("Door_Exit", new Vector3(22.5f + 2.4f, 0f, 3f), false, true, room, 180f);
        SetField(exit, "openPrompt", p => p.stringValue = "open the way out");
        var ending = Object.FindFirstObjectByType<ChaseSequence>();
        if (ending != null)
            SetField(ending, "endDoor", p => p.objectReferenceValue = exit);   // opening it rolls the credits

        // Rubble: rocks placed where they land across the whole corridor; a blocker seals the gaps.
        var rubbleGo = new GameObject("Rubble");
        rubbleGo.transform.SetParent(room, false);
        rubbleGo.transform.position = new Vector3(24.375f, 0f, 36f);
        var blockerGo = new GameObject("Blocker");
        blockerGo.transform.SetParent(rubbleGo.transform, false);
        blockerGo.transform.localPosition = new Vector3(0f, Ceiling * 0.5f, 0f);
        var blocker = blockerGo.AddComponent<BoxCollider>();
        blocker.size = new Vector3(8.75f, Ceiling, 1.8f);
        int n = 0;
        foreach (var row in new[] { (y: 0.8f, count: 5, size: 1.75f, start: 20.9f), (y: 2.3f, count: 4, size: 1.65f, start: 21.7f), (y: 3.75f, count: 4, size: 1.45f, start: 21.4f) })
        {
            for (int i = 0; i < row.count; i++, n++)
            {
                float jitter = (n * 37 % 10 - 5) * 0.03f;
                GameObject r = Box("Rock", new Vector3(row.start + i * 1.75f + jitter, row.y, 36f + jitter * 3f), Vector3.one * (row.size + jitter), Rock, rubbleGo.transform);
                r.transform.rotation = Quaternion.Euler(n * 17f % 30f - 15f, n * 41f % 90f, n * 23f % 30f - 15f);
            }
        }
        var rubble = rubbleGo.AddComponent<RubbleFall>();
        SetField(rubble, "blocker", p => p.objectReferenceValue = blocker);
        SetField(rubble, "thudSound", p => p.objectReferenceValue = Sfx("Hit impact.wav"));
        var chase = Object.FindFirstObjectByType<ChaseSequence>();
        if (chase != null)
            SetField(chase, "endRubble", p => p.objectReferenceValue = rubble);

        // The trident on its pedestal (button mash later): taking it drops the rubble behind you.
        Box("Pedestal_Trident", new Vector3(24f, 0.6f, 28.75f), new Vector3(1.2f, 1.2f, 1.2f), Prop, room);
        GameObject trident = CreatePickup(new Vector3(24f, 1.9f, 28.75f), "Item_Trident", room);   // standing up, clear of the pedestal top
        SetField(rubble, "dropOnPickup", p => p.objectReferenceValue = trident != null ? trident.GetComponentInChildren<PickupItem>() : null);

        // South of the divider: the first fights with the trident, then the one-way exit.
        CreateSchool("Fish_Wanderer", 4, new Vector3(24f, 2.5f, 14f), room);
        Spawn("Pufferfish", new Vector3(26f, 2f, 10f), room, 160f);
        GameObject plate = Spawn("RespawnPlate", new Vector3(27.8f, 0.05f, 20f), room);   // against the east wall, off the middle of the column
        if (plate != null)
            plate.name = "Checkpoint_Column";
    }

    // ---- player, HUD, map ----------------------------------------------------------------------------------------

    private static void PlacePlayer()
    {
        var player = Object.FindFirstObjectByType<SwimController>();
        if (player != null)
        {
            player.transform.SetPositionAndRotation(Spawn, Quaternion.Euler(0f, 30f, 0f));
            if (player.GetComponent<PlayerInventory>() == null)
                player.gameObject.AddComponent<PlayerInventory>();
        }

        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        if (spawnPoint == null)
            spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.SetPositionAndRotation(Spawn, Quaternion.Euler(0f, 30f, 0f));

        // The spawn checkpoint: a RespawnPlate instance beside the spawn (the pillar would swallow the player on it).
        // An old hand-made one is replaced so it gets the model like every other checkpoint.
        GameObject checkpoint = GameObject.Find("Checkpoint_Spawn");
        Transform parent = checkpoint != null ? checkpoint.transform.parent : null;
        if (checkpoint != null && PrefabUtility.GetCorrespondingObjectFromSource(checkpoint) == null)
        {
            Object.DestroyImmediate(checkpoint);
            checkpoint = null;
        }
        if (checkpoint == null)
        {
            checkpoint = Spawn("RespawnPlate", SpawnCheckpoint, parent != null ? parent : ship);
            if (checkpoint != null)
                checkpoint.name = "Checkpoint_Spawn";
        }
        if (checkpoint != null)
        {
            checkpoint.transform.position = SpawnCheckpoint;
            var plate = checkpoint.GetComponentInChildren<Checkpoint>();
            if (plate != null)
                SetField(plate, "startsActivated", p => p.boolValue = true);
        }

        var death = Object.FindFirstObjectByType<DeathManager>();
        if (death != null)
            SetField(death, "respawnPoint", p =>
            {
                if (p.objectReferenceValue == null)
                    p.objectReferenceValue = spawnPoint.transform;
            });
    }

    // The lighting, in layers: a dim cool fill in every room and along the basement so nothing is pitch black; the
    // water light, a cool beam slanting in through every window and the sun falling through the roof holes (each with
    // a faint shaft); warm spots on what matters (the dresser, the seaweed, the pedestals, the locks).
    private static void BuildLights()
    {
        Transform lights = Group("Lights");

        var fill = new Color(0.42f, 0.66f, 0.95f);
        var below = new Color(0.4f, 0.72f, 0.62f);
        foreach (var (name, x, z) in new[]
        {
            ("Room1", -20.6f, 18.6f), ("Room2", -2.6f, 29f), ("Room3", -20.6f, 33.5f), ("Nook", -18f, 47f),
            ("Room5", 11f, 44f), ("Room6", 13.4f, 30f), ("Room7", 3.75f, 15f), ("Room8", -2.25f, 44f),
            ("Hallway_W", -17f, 54.25f), ("Hallway_E", 7.5f, 54.25f), ("Column_S", 24.4f, 15f), ("Column_N", 24.4f, 45f),
        })
            RoomLight("Light_" + name, new Vector3(x, Ceiling - 1.9f, z), fill, 1.6f, 20f, lights);
        foreach (var (name, x, z) in new[] { ("Basement_SW", -15f, 12f), ("Basement_SE", 15f, 12f), ("Basement_NW", -15f, 32f), ("Basement_NE", 15f, 32f) })
            RoomLight("Light_" + name, new Vector3(x, BasementCeiling - 2.4f, z), below, 1.6f, 20f, lights);

        Transform water = Group("WaterLight");
        water.SetParent(lights, false);
        foreach (FishWindow window in ship.GetComponentsInChildren<FishWindow>(true))
        {
            Transform w = window.transform;
            if (Mathf.Abs(w.forward.y) > 0.3f)
                continue;   // the roof holes: the sun does those
            bool shadows = (w.position.x < -20f && w.position.y > 0f) || w.position.y < 0f;   // the spawn and fish room windows, and the cellar's
            WindowBeam(w, shadows, water);
        }
        // The roof holes: a shaft down each along the sun's light, and a soft spot to lift the pool it lands in.
        Vector3 sunward = SunRotation * Vector3.forward;
        foreach (var hole in RoofBreaks)
        {
            Vector3 top = new Vector3(hole.centre.x, Ceiling + SlabT + 0.2f, hole.centre.y);
            float length = (Ceiling + SlabT + 0.2f) / Mathf.Max(0.2f, -sunward.y);
            float width = Mathf.Min(hole.radii.x, hole.radii.y) * 1.6f;
            Shaft("Shaft_RoofHole", top, sunward, length, width, new Color(0.6f, 0.9f, 1f, 0.2f), water);
            if (Mathf.Min(hole.radii.x, hole.radii.y) >= 0.7f)
                BeamLight("Beam_RoofHole", top - sunward * 3f, sunward, 20f + Mathf.Max(hole.radii.x, hole.radii.y) * 12f, 2.2f, 14f, true, new Color(0.7f, 0.92f, 1f), water);
        }

        Transform accents = Group("Accents");
        accents.SetParent(lights, false);
        foreach (var (name, target, from) in new[]
        {
            ("Dresser", new Vector3(-15.14f, 0.8f, 10.6f), new Vector3(-15.14f, 4.6f, 12.8f)),
            ("Seaweed", SeaweedSpot + Vector3.up, SeaweedSpot + new Vector3(-1.2f, 4.6f, -2f)),
            ("StoneTablet", new Vector3(13.5f, 1.2f, 34.25f), new Vector3(13.5f, 4.6f, 32.3f)),
            ("Chest", new Vector3(16f, 1f, 44f), new Vector3(16f, 4.6f, 42.2f)),
            ("ChestWindow", new Vector3((ChestWindow.x + ChestWindow.y) * 0.5f, 1.9f, 38.25f), new Vector3((ChestWindow.x + ChestWindow.y) * 0.5f, 4.6f, 36f)),
            ("CodeLock", new Vector3(-2.6f, 2.3f, 49.5f), new Vector3(-2.6f, 4.6f, 46.8f)),
            ("HallwayTablet", new Vector3(19.35f, 1.8f, 55.5f), new Vector3(17.2f, 4.6f, 55.5f)),
            ("Trident", new Vector3(24f, 1.8f, 28.75f), new Vector3(24f, 4.6f, 26.8f)),
        })
            Accent(name, target, from, accents);
    }

    // Where the enemy pufferfish come from: red, spiny, glowing nests (Puffer Nest), easy to tell from the friendly
    // fish's windows and roof holes. Each keeps a couple of pufferfish out while you are near and in its sight, warns
    // before each one, and can be slashed to pieces to stop it. In the fish room, the basement (two), the chest room,
    // the stone room and the box room, clear of the ways through.
    private static void BuildNests()
    {
        Transform nests = Group("PufferNests");
        nests.SetParent(ship, false);
        // Tucked into the fish room's far north-east corner, round the corner from the door in: out of sight (and out of
        // range) on the way to the dagger, so you meet it armed.
        NestAt("PufferNest_FishRoom", new Vector3(-8.1f, 0f, 43f), Vector3.up, 2, nests);
        NestAt("PufferNest_Basement_W", CellarGround(-8f, 24f), Vector3.up, 2, nests);
        NestAt("PufferNest_Basement_E", CellarGround(12f, 34f), Vector3.up, 2, nests);
        NestAt("PufferNest_ChestRoom", new Vector3(8f, 0f, 46f), Vector3.up, 1, nests);
        NestAt("PufferNest_StoneRoom", new Vector3(16.5f, 0f, 25f), Vector3.up, 1, nests);
        NestAt("PufferNest_BoxRoom", new Vector3(-6f, 0f, 12f), Vector3.up, 2, nests);
    }

    // Collectibles tucked away round the ship, for the counter in the corner: high corners you have to swim up to,
    // corners behind things. (More are in the rooms: the spawn room, the dresser, the middle room's roof hole, the
    // basement and its current.)
    private static void BuildCollectibles()
    {
        Transform group = Group("Collectibles");
        foreach (var (item, x, y, z) in new[]
        {
            ("Item_Shell", -28.2f, 4.4f, 29.5f),   // fish room, high in the north-west corner
            ("Item_Pearl", 19f, 4.4f, 37.5f),      // stone room, high behind the pedestal
            ("Item_Shell", -11.5f, 0.6f, 9.2f),    // box room, the far south-west corner
            ("Item_Pearl", 19f, 4.4f, 49.3f),      // chest room, high in the far corner
            ("Item_Shell", 19f, 0.6f, 57.8f),      // the room past the rune door
            ("Item_Pearl", 28.2f, 4.4f, 5f),       // the column, high by the way out
            ("Item_Pearl", 2.2f, 4.3f, 24.6f),     // middle room, up in the sunlight under a roof hole
        })
            CreatePickup(new Vector3(x, y, z), item, group);
    }

    // The sun, turned high (Lighting Tools' Sun Rotation) so it shows through the roof holes and its light falls in
    // through them, and the ocean surface overhead (Ocean Surface: the shimmering underside and the sun's glow).
    private static void BuildSunAndSurface()
    {
        SunAndSurface(ship);
    }

    // Nothing left poking through the level: a pickup placed inside something solid (a wall, a pillar, a pedestal)
    // is lifted clear, and seaweed growing into a wall or pillar is reported. Things hidden in drawers are left alone.
    private static void CheckClearance()
    {
        Physics.SyncTransforms();
        var hits = new Collider[16];
        foreach (PickupItem pickup in ship.GetComponentsInChildren<PickupItem>(true))
        {
            if (pickup.GetComponentInParent<DrawerLoot>(true) != null)
                continue;
            Transform t = pickup.transform;
            Vector3 was = t.position;
            for (int step = 0; step < 25 && Blocked(t, t.position, 0.15f, hits); step++)
            {
                t.position += Vector3.up * 0.1f;
                Physics.SyncTransforms();
            }
            if (t.position != was)
                Debug.Log($"Ship greybox: lifted {t.name} {t.position.y - was.y:0.0} m clear of what it was inside.", t);
        }
        foreach (Seaweed weed in ship.GetComponentsInChildren<Seaweed>(true))
            if (Blocked(weed.transform, weed.transform.position + Vector3.up * 0.8f, 0.35f, hits))
                Debug.LogWarning($"Ship greybox: {weed.name} grows into something at {weed.transform.position}.", weed);
    }

    private static bool Blocked(Transform self, Vector3 at, float radius, Collider[] hits)
    {
        int count = Physics.OverlapSphereNonAlloc(at, radius, hits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
            if (!hits[i].transform.IsChildOf(self))
                return true;
        return false;
    }

    // The pause menu's map page: the GDD floor plans, cropped to the hull, with the hull edges as the world corners.
    private static void SetMapBounds()
    {
        var map = Object.FindFirstObjectByType<MapPage>(FindObjectsInactive.Include);
        if (map == null)
            return;
        SetField(map, "mapTexture", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/ShipMap_Deck.png"));
        SetField(map, "basementTexture", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/UI/ShipMap_Basement.png"));
        SetField(map, "basementBelowY", p => p.floatValue = BasementCeiling);
        SetField(map, "worldMin", p => p.vector2Value = new Vector2(-28.75f, HullS));
        SetField(map, "worldMax", p => p.vector2Value = new Vector2(28.75f, HullN));
    }

    // ---- building blocks -----------------------------------------------------------------------------------------

    // A wall along X at z, from x1 to x2, floor to ceiling, with door-height gaps (pairs of x) and a lintel over each.
    private static void WallX(string name, float z, float x1, float x2, Transform parent, params float[] gaps) =>
        WallRun(name, true, z, x1, x2, 0f, Ceiling, parent, gaps);

    private static void WallZ(string name, float x, float z1, float z2, Transform parent, params float[] gaps) =>
        WallRun(name, false, x, z1, z2, 0f, Ceiling, parent, gaps);

    // A wall along X with taller gaps, for the double door.
    private static void WallXBig(string name, float z, float x1, float x2, Transform parent, params float[] gaps) =>
        WallRun(name, true, z, x1, x2, 0f, Ceiling, parent, gaps, DoubleDoorTop);

    private static void BasementWallX(string name, float z, float x1, float x2, Transform parent, params float[] gaps) =>
        WallRun(name, true, z, x1, x2, BasementFloor, BasementCeiling, parent, gaps);

    private static void BasementWallZ(string name, float x, float z1, float z2, Transform parent, params float[] gaps) =>
        WallRun(name, false, x, z1, z2, BasementFloor, BasementCeiling, parent, gaps);

    private static void WallRun(string name, bool alongX, float cross, float a, float b, float y0, float y1, Transform parent, float[] gaps, float doorTop = DoorTop)
    {
        if (a > b)
            (a, b) = (b, a);
        var cuts = new List<float> { a, b };
        for (int i = 0; i + 1 < gaps.Length; i += 2)
        {
            cuts.Add(Mathf.Clamp(Mathf.Min(gaps[i], gaps[i + 1]), a, b));
            cuts.Add(Mathf.Clamp(Mathf.Max(gaps[i], gaps[i + 1]), a, b));
        }
        cuts.Sort();
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            float from = cuts[i], to = cuts[i + 1];
            if (to - from < 0.01f)
                continue;
            float mid = (from + to) * 0.5f;
            bool inGap = false;
            for (int g = 0; g + 1 < gaps.Length; g += 2)
                inGap |= mid > Mathf.Min(gaps[g], gaps[g + 1]) && mid < Mathf.Max(gaps[g], gaps[g + 1]);
            float bottom = inGap ? y0 + doorTop : y0;
            if (y1 - bottom > 0.01f)
                Piece(name, alongX, cross, from, to, bottom, y1, parent);
        }
    }

    // The window into the chest room: the wall under it (the sill) and over it up to where the double door's tall gap
    // stops (the wall run above that is already there), and a worn brass frame round the opening, a little proud of the
    // wall on both sides. The frame has no colliders, so the whole 1.8 x 1.8 m opening is free to swim through.
    private static void ChestRoomWindow(Transform parent)
    {
        const float z = 38.25f;
        float x1 = ChestWindow.x, x2 = ChestWindow.y, sill = ChestWindow.z, top = ChestWindow.w;
        Piece("R2_N", true, z, x1, x2, 0f, sill, parent);
        Piece("R2_N", true, z, x1, x2, top, DoubleDoorTop, parent);

        var brass = new Color(0.55f, 0.42f, 0.2f);
        const float bar = 0.12f, depth = WallT + 0.1f;
        float midX = (x1 + x2) * 0.5f, midY = (sill + top) * 0.5f;
        Decor("ChestWindow_Frame", new Vector3(midX, sill + bar * 0.5f, z), new Vector3(x2 - x1 + bar * 2f, bar, depth), brass, parent);
        Decor("ChestWindow_Frame", new Vector3(midX, top - bar * 0.5f, z), new Vector3(x2 - x1 + bar * 2f, bar, depth), brass, parent);
        Decor("ChestWindow_Frame", new Vector3(x1 + bar * 0.5f, midY, z), new Vector3(bar, top - sill, depth), brass, parent);
        Decor("ChestWindow_Frame", new Vector3(x2 - bar * 0.5f, midY, z), new Vector3(bar, top - sill, depth), brass, parent);
        // What is left of the glass: a few jagged bits still stuck in the corners.
        var glass = new Color(0.55f, 0.8f, 0.85f);
        Decor("ChestWindow_Glass", new Vector3(x1 + 0.22f, top - 0.2f, z), new Vector3(0.22f, 0.28f, 0.02f), glass, parent);
        Decor("ChestWindow_Glass", new Vector3(x2 - 0.18f, sill + 0.24f, z), new Vector3(0.18f, 0.34f, 0.02f), glass, parent);
        Decor("ChestWindow_Glass", new Vector3(x2 - 0.3f, top - 0.16f, z), new Vector3(0.36f, 0.14f, 0.02f), glass, parent);
    }

    // One wall box: along X at z = cross (or along Z at x = cross), from..to, between the given heights.
    private static void Piece(string name, bool alongX, float cross, float from, float to, float y0, float y1, Transform parent)
    {
        float mid = (from + to) * 0.5f;
        Vector3 center = alongX ? new Vector3(mid, (y0 + y1) * 0.5f, cross) : new Vector3(cross, (y0 + y1) * 0.5f, mid);
        Vector3 size = alongX ? new Vector3(to - from, y1 - y0, WallT) : new Vector3(WallT, y1 - y0, to - from);
        Box(name, center, size, Hull, parent);
    }

    // A door in a wall along X at z, its gap starting at gapStart (the wall must have the matching gap).
    private static Door DoorX(string name, float z, float gapStart, bool locked, bool closeBehind, Transform parent) =>
        SpawnDoor(name, new Vector3(gapStart + 0.4f, 0f, z), locked, closeBehind, parent, 0f);

    // A door in a wall along Z at x: the door spans -Z from its hinge, so the hinge sits 0.4 m (a frame post) short
    // of the gap's far end and the frame fills the gap exactly.
    private static Door DoorZ(string name, float x, float gapStart, bool locked, bool closeBehind, Transform parent) =>
        SpawnDoor(name, new Vector3(x, 0f, gapStart + DoorGap - 0.4f), locked, closeBehind, parent, 90f);

    private static void Slab(string name, float x1, float z1, float x2, float z2, float top, Transform parent)
    {
        Box(name, new Vector3((x1 + x2) * 0.5f, top - SlabT * 0.5f, (z1 + z2) * 0.5f), new Vector3(x2 - x1, SlabT, z2 - z1), Hull, parent);
    }

    // A slab with rectangular holes (hatches): cut into bands along x, each split north / south round its hole.
    private static void SlabWithHoles(string name, float x1, float z1, float x2, float z2, float top, Rect[] holes, Transform parent)
    {
        Bands(x1, z1, x2, z2, holes, (a, b, c, d) => Slab(name, a, b, c, d, top, parent));
    }

    // A rectangle minus rectangular holes, handed out as pieces: bands along x, each split north / south round its hole.
    private static void Bands(float x1, float z1, float x2, float z2, Rect[] holes, System.Action<float, float, float, float> piece)
    {
        if (holes == null || holes.Length == 0)
        {
            piece(x1, z1, x2, z2);
            return;
        }
        var xs = new List<float> { x1, x2 };
        foreach (Rect hole in holes)
        {
            xs.Add(Mathf.Clamp(hole.xMin, x1, x2));
            xs.Add(Mathf.Clamp(hole.xMax, x1, x2));
        }
        xs.Sort();
        for (int i = 0; i + 1 < xs.Count; i++)
        {
            float a = xs[i], b = xs[i + 1];
            if (b - a < 0.01f)
                continue;
            float mid = (a + b) * 0.5f;
            bool cut = false;
            foreach (Rect hole in holes)
            {
                if (mid <= hole.xMin || mid >= hole.xMax)
                    continue;
                if (hole.yMin - z1 > 0.01f)
                    piece(a, z1, b, hole.yMin);
                if (z2 - hole.yMax > 0.01f)
                    piece(a, hole.yMax, b, z2);
                cut = true;
                break;
            }
            if (!cut)
                piece(a, z1, b, z2);
        }
    }

    // A coloured grid patch a hair above the hull slab (3 cm: far enough that the two never fight at a distance), cut
    // round any hatch holes so the way down is open to look at, not only to swim through.
    private static void Deck(Transform parent, float x1, float z1, float x2, float z2, float floorY, Color color, params Rect[] holes)
    {
        Bands(x1, z1, x2, z2, holes, (a, b, c, d) =>
        {
            var deck = new GameObject("Deck");
            deck.transform.SetParent(parent, false);
            deck.transform.position = new Vector3((a + c) * 0.5f, floorY + 0.03f, (b + d) * 0.5f);
            deck.AddComponent<GridFloor>().Setup(new Vector2(c - a, d - b), color, EnsureGridMaterial());
        });
    }

    private const string GridTexturePath = "Assets/Art/Textures/GridFloor.png";
    private const string GridMaterialPath = "Assets/Art/Materials/GridFloor.mat";

    // GridFloor.mat: URP Lit with the generated grid texture (saved as a PNG so it is a normal asset), shared by every deck.
    private static Material EnsureGridMaterial()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        if (texture == null)
        {
            Texture2D generated = GridFloor.MakeTexture(5);
            System.IO.File.WriteAllBytes(GridTexturePath, generated.EncodeToPNG());
            Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(GridTexturePath);
            if (AssetImporter.GetAtPath(GridTexturePath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 8;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GridTexturePath);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, GridMaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // A cabinet with one drawer that E slides out (a Door in Slide mode with a small visual). The root carries the
    // trigger and the Door; the drawer front is the Visual. slideOut is the direction it opens in.
    private const string DresserPrefabPath = "Assets/Prefabs/VetoLaatikko.prefab";
    private const string DresserDrawerPart = "Vetolaatikko.001";   // the model's one real drawer (its Animator slides it)
    // The model's grid of drawers, in its own units: 2 columns 0.9144 m apart and 3 rows 0.3048 m apart.
    private static readonly Vector2 DresserPitch = new Vector2(0.9144f, 0.3048f);
    private const int DresserColumns = 2;
    private const int DresserRows = 3;
    private const float DresserSlide = 0.497f;   // how far Vili's clip pulls the drawer out

    // The VetoLaatikko dresser with all six drawers opening. The model has one real drawer (Vetolaatikko.001, slid by
    // its Animator); the other five are just fronts baked into the cabinet. So the cabinet gets a copy of its mesh with
    // those fronts cut away and a hollow behind each (copied from the real drawer's hollow), and each gap gets a copy
    // of the real drawer that slides by code. Every drawer: Animated Drawer (E), a collider on its front and one on its
    // bottom, the glow. The cabinet: a solid box up to the drawer fronts (the model has no colliders). Drawer Loot hides
    // the key in a random drawer at start. False if the prefab is missing.
    private static bool Dresser(string name, Vector3 position, float yaw, GameObject key, Transform parent, params GameObject[] extras)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DresserPrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Ship greybox: no dresser at {DresserPrefabPath}, the placeholder cabinet is used.");
            return false;
        }
        var dresser = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(dresser, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        dresser.transform.SetParent(parent, true);
        dresser.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        dresser.name = name;

        Transform real = FindDeep(dresser.transform, DresserDrawerPart);
        MeshFilter drawerFilter = real != null ? real.GetComponent<MeshFilter>() : null;
        MeshFilter cabinet = null;
        foreach (MeshFilter filter in dresser.GetComponentsInChildren<MeshFilter>())
        {
            if (real == null || (filter.transform != real && !filter.transform.IsChildOf(real)))
            {
                cabinet = filter;
                break;
            }
        }
        if (drawerFilter == null || drawerFilter.sharedMesh == null || cabinet == null || cabinet.sharedMesh == null)
        {
            Debug.LogWarning($"Ship greybox: the dresser has no {DresserDrawerPart} drawer or no cabinet mesh, the placeholder cabinet is used.");
            Object.DestroyImmediate(dresser);
            return false;
        }
        real.localPosition = Vector3.zero;   // shut (the prefab shows it half out)
        Animator animator = dresser.GetComponentInChildren<Animator>();

        // The real drawer's box in the cabinet mesh's space (x across, y up, z out of the front), and where the others
        // are from it: across towards the middle, and down the rows (up, if it is a bottom drawer).
        Bounds slot = TransformBounds(drawerFilter.sharedMesh.bounds, cabinet.transform.worldToLocalMatrix * real.localToWorldMatrix);
        Bounds body = cabinet.sharedMesh.bounds;
        float across = slot.center.x > body.center.x ? -1f : 1f;
        float down = slot.center.y > body.center.y ? -1f : 1f;
        var offsets = new List<Vector3>();
        for (int column = 0; column < DresserColumns; column++)
            for (int row = 0; row < DresserRows; row++)
                if (column > 0 || row > 0)
                    offsets.Add(new Vector3(across * DresserPitch.x * column, down * DresserPitch.y * row, 0f));

        Mesh cut = CutDrawerFronts(cabinet.sharedMesh, slot, offsets);
        if (cut != null)
            cabinet.sharedMesh = cut;
        else
            offsets.Clear();   // could not read the mesh: just the one drawer

        // Copies of the bare drawer first, then every drawer gets its parts (the real one slid by its Animator).
        var copies = new List<Transform>();
        for (int i = 0; i < offsets.Count; i++)
        {
            GameObject copy = Object.Instantiate(real.gameObject, real.parent);
            copy.name = $"Vetolaatikko.{i + 2:000}";
            copy.transform.localPosition = real.localPosition + real.parent.InverseTransformVector(cabinet.transform.TransformVector(offsets[i]));
            copy.transform.localRotation = real.localRotation;
            copy.transform.localScale = real.localScale;
            copies.Add(copy.transform);
        }
        var drawers = new List<AnimatedDrawer> { SetUpDrawer(real, drawerFilter.sharedMesh, animator) };
        foreach (Transform copy in copies)
            drawers.Add(SetUpDrawer(copy, drawerFilter.sharedMesh, null));

        // The cabinet, solid up to just behind the drawer fronts, so looking at a drawer finds the drawer.
        Bounds solid = cut != null ? cut.bounds : body;
        solid.SetMinMax(solid.min, new Vector3(solid.max.x, solid.max.y, Mathf.Min(solid.max.z, slot.max.z - 0.045f)));
        var box = dresser.AddComponent<BoxCollider>();
        box.center = dresser.transform.InverseTransformPoint(cabinet.transform.TransformPoint(solid.center));
        Vector3 size = dresser.transform.InverseTransformVector(cabinet.transform.TransformVector(solid.size));
        box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        // The key: shown in the real drawer here; at start Drawer Loot moves it into a random one and hides it.
        if (key != null)
        {
            key.transform.SetParent(dresser.transform, true);
            key.transform.position = real.TransformPoint(StashPoint(drawerFilter.sharedMesh.bounds));
            var loot = dresser.AddComponent<DrawerLoot>();
            SetField(loot, "item", p => p.objectReferenceValue = key.transform);
            SetField(loot, "drawers", p =>
            {
                p.arraySize = drawers.Count;
                for (int i = 0; i < drawers.Count; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = drawers[i];
            });
            // Anything else hidden in it: shown in the next drawers here, shuffled at start like the key.
            for (int i = 0; i < extras.Length; i++)
            {
                extras[i].transform.SetParent(dresser.transform, true);
                if (i + 1 < drawers.Count)
                    extras[i].transform.position = drawers[i + 1].Part.TransformPoint(StashPoint(drawerFilter.sharedMesh.bounds));
            }
            SetField(loot, "extras", p =>
            {
                p.arraySize = extras.Length;
                for (int i = 0; i < extras.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = extras[i].transform;
            });
        }
        return true;
    }

    // One drawer: Animated Drawer (by the Animator, or sliding by code), colliders on its front and its bottom (so E
    // finds it, and you can look down past the front into it), the glow, and its sounds.
    private static AnimatedDrawer SetUpDrawer(Transform part, Mesh mesh, Animator animator)
    {
        Bounds b = mesh.bounds;
        var front = part.gameObject.AddComponent<BoxCollider>();
        front.center = new Vector3(b.center.x, b.center.y, b.max.z - 0.04f);
        front.size = new Vector3(b.size.x, b.size.y, 0.08f);
        var bottom = part.gameObject.AddComponent<BoxCollider>();
        bottom.center = new Vector3(b.center.x, b.min.y + 0.015f, b.center.z);
        bottom.size = new Vector3(b.size.x, 0.03f, b.size.z);

        var drawer = part.gameObject.AddComponent<AnimatedDrawer>();
        part.gameObject.AddComponent<InteractableHighlight>();
        const string sounds = "Assets/Sound/Doors/";
        SetField(drawer, "animator", p => p.objectReferenceValue = animator);
        SetField(drawer, "slide", p => p.vector3Value = new Vector3(0f, 0f, DresserSlide));
        SetField(drawer, "stashPoint", p => p.vector3Value = StashPoint(b));
        SetField(drawer, "openSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + "Door_Open_Wood_CC0.mp3"));
        SetField(drawer, "closeSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + "Door_Shut_Wood_CC0.mp3"));
        return drawer;
    }

    // The middle of a drawer, a little up from its centre.
    private static Vector3 StashPoint(Bounds drawer) => drawer.center + Vector3.up * (drawer.extents.y * 0.15f);

    // A copy of the cabinet mesh with the baked drawer fronts (panel, lip and knob, all in the front 10 cm of each
    // drawer's outline) cut away at `offsets` from the real drawer, and a copy of the real drawer's hollow (every face
    // inside its outline: the sides, top, bottom and back) behind each. Null if the mesh cannot be read.
    private static Mesh CutDrawerFronts(Mesh source, Bounds slot, List<Vector3> offsets)
    {
        const float edge = 0.004f;        // slack round a drawer's outline
        const float frontDepth = 0.1f;
        Vector3[] v = source.vertices;
        if (v == null || v.Length == 0)
        {
            Debug.LogWarning("Ship greybox: could not read the dresser mesh; only its one real drawer opens.");
            return null;
        }
        Vector3[] n = source.normals;
        Vector4[] tan = source.tangents;
        Vector2[] uv = source.uv;
        Vector2[] uv2 = source.uv2;
        bool hasN = n != null && n.Length == v.Length;
        bool hasT = tan != null && tan.Length == v.Length;
        bool hasUv = uv != null && uv.Length == v.Length;
        bool hasUv2 = uv2 != null && uv2.Length == v.Length;
        var verts = new List<Vector3>(v);
        var normals = hasN ? new List<Vector3>(n) : null;
        var tangents = hasT ? new List<Vector4>(tan) : null;
        var uvs = hasUv ? new List<Vector2>(uv) : null;
        var uv2s = hasUv2 ? new List<Vector2>(uv2) : null;

        bool Inside(Vector3 p, Vector3 offset) =>
            Mathf.Abs(p.x - (slot.center.x + offset.x)) <= slot.extents.x + edge && Mathf.Abs(p.y - (slot.center.y + offset.y)) <= slot.extents.y + edge;

        var submeshes = new List<List<int>>();
        for (int s = 0; s < source.subMeshCount; s++)
        {
            int[] tris = source.GetTriangles(s);
            var kept = new List<int>();
            var hollow = new List<int>();
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector3 a = v[tris[i]], b = v[tris[i + 1]], c = v[tris[i + 2]];
                if (Inside(a, Vector3.zero) && Inside(b, Vector3.zero) && Inside(c, Vector3.zero))
                    hollow.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                bool front = false;
                if (Mathf.Min(a.z, b.z, c.z) >= slot.max.z - frontDepth)
                    foreach (Vector3 offset in offsets)
                        if (Inside(a, offset) && Inside(b, offset) && Inside(c, offset))
                        {
                            front = true;
                            break;
                        }
                if (!front)
                    kept.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
            }
            foreach (Vector3 offset in offsets)
            {
                foreach (int index in hollow)
                {
                    kept.Add(verts.Count);
                    verts.Add(v[index] + offset);
                    normals?.Add(n[index]);
                    tangents?.Add(tan[index]);
                    uvs?.Add(uv[index]);
                    uv2s?.Add(uv2[index]);
                }
            }
            submeshes.Add(kept);
        }

        var mesh = new Mesh { name = source.name + " (six drawers)" };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        if (normals != null)
            mesh.SetNormals(normals);
        if (tangents != null)
            mesh.SetTangents(tangents);
        if (uvs != null)
            mesh.SetUVs(0, uvs);
        if (uv2s != null)
            mesh.SetUVs(1, uv2s);
        mesh.subMeshCount = submeshes.Count;
        for (int s = 0; s < submeshes.Count; s++)
            mesh.SetTriangles(submeshes[s], s);
        mesh.RecalculateBounds();
        return mesh;
    }

    // An axis-aligned box round `bounds` after a transform.
    private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
    {
        var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            result.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }
        return result;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    private static Door Drawer(string name, Vector3 position, Vector3 slideOut, Transform parent)
    {
        Box("Cabinet", position + new Vector3(0f, 0.45f, 0f), new Vector3(1.2f, 0.9f, 0.7f), Wood * 0.9f, parent);

        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.6f, 0f);
        trigger.size = new Vector3(1.4f, 1f, 1.2f);
        var door = root.AddComponent<Door>();
        root.AddComponent<InteractableHighlight>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.6f, 0f) + slideOut * 0.1f;
        visual.transform.localScale = new Vector3(1.1f, 0.35f, 0.9f);
        visual.AddComponent<RendererTint>().Tint = Wood;

        const string sounds = "Assets/Sound/Doors/";
        var so = new SerializedObject(door);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("motion").enumValueIndex = (int)Door.Motion.Slide;
        so.FindProperty("slideOffset").vector3Value = slideOut.normalized * 0.55f;
        so.FindProperty("duration").floatValue = 0.5f;
        so.FindProperty("openPrompt").stringValue = "open drawer";
        so.FindProperty("closePrompt").stringValue = "close drawer";
        so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + "Door_Open_Wood_CC0.mp3");
        so.FindProperty("closeStopSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(sounds + "Door_Shut_Wood_CC0.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();
        return door;
    }

    // A clump of low-poly seaweed, set dressing only: it sways and parts around the player.
    private static void SeaweedClump(Vector3 position, Seaweed.Kind kind, int seed, Transform parent)
    {
        var seaweed = new GameObject("Seaweed");
        seaweed.transform.SetParent(parent, false);
        seaweed.transform.position = position;
        AddSeaweed(seaweed, kind, seed);
    }

    // The seaweed that ties three bone fragments into the bone key (as in the arena): the clump, the socket on a
    // trigger you can swim through, and the tying minigame; it swoops when the key is tied.
    private static void SeaweedSocket(Vector3 position, Transform parent)
    {
        var seaweed = new GameObject("Seaweed_BoneKey");
        seaweed.transform.SetParent(parent, false);
        seaweed.transform.position = position;
        var collider = seaweed.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.7f, 0f);
        collider.size = new Vector3(1f, 3.4f, 1f);
        collider.isTrigger = true;
        Seaweed weed = AddSeaweed(seaweed, Seaweed.Kind.Kelp, 7);
        ItemSocket socket = Socket(seaweed, "Item_BoneKeyFragment", 3, true, "tie", null, null);   // the knot minigame gives the key
        AddKnotMinigame(seaweed, socket, weed);
    }

    // A bone carcass on the floor: a spine `length` long (before `scale`) with a pair of ribs every 0.8 m along most
    // of it, the biggest at the head end (-x before the turn) shrinking toward the tail, turned by yaw.
    private static void Carcass(Vector3 position, float yaw, Transform parent, float scale = 1f, float length = 5f)
    {
        var carcass = new GameObject("BoneCarcass");
        carcass.transform.SetParent(parent, false);
        carcass.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Decor("Spine", position + Vector3.up * 0.3f, new Vector3(length, 0.3f, 0.3f), Bone, parent).transform.SetParent(carcass.transform, true);
        float span = length * 0.8f;
        int ribs = Mathf.Max(3, Mathf.RoundToInt(span / 0.8f) + 1);
        for (int i = 0; i < ribs; i++)
        {
            float x = -span * 0.5f + i * span / (ribs - 1);
            float size = Mathf.Lerp(1f, 0.55f, i / (ribs - 1f));
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject rib = Decor("Rib", position + new Vector3(x, 0.9f * size, side * 0.7f * size), new Vector3(0.2f, 1.8f * size, 0.2f), Bone, parent);
                rib.transform.rotation = Quaternion.Euler(side * 35f, 0f, 0f);
                rib.transform.SetParent(carcass.transform, true);
            }
        }
        foreach (Transform child in carcass.transform)
            child.RotateAround(position, Vector3.up, yaw);
        carcass.transform.localScale = Vector3.one * scale;
    }
}
