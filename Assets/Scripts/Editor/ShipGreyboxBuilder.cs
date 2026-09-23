using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static TestArenaBuilder;

// Tools > Out of the Depths > Rebuild Ship Greybox (Main_Scene). Clears the level content in Main_Scene (the ROOMS and
// Placeholder groups and the loose doors / fish / plates at the root) and rebuilds the ship from the GDD floor plans at
// 1 map pixel = 5 cm: the nine first-floor rooms, the basement under the hatches, every door, pickup, socket, fish pack
// and the chase, wired in the GDD's lock-and-key order. Player, HUD, lighting, managers and ambience are kept.
// Safe to run again any time; everything it makes lives under SHIP. All numbers are metres (x east, z north).
//
// Route: 1 spawn (cyan) -> one-way door east into 2 the middle room (red) -> west into 3 the fish room (orange), hatch
// down to 4 the basement -> up the second hatch into 5 the chest room (green, no other way in or out) -> back through
// the basement -> 6 the stone room (blue) east of the middle room, pedestal door south into 7 the box room (yellow, the
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

    // The two hatches through the deck: room 3 down to the basement, and the basement up into room 5.
    private static readonly Rect HatchFish = new Rect(-27f, 40.25f, 2f, 2f);
    private static readonly Rect HatchChest = new Rect(7f, 40f, 2f, 2f);

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

    // Floor slabs: the basement footprint (also the basement ceiling) with the two hatch holes, the northern strip
    // under the hallway, symbol room, chest room and the top of the column, and the basement floor.
    private static void BuildDecks()
    {
        Transform decks = Group("Decks");
        SlabWithHoles("Deck", HullW, HullS, HullE, BasementN, 0f, new[] { HatchFish, HatchChest }, decks);
        Slab("Deck_N", HullW, BasementN, HullE, HullN, 0f, decks);
        Slab("BasementFloor", HullW, HullS, HullE, BasementN, BasementFloor, decks);
    }

    // The whole first-floor roof as one slab, so hiding it in the editor is one click.
    private static void BuildRoof()
    {
        Box("Roof", new Vector3((HullW + HullE) * 0.5f, Ceiling + SlabT * 0.5f, (HullS + HullN) * 0.5f), new Vector3(HullE - HullW, SlabT, HullN - HullS), Hull, ship);
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
        foreach (float z in new[] { 10.75f, 21.5f, 25f })                     // room 1, west hull
            Window(windowPrefab, new Vector3(HullW + WallT * 0.5f, 3f, z), 90f, windows);
        foreach (float x in new[] { -20.25f, -13f, -1f, 4f, 9f, 22f })          // north hull: hallway, room 9b, column
            Window(windowPrefab, new Vector3(x, 3f, HullN - WallT * 0.5f), 180f, windows);
        foreach (float z in new[] { 50.5f, 37f, 21f, 8.75f })                   // column, east hull
            Window(windowPrefab, new Vector3(HullE - WallT * 0.5f, 3f, z), -90f, windows);
        foreach (float z in new[] { 37f, 23.5f, 8.75f })                        // basement, east hull
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

        // The key, in a drawer in the south-east corner: E slides the drawer out and the key appears in it.
        GameObject key = CreatePickup(new Vector3(-13.4f, 0.75f, 10.3f), "Item_FirstRoomKey", room);
        key.SetActive(false);
        Door drawer = Drawer("Drawer_Key", new Vector3(-13.2f, 0f, 10.3f), Vector3.left, room);
        UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(drawer.onOpened, key.SetActive, true);

        // The one-way door: unlocked by the key (lock box beside it), shuts and locks behind you.
        Door oneWay = DoorZ("Door_Room1_Exit", -12.25f, 24f, true, true, room);
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
        WallXBig("R2_N", 38.25f, -12.25f, 19.75f, room, -5.05f, -5.05f + DoubleDoorGap);   // rooms 3, 8, 5 above: only the symbol room's big double door
        DoubleDoor toSymbol = SpawnDoubleDoor("Door_SymbolRoom", new Vector3(-2.25f, 0f, 38.25f), 0f, true, room);
        WallZ("R2_E", 7f, 19.75f, 38.25f, room, 29f, 29f + DoorGap);               // room 6 east (and the stub down to its south wall)
        DoorZ("Door_StoneRoom", 7f, 29f, false, false, room);
        DoorZ("Door_FishRoom", -12.25f, 30f, false, false, room);                  // in room 1's east wall, north of room 1

        foreach (Vector3 corner in new[] { new Vector3(-11f, 0f, 21f), new Vector3(5.75f, 0f, 21f), new Vector3(-11f, 0f, 37f), new Vector3(5.75f, 0f, 37f) })
            Box("Pillar", corner + Vector3.up * (Ceiling * 0.5f), new Vector3(1.5f, Ceiling, 1.5f), Prop, room);
        Decor("Symbol2_Floor", new Vector3(-2.5f, 0.02f, 29.25f), new Vector3(2.4f, 0.02f, 2.4f), new Color(0.15f, 0.2f, 0.9f), room);
        CreatePickup(new Vector3(-6.75f, 0.8f, 22f), "Item_StoneFragment", room);

        // The bone key goes into the lock plate in the middle of the double door.
        OpenOnFilled(Socket(toSymbol.LockPlate, "Item_BoneKey", 1, true, "unlock with", null, null), toSymbol);

        SeaweedSocket(new Vector3(5.2f, 0f, 36.4f), room);
    }

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

        // The dagger, just inside from the middle room.
        CreatePickup(new Vector3(-15f, 0.8f, 31f), "Item_Dagger", room);

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

    // Room 4: the basement, one open cellar under everything (x -29..29, z 0..44.25, floor at -6, no walls inside; the
    // strip north of it is the plan's hatched empty space) with a mural down the west wall, seaweed, bone carcasses,
    // fish, the symbol key, a stone fragment and a bone fragment, and the second hatch up into room 5.
    private static void BuildBasement()
    {
        Transform room = Group("Room_4_Basement");
        Deck(room, HullW, HullS, HullE, BasementN, BasementFloor, new Color(0.22f, 0.24f, 0.2f));

        // No walls inside: one open hull, as the plan draws it, with the mural strip down the west wall from the hatch.
        MuralAt(new Vector3(HullW + 0.3f, -3.3f, 31f), new Vector2(26f, 1.6f), Vector3.right, room);

        // Seven clumps of four kinds, each with its own layout.
        Seaweed.Kind[] kinds = { Seaweed.Kind.Kelp, Seaweed.Kind.SeaGrass, Seaweed.Kind.BroadLeaf, Seaweed.Kind.Ribbon };
        Vector3[] spots = { new Vector3(-22f, 0f, 36f), new Vector3(-14f, 0f, 40f), new Vector3(12f, 0f, 36f), new Vector3(20f, 0f, 30f), new Vector3(-8f, 0f, 22f), new Vector3(24f, 0f, 8f), new Vector3(-24f, 0f, 12f) };
        for (int i = 0; i < spots.Length; i++)
            SeaweedClump(spots[i] + Vector3.up * BasementFloor, kinds[i % kinds.Length], 20 + i, room);
        Carcass(new Vector3(-12f, BasementFloor, 8f), 20f, room);
        Carcass(new Vector3(16f, BasementFloor, 20f), -50f, room);
        Carcass(new Vector3(0f, BasementFloor, 40f), 90f, room);

        CreatePickup(new Vector3(-17.5f, BasementFloor + 0.8f, 8.25f), "Item_SymbolKey", room);
        CreatePickup(new Vector3(16.5f, BasementFloor + 0.8f, 28.75f), "Item_StoneFragment", room);
        CreatePickup(new Vector3(-4f, BasementFloor + 0.8f, 3f), "Item_BoneKeyFragment", room);
        CreatePickup(new Vector3(10f, BasementFloor + 0.8f, 15f), "Item_Pearl", room);
        CreatePickup(new Vector3(-24f, BasementFloor + 0.8f, 30f), "Item_Pearl", room);
        CreatePickup(new Vector3(22f, BasementFloor + 0.8f, 38f), "Item_Shell", room);

        CreateSchool("Fish_Wanderer", 3, new Vector3(-10f, BasementFloor + 2.5f, 25f), room);
        CreateSchool("Fish_Wanderer", 3, new Vector3(15f, BasementFloor + 2.5f, 12f), room);
        Spawn("DeadFish", new Vector3(0f, BasementFloor + 1f, 30f), room, 10f);
        Spawn("DeadFish", new Vector3(-20f, BasementFloor + 1f, 15f), room, -80f);
        GameObject plate = Spawn("RespawnPlate", new Vector3(-22.5f, BasementFloor + 0.05f, 43.2f), room);   // by the north wall, beside where the hatch drops you, not under it
        if (plate != null)
            plate.name = "Checkpoint_Basement";
    }

    // Room 5 (green): the chest room (x 2.5..19.75, z 38.25..50). No doors: the only way in and out is the hatch in its
    // floor from the basement. The chest opens with the symbol key and holds a stone fragment and a bone fragment.
    private static void BuildChestRoom()
    {
        Transform room = Group("Room_5_ChestRoom");
        Deck(room, 2.5f, 38.25f, 19.75f, 50f, 0f, Room5Green, HatchChest);
        WallZ("R8_E", 2.5f, 38.25f, 50f, room);   // shared with the symbol room

        // The hatch up from the basement.
        SpawnTrapdoor("Hatch_ChestRoom", new Vector3(HatchChest.xMin, 0f, HatchChest.yMin), room);

        GameObject chest = Box("Chest", new Vector3(16f, 0.5f, 44f), new Vector3(1.8f, 1f, 1.1f), Wood, room);
        Decor("ChestLid", new Vector3(16f, 1.06f, 44f), new Vector3(1.9f, 0.12f, 1.2f), Wood * 0.8f, room);
        GameObject stone = CreatePickup(new Vector3(15.5f, 1.4f, 44f), "Item_StoneFragment", room);
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

        GameObject pedestal = Box("Pedestal", new Vector3(13.5f, 0.6f, 34.25f), new Vector3(1.2f, 1.2f, 1.2f), Prop, room);
        PuzzleStation tablet = PuzzleBuildTools.AddStation(pedestal, "Puzzle_StoneTablet", "Item_StoneFragment", 3, true, "piece the tablet together");
        PuzzleBuildTools.SolveOpens(tablet, toBoxRoom);
        MuralAt(new Vector3(19.45f, 2.4f, 30f), new Vector2(8f, 1.6f), Vector3.left, room);
    }

    // Room 7 (yellow): the box room, the whole south strip (x -12.25..19.75, z 8.5..21.5) up against room 1's wall.
    // Push the two crates (Pushable Box: E puts your hands on one, W pushes and S pulls it along the room) onto the two
    // pressure plates; with both plates down the closet in the south-east corner opens. The closet holds the last
    // bone fragment.
    private static void BuildBoxRoom()
    {
        Transform room = Group("Room_7_BoxRoom");
        Deck(room, -12.25f, 8.5f, 7f, 19.75f, 0f, Room7Yellow);    // west of the stone room: up to the middle room's wall
        Deck(room, 7f, 8.5f, 19.75f, 21.5f, 0f, Room7Yellow);      // east: up to the stone room's wall

        // The closet: x 15..19.75, z 8.5..12.5, its door in its north wall.
        WallZ("Closet_W", 15f, 8.5f, 12.5f, room);
        WallX("Closet_N", 12.5f, 15f, 19.75f, room, 16f, 16f + DoorGap);
        Door closet = DoorX("Door_Closet", 12.5f, 16f, true, false, room);
        Box("OpenChest", new Vector3(17.4f, 0.4f, 10f), new Vector3(1.4f, 0.8f, 0.9f), Wood, room);
        PickupVariant(CreatePickup(new Vector3(17.4f, 1.1f, 10f), "Item_BoneKeyFragment", room), 2);

        // Two plates, each needing the other; the crates start 7 m west of them, in line, so a straight push east lands
        // each one on its plate.
        var plates = new PressurePlate[2];
        float[] rows = { 11f, 15f };
        for (int i = 0; i < 2; i++)
        {
            GameObject plate = Decor("PressurePlate", new Vector3(9f, 0.04f, rows[i]), new Vector3(1.6f, 0.08f, 1.6f), new Color(0.9f, 0.75f, 0.2f), room);
            plates[i] = plate.AddComponent<PressurePlate>();
            SetField(plates[i], "pressSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Doors/Door_MetalClunk_Grinnell_CC0.mp3"));
        }
        for (int i = 0; i < 2; i++)
        {
            PressurePlate other = plates[1 - i];
            SetField(plates[i], "alsoNeeds", p =>
            {
                p.arraySize = 1;
                p.GetArrayElementAtIndex(0).objectReferenceValue = other;
            });
        }
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(plates[0].onAllPressed, closet.Open);
        foreach (float z in rows)
        {
            GameObject crate = Box("PushBox", new Vector3(2f, 0.6f, z), new Vector3(1.2f, 1.2f, 1.2f), Wood, room);
            crate.AddComponent<InteractableHighlight>();   // lights up when you look at it: E puts your hands on it
            var push = crate.AddComponent<PushableBox>();
            SetField(push, "slideSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Doors/Door_MetalScrapeLoop_Toddcircle_CC0.mp3"));
        }
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
    // into. The pack waits in a vent at the hallway's far west end; the chase starts when the rune door opens. Two
    // stone fragments in the hallway go into the tablet by its east end, which unlocks the door east into the column.
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
        // The rune door arms it; the cutscene plays once the player is in the hallway (by the divider doorway or the
        // nook door), where the vent can be seen down its length.
        var start = new GameObject("ChaseStart");
        start.transform.SetParent(room, false);
        // 1.5 m in from the divider and the nook door, so it starts once the player is through, not in the doorway.
        start.transform.position = new Vector3((HullW + 0.25f - 6.25f) * 0.5f, Ceiling * 0.5f, (51.5f + HullN - 0.25f) * 0.5f);
        var startBox = start.AddComponent<BoxCollider>();
        startBox.isTrigger = true;
        startBox.size = new Vector3(-6.25f - HullW - 0.25f, Ceiling, HullN - 0.25f - 51.5f);
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

        // Under pressure: two stone fragments, high and low, for the tablet by the hallway's east end.
        CreatePickup(new Vector3(-20f, 4.2f, 52.5f), "Item_StoneFragment", room);
        CreatePickup(new Vector3(-12f, 0.6f, 56f), "Item_StoneFragment", room);
        GameObject tablet = Box("RuneTablet", new Vector3(-7f, 1.8f, HullN - 0.35f), new Vector3(1.1f, 1.3f, 0.2f), Stone, room);
        var slotted = new GameObject[2];
        for (int i = 0; i < slotted.Length; i++)
        {
            slotted[i] = Box("Placed_Stone_" + (i + 1), new Vector3(-7.3f + i * 0.6f, 1.8f, HullN - 0.53f), new Vector3(0.3f, 0.35f, 0.16f), new Color(0.4f, 0.8f, 0.85f), room);
            slotted[i].SetActive(false);
        }
        hallwayTablet = Socket(tablet, "Item_StoneFragment", 2, true, "slot", null, slotted);   // the column door it opens is made by BuildColumn

        CreateSchool("Fish_Wanderer", 3, new Vector3(8f, 2.5f, 54f), room);
    }

    // The column down the east side (x 19.75..29): the trident corridor north of the divider, the rubble that seals it,
    // the trident on its pedestal, then the exit corridor south with the first trident fights and the one-way exit.
    private static void BuildColumn()
    {
        Transform room = Group("Column_Trident");
        Deck(room, 19.75f, HullS, HullE, HullN, 0f, new Color(0.28f, 0.3f, 0.3f));
        WallZ("Column_W", 19.75f, HullS, HullN, room, 51f, 51f + DoorGap);
        columnDoor = DoorZ("Door_Column", 19.75f, 51f, true, false, room);
        if (hallwayTablet != null)
            OpenOnFilled(hallwayTablet, columnDoor);
        WallX("Column_Div", 23.75f, 19.75f, HullE, room, 23f, 23f + DoorGap);
        DoorX("Door_ColumnDivider", 23.75f, 23f, false, false, room);
        WallX("Column_ExitWall", 3f, 19.75f, HullE, room, 22.5f, 22.5f + DoorGap);
        Door exit = SpawnDoor("Door_Exit", new Vector3(22.5f + 2.4f, 0f, 3f), false, true, room, 180f);
        SetField(exit, "openPrompt", p => p.stringValue = "open the way out");

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
        GameObject trident = CreatePickup(new Vector3(24f, 1.7f, 28.75f), "Item_Trident", room);
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

    // A dim cool light in the middle of every room and along the basement, so the inside of the ship has shape
    // when the roof keeps the sun out. Point lights without shadows (cheap), a little greener below deck.
    private static void BuildLights()
    {
        Transform lights = Group("Lights");
        var deck = new Color(0.55f, 0.8f, 1f);
        var below = new Color(0.45f, 0.75f, 0.62f);
        foreach (var (name, x, z) in new[]
        {
            ("Room1", -20.6f, 18.6f), ("Room2", -2.6f, 29f), ("Room3", -20.6f, 33.5f), ("Nook", -18f, 47f),
            ("Room5", 11f, 44f), ("Room6", 13.4f, 30f), ("Room7", 3.75f, 15f), ("Room8", -2.25f, 44f),
            ("Hallway_W", -17f, 54.25f), ("Hallway_E", 7.5f, 54.25f), ("Column_S", 24.4f, 15f), ("Column_N", 24.4f, 45f),
        })
            RoomLight("Light_" + name, new Vector3(x, Ceiling - 1.9f, z), deck, 1.3f, 22f, lights);
        foreach (var (name, x, z) in new[] { ("Basement_SW", -15f, 12f), ("Basement_SE", 15f, 12f), ("Basement_NW", -15f, 32f), ("Basement_NE", 15f, 32f) })
            RoomLight("Light_" + name, new Vector3(x, BasementCeiling - 2.4f, z), below, 1.2f, 20f, lights);
    }

    private static void RoomLight(string name, Vector3 position, Color color, float intensity, float range, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
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

    // A bone carcass on the floor: a spine with ribs, turned by yaw.
    private static void Carcass(Vector3 position, float yaw, Transform parent)
    {
        var carcass = new GameObject("BoneCarcass");
        carcass.transform.SetParent(parent, false);
        carcass.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        Decor("Spine", position + Vector3.up * 0.3f, new Vector3(5f, 0.3f, 0.3f), Bone, parent).transform.SetParent(carcass.transform, true);
        for (int i = 0; i < 6; i++)
        {
            float x = -2f + i * 0.8f;
            foreach (float side in new[] { -1f, 1f })
            {
                GameObject rib = Decor("Rib", position + new Vector3(x, 0.9f, side * 0.7f), new Vector3(0.2f, 1.8f, 0.2f), Bone, parent);
                rib.transform.rotation = Quaternion.Euler(side * 35f, 0f, 0f);
                rib.transform.SetParent(carcass.transform, true);
            }
        }
        foreach (Transform child in carcass.transform)
            child.RotateAround(position, Vector3.up, yaw);
    }
}
