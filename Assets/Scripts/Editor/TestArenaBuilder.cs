using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static LightingTools;

// Tools > Out of the Depths > Rebuild Test Arena. Keeps Player, HUD, lights and the admin panel in TestArena.unity and
// replaces the arena itself with labelled test zones built from the placeholder prefabs. Safe to run again any time.
public static class TestArenaBuilder
{
    private const string ScenePath = "Assets/Scenes/TestArena.unity";
    private const string PrefabRoot = InteractablePrefabTools.PrefabRoot;
    private const string DoorPrefabPath = PrefabRoot + "/Placeholders/Door_Placeholder.prefab";
    private const string TrapdoorPrefabPath = PrefabRoot + "/Placeholders/Trapdoor_Placeholder.prefab";
    private const string DoubleDoorPrefabPath = PrefabRoot + "/Placeholders/DoubleDoor_Placeholder.prefab";
    internal const float DoubleDoorGap = 5.6f;    // two 2.4 m leaves + two 0.4 m frame posts
    internal const float DoubleDoorTop = 4.4f;    // 4 m leaves + the 0.4 m top of the frame
    private const string FishWindowPrefabPath = PrefabRoot + "/Placeholders/FishWindow_Placeholder.prefab";
    private const string SfxFolder = "Assets/Sound/SFX Sound effects/";
    private const string DoorSoundFolder = "Assets/Sound/Doors/";
    private const string PhotoTexturePath = "Assets/Art/Textures/DogPhoto.jpg";
    private const string PhotoMaterialPath = "Assets/Art/Materials/DogPhoto.mat";

    private static readonly Vector3 SpawnPosition = new Vector3(0f, 1.6f, -22f);

    // Fish windows: framed openings through the north wall onto open water, like looking out of the ship.
    private static readonly float[] WindowXs = { -24f, -14f };
    private const float WindowY = 3f;
    private const float WindowWidth = 2.6f;
    private const float WindowHeight = 1.6f;

    // Root objects whose names start with one of these are arena content and get rebuilt. Everything else is kept.
    private static readonly string[] ArenaPrefixes =
        { "Arena", "Floor", "Wall_", "Checkpoint_", "Hazard", "Fish", "DeadFish", "WallFish", "Pufferfish", "Pickup" };

    private static readonly Color TileA = new Color(0.16f, 0.24f, 0.30f);
    private static readonly Color TileB = new Color(0.20f, 0.29f, 0.36f);
    private static readonly Color WallColor = new Color(0.12f, 0.15f, 0.20f);
    private static readonly Color PropColor = new Color(0.36f, 0.40f, 0.46f);
    private static readonly Color LabelColor = new Color(0.9f, 0.97f, 1f);
    private static readonly Color SignAccent = new Color(0.35f, 0.85f, 0.95f);

    private static Transform arenaRoot;
    private static Font labelFont;
    private static Vector3 labelFocus = SpawnPosition;
    private static string buildName = "TestArena";

    // Lets another builder (the ship greybox) use these helpers: everything goes under root, signs face signFocus,
    // and console messages carry its name.
    internal static void BeginBuild(Transform root, Vector3 signFocus, string name)
    {
        arenaRoot = root;
        labelFont = GameFont.Font;
        labelFocus = signFocus;
        buildName = name;
        failedSteps = 0;
    }

    internal static int FailedSteps => failedSteps;

    [MenuItem("Tools/Out of the Depths/Rebuild Test Arena")]
    private static void Rebuild()
    {
        bool go = EditorUtility.DisplayDialog("Rebuild Test Arena",
            "This replaces the arena in TestArena.unity (floor, walls, fish, hazards, checkpoints, pickups).\n" +
            "Player, HUD, lights and the admin panel are kept.\n\nThe scene is saved when done.",
            "Rebuild", "Cancel");
        if (!go || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        InteractablePrefabTools.AddIndicatorsToInteractablePrefabs();
        ItemTools.EnsureItemDefinitions();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        failedSteps = 0;
        // Work done by hand inside the old arena (the box puzzle) is lifted out first and put back at the end.
        GameObject oldArena = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Arena")
                oldArena = root;
        HandMadeKeeper.Kept handMade = HandMadeKeeper.Lift(oldArena != null ? oldArena.transform : null);
        RemoveOldArena(scene);
        RemoveStrayPlaceholders(scene);

        BeginBuild(new GameObject("Arena").transform, SpawnPosition, "TestArena");

        // Each step on its own, so one failure is reported in the Console and the rest of the arena still gets built.
        Step("Checkpoint model", CheckpointModelTools.Apply);
        Step("Floor and walls", BuildFloorAndWalls);
        Step("Spawn", BuildSpawn);
        Step("Mascot", BuildMascot);
        Step("Movement course", BuildMovementCourse);
        Step("Fish and food", BuildFishAndFood);
        Step("Combat pen", BuildCombatPen);
        Step("Hazard lane", BuildHazardLane);
        Step("Pickups", BuildPickups);
        Step("Puzzles", BuildPuzzles);
        Step("Doors", BuildDoors);
        Step("Hatch", BuildHatch);
        Step("Chase corridor", BuildChaseCorridor);
        Step("Item models", ItemModelTools.ApplyItemModelsInOpenScene);
        Step("Player", PlacePlayer);
        Step("Inventory HUD", ItemTools.EnsureInventoryHud);
        Step("Chase HUD", ChaseTools.EnsureDangerHud);
        Step("Admin panel", WireAdminPanel);
        Step("Pause menu theme", PauseMenuTools.EnsureTheme);
        Step("Pause menu pages", PauseMenuTools.EnsurePages);
        Step("Game font", FontTools.ApplyToOpenSceneQuietly);
        Step("HUD layout", HudLayoutTools.Apply);
        Step("Warning thresholds", TuneWarningThresholds);
        Step("Underwater look", UnderwaterTools.ApplyToOpenScene);
        Step("Lighting", BuildLighting);
        Step("Hand-made work", () => HandMadeKeeper.PutBack(handMade, arenaRoot));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"TestArena rebuilt and saved: {ScenePath} ({failedSteps} step(s) failed - see errors above)".Replace(" (0 step(s) failed - see errors above)", ""));
    }

    private static int failedSteps;

    internal static void Step(string name, System.Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception e)
        {
            failedSteps++;
            Debug.LogError($"{buildName}: step '{name}' failed: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void RemoveOldArena(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (string prefix in ArenaPrefixes)
            {
                if (!root.name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                Object.DestroyImmediate(root);
                break;
            }
        }
    }

    // ---- zones -------------------------------------------------------------------------------------------------

    private static void BuildFloorAndWalls()
    {
        Transform floor = Group("Floor");
        for (int x = 0; x < 6; x++)
            for (int z = 0; z < 6; z++)
                Box($"Tile_{x}_{z}", new Vector3(-25f + x * 10f, -0.1f, -25f + z * 10f), new Vector3(10f, 0.2f, 10f),
                    (x + z) % 2 == 0 ? TileA : TileB, floor);

        // Dead fish drift up into this and fade away.
        var barrier = new GameObject("DeadFishBarrier");
        barrier.transform.SetParent(arenaRoot, false);
        barrier.transform.position = new Vector3(0f, 8.5f, 0f);
        var barrierBox = barrier.AddComponent<BoxCollider>();
        barrierBox.isTrigger = true;
        barrierBox.size = new Vector3(62f, 1f, 62f);
        barrier.AddComponent<DeadFishBarrier>();

        Transform walls = Group("Walls");
        // The north wall has a 2.8 m doorway at x 2..4 for the chase corridor (Door_Final and its frame fill it).
        Box("Wall_North_W", new Vector3(-14.7f, 5f, 30.5f), new Vector3(32.6f, 10f, 1f), WallColor, walls);
        Box("Wall_North_E", new Vector3(17.7f, 5f, 30.5f), new Vector3(26.6f, 10f, 1f), WallColor, walls);
        Box("Wall_North_Top", new Vector3(3f, 6.7f, 30.5f), new Vector3(2.8f, 6.6f, 1f), WallColor, walls);
        Box("Wall_South", new Vector3(0f, 5f, -30.5f), new Vector3(62f, 10f, 1f), WallColor, walls);
        Box("Wall_East", new Vector3(30.5f, 5f, 0f), new Vector3(1f, 10f, 62f), WallColor, walls);
        Box("Wall_West", new Vector3(-30.5f, 5f, 0f), new Vector3(1f, 10f, 62f), WallColor, walls);
    }

    private static void BuildSpawn()
    {
        Transform zone = Group("Zone_Spawn");
        Patch(zone, new Vector2(0f, -22f), new Vector2(10f, 10f), new Color(0.2f, 0.36f, 0.4f));
        Label("WELCOME - N: fish and ship windows. NE: combat pen. E: hazard lane. SE: pickup shelf. S/E: doors. Centre: puzzles. SW: hatch and basement. W: movement course. N door: the chase. Walk up to a sign to read it. Turn around to meet the QA lead.", new Vector3(-4f, 3.2f, -25f), zone);
        GameObject plate = Spawn("RespawnPlate", new Vector3(SpawnPosition.x + 3.5f, 0.05f, SpawnPosition.z - 3.5f), zone);   // behind and to the right of the spawn, off the way north
        if (plate != null)
        {
            plate.name = "Checkpoint_Spawn";
            var checkpoint = plate.GetComponentInChildren<Checkpoint>();
            if (checkpoint != null)
                SetField(checkpoint, "startsActivated", p => p.boolValue = true);
        }
        CreatePickup(new Vector3(2f, 1.2f, -19f), "Item_Dagger", zone);
        Label("SPAWN - take the dagger (E) to be able to slash\nadmin panel = 0", new Vector3(4.5f, 3.4f, -19f), zone);   // beside the dagger, not across the way north
    }


    // The team mascot: a framed photo on the south wall right behind the spawn pad (turn around), with a plaque and its
    // own spotlight. Photo: Art/Textures/DogPhoto.jpg - swap the file to change the picture.
    private static void BuildMascot()
    {
        Transform zone = Group("Zone_Mascot");
        Vector3 wall = new Vector3(0f, 3f, -30f);      // inner face of the south wall, north side
        const float width = 2.4f, height = 1.8f;       // the photo is 4:3

        GameObject frame = Box("Frame", wall + new Vector3(0f, 0f, 0.05f), new Vector3(width + 0.24f, height + 0.24f, 0.1f), new Color(0.32f, 0.22f, 0.12f), zone);

        // A Quad is seen from its -Z side, so it is turned to look north, toward the spawn pad.
        GameObject photo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        photo.name = "Photo";
        Object.DestroyImmediate(photo.GetComponent<Collider>());
        photo.transform.SetParent(zone, false);
        photo.transform.SetPositionAndRotation(wall + new Vector3(0f, 0f, 0.105f), Quaternion.Euler(0f, 180f, 0f));
        photo.transform.localScale = new Vector3(width, height, 1f);
        Material material = EnsurePhotoMaterial();
        if (material != null)
            photo.GetComponent<Renderer>().sharedMaterial = material;

        // Press E on the picture and the dog barks (Sound On Interact; the clips live in Sound/Props). The photo rides
        // on the frame so the whole thing rattles on its nail.
        photo.transform.SetParent(frame.transform, true);
        frame.AddComponent<InteractableHighlight>();
        var bark = frame.AddComponent<SoundOnInteract>();
        var barkSo = new SerializedObject(bark);
        barkSo.FindProperty("prompt").stringValue = "pet the dog";
        SerializedProperty barkClips = barkSo.FindProperty("clips");
        barkClips.arraySize = 2;
        barkClips.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Props/Prop_DogBark_Kwahmah_CC0.mp3");
        barkClips.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Props/Prop_DogBark_Sadiquecat_CC0.mp3");
        barkSo.ApplyModifiedPropertiesWithoutUndo();

        var plaque = new GameObject("Plaque");
        plaque.transform.SetParent(zone, false);
        plaque.transform.position = wall + new Vector3(0f, -(height * 0.5f + 0.45f), 0f);
        RimPiece(plaque, "Board", new Vector3(0f, 0f, 0.04f), new Vector3(1.9f, 0.5f, 0.08f), new Color(0.6f, 0.48f, 0.2f));
        SignFace(plaque, "Head of Quality Assurance", new[] { "(asleep on the job)" }, new Vector3(0f, 0f, 0.085f), 180f, 1.9f, 0.5f, 0.24f, 0.16f, 0.05f);

        var spot = new GameObject("Spotlight");
        spot.transform.SetParent(zone, false);
        spot.transform.position = wall + new Vector3(0f, 3.5f, 3f);
        spot.transform.LookAt(wall);
        var light = spot.AddComponent<Light>();
        light.type = LightType.Spot;
        light.spotAngle = 55f;
        light.range = 10f;
        light.intensity = 7f;
        light.color = new Color(1f, 0.95f, 0.85f);
    }

    // DogPhoto.mat: URP Lit with the photo as base map plus an emissive copy so it reads brightly in the dim water.
    private static Material EnsurePhotoMaterial()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoTexturePath);
        if (texture == null)
        {
            AssetDatabase.ImportAsset(PhotoTexturePath);
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PhotoTexturePath);
        }
        if (texture == null)
        {
            Debug.LogWarning($"TestArena: no picture at {PhotoTexturePath}, the mascot frame stays empty.");
            return null;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(PhotoMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, PhotoMaterialPath);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetTexture("_MainTex", texture);
        material.SetFloat("_Smoothness", 0.35f);
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_EmissionColor", Color.white * 0.9f);
        EditorUtility.SetDirty(material);
        return material;
    }
    private static void BuildMovementCourse()
    {
        Transform zone = Group("Zone_Movement");
        Patch(zone, new Vector2(-19f, -16f), new Vector2(20f, 26f), new Color(0.2f, 0.3f, 0.45f));

        // Slalom: five pillars, alternating sides.
        for (int i = 0; i < 5; i++)
            Box("Pillar", new Vector3(-16f + (i % 2 == 0 ? -2.5f : 2.5f), 3f, -27f + i * 4f), new Vector3(1.2f, 6f, 1.2f), PropColor, zone);

        // Low tunnel: 2.6 m of headroom, tests keeping depth in tight spaces.
        Box("Tunnel_Roof", new Vector3(-24f, 2.75f, -12f), new Vector3(6f, 0.3f, 8f), PropColor, zone);
        Box("Tunnel_WallW", new Vector3(-27.2f, 1.4f, -12f), new Vector3(0.4f, 2.8f, 8f), PropColor, zone);
        Box("Tunnel_WallE", new Vector3(-20.8f, 1.4f, -12f), new Vector3(0.4f, 2.8f, 8f), PropColor, zone);

        // Shaft: enter through the gap at the bottom of the south side, swim straight up and out the open top.
        Box("Shaft_N", new Vector3(-10f, 4.5f, -8.15f), new Vector3(3.7f, 9f, 0.3f), PropColor, zone);
        Box("Shaft_E", new Vector3(-8.15f, 4.5f, -10f), new Vector3(0.3f, 9f, 3.4f), PropColor, zone);
        Box("Shaft_W", new Vector3(-11.85f, 4.5f, -10f), new Vector3(0.3f, 9f, 3.4f), PropColor, zone);
        Box("Shaft_S", new Vector3(-10f, 6f, -11.85f), new Vector3(3.7f, 6f, 0.3f), PropColor, zone);

        // Ramp: checks the Character Controller slope limit against sloped level geometry.
        GameObject ramp = Box("Ramp", new Vector3(-24f, 1.2f, -24f), new Vector3(5f, 0.3f, 8f), PropColor, zone);
        ramp.transform.rotation = Quaternion.Euler(-25f, 0f, 0f);

        Label("MOVEMENT - slalom, tunnel, shaft, ramp", new Vector3(-17f, 7f, -14f), zone);
    }

    private static void BuildFishAndFood()
    {
        Transform zone = Group("Zone_Fish");
        Patch(zone, new Vector2(-18.5f, 19f), new Vector2(22f, 22f), new Color(0.2f, 0.38f, 0.32f));

        // A closed room (arena walls to the west and north) with one swing door on the south side.
        Box("FishRoom_E", new Vector3(-7f, 5f, 19f), new Vector3(0.5f, 10f, 22f), WallColor, zone);
        Box("FishRoom_S_W", new Vector3(-24.5f, 5f, 8f), new Vector3(11f, 10f, 0.5f), WallColor, zone);
        Box("FishRoom_S_E", new Vector3(-12f, 5f, 8f), new Vector3(10f, 10f, 0.5f), WallColor, zone);
        Box("FishRoom_S_Top", new Vector3(-18f, 6.5f, 8f), new Vector3(2f, 7f, 0.5f), WallColor, zone);
        Door roomDoor = SpawnDoor("Door_FishRoom", new Vector3(-19f, 0f, 8f), false, true, zone);
        SetField(roomDoor, "lockBehind", p => p.boolValue = false);

        FishSchool school = CreateSchool("Fish_Wanderer", 0, new Vector3(-19f, 2.5f, 20f), zone);
        BuildFishWindows(zone, school);
        Spawn("Fish_Wanderer", new Vector3(-24f, 1.5f, 12f), zone, 240f);
        Spawn("WallFish", new Vector3(-28.5f, 2f, 22f), zone, 90f);
        Label("FISH ROOM - open the door (it swings away from you and shuts behind you) and swim in: the pack comes in through the ship windows (pack size = Fish Spawner > Count). Leave and they swim back out.", new Vector3(-26f, 3.4f, 5f), zone);

        Transform food = Group("Zone_Food");
        Spawn("DeadFish", new Vector3(-10f, 1f, 10f), food, 0f);
        Spawn("DeadFish", new Vector3(-8f, 1f, 12.5f), food, 40f);
        GameObject respawning = Spawn("DeadFish", new Vector3(-12.5f, 1f, 8f), food, -30f);
        if (respawning != null)
        {
            respawning.name += "_Respawning";
            var edible = respawning.GetComponentInChildren<EdibleFish>();
            if (edible != null)
                SetField(edible, "respawnTime", p => p.floatValue = 12f);
        }
        Label("FOOD - E to eat (one respawns after 12 s)", new Vector3(-9.5f, 4.6f, 11.5f), food);
    }

    private static void BuildCombatPen()
    {
        Transform zone = Group("Zone_Combat");
        Patch(zone, new Vector2(18f, 19f), new Vector2(18f, 16f), new Color(0.4f, 0.25f, 0.25f));
        Box("Pen_W", new Vector3(10f, 2f, 19f), new Vector3(0.5f, 4f, 14f), PropColor, zone);
        Box("Pen_E", new Vector3(26f, 2f, 19f), new Vector3(0.5f, 4f, 14f), PropColor, zone);
        Box("Pen_N", new Vector3(18f, 2f, 26f), new Vector3(16.5f, 4f, 0.5f), PropColor, zone);
        Spawn("Pufferfish", new Vector3(14f, 2f, 20f), zone, 200f);
        Spawn("Pufferfish", new Vector3(22f, 2f, 18f), zone, 160f);
        NestAt("PufferNest_Combat", new Vector3(18f, 0f, 14f), Vector3.up, 2, zone);   // and a nest that keeps sending more
        Spawn("Fish_Wanderer", new Vector3(18f, 2f, 23f), zone);
        Label("COMBAT - left click to slash (needs the dagger)", new Vector3(18f, 5.5f, 25f), zone);
    }

    private static void BuildHazardLane()
    {
        Transform zone = Group("Zone_Hazards");
        Patch(zone, new Vector2(19f, -1f), new Vector2(22f, 11f), new Color(0.42f, 0.32f, 0.2f));
        Box("Lane_N", new Vector3(19f, 1.5f, 4.5f), new Vector3(22f, 3f, 0.4f), PropColor, zone);
        Box("Lane_S", new Vector3(19f, 1.5f, -6.5f), new Vector3(22f, 3f, 0.4f), PropColor, zone);
        Spawn("Hazard", new Vector3(12f, 1f, 1f), zone, 0f);
        Spawn("Hazard", new Vector3(17f, 1f, -3f), zone, 0f);
        Spawn("Hazard", new Vector3(22f, 1f, 1f), zone, 0f);

        GameObject plate = Spawn("RespawnPlate", new Vector3(28.3f, 0.05f, -5.2f), zone);   // in the far corner of the lane, off its middle
        if (plate != null)
            plate.name = "Checkpoint_Far";
        Label("HAZARDS - checkpoint at the end", new Vector3(19f, 4.5f, 5f), zone);
    }

    private static void BuildPickups()
    {
        Transform zone = Group("Zone_Pickups");
        Patch(zone, new Vector2(18f, -20f), new Vector2(22f, 8f), new Color(0.4f, 0.38f, 0.22f));
        Box("Shelf", new Vector3(18f, 0.5f, -20f), new Vector3(20f, 1f, 1.5f), PropColor, zone);
        string[] items =
        {
            "Item_StoneFragment", "Item_StoneFragment", "Item_StoneFragment",
            "Item_BoneKeyFragment", "Item_BoneKeyFragment", "Item_BoneKeyFragment",
            "Item_Pearl",
            "Item_FirstRoomKey", "Item_SymbolKey",
        };
        int bonePiece = 0;
        for (int i = 0; i < items.Length; i++)
        {
            GameObject pickup = CreatePickup(new Vector3(10f + i * 2f, 1.25f, -20f), items[i], zone);
            if (items[i] == "Item_BoneKeyFragment")
                PickupVariant(pickup, bonePiece++);   // the three pieces of the key, one each
        }
        Label("PICKUPS - E to take, 1-4 / wheel selects a slot", new Vector3(18f, 3.4f, -24f), zone);   // behind the shelf, not over the pickups
    }

    private static void BuildPuzzles()
    {
        Transform zone = Group("Zone_Puzzles");
        Patch(zone, new Vector2(0f, 9f), new Vector2(14f, 12f), new Color(0.33f, 0.25f, 0.42f));
        Color stone = new Color(0.7f, 0.65f, 0.5f);

        // A wall with three locked doors: the stone tablet opens the left one, the bone key the middle one, the
        // rune lock the right one. 1.4 m of wall between 2.8 m doorways, x -7..7.
        foreach (float x in new[] { -6.3f, -2.1f, 2.1f, 6.3f })
            Box("PuzzleWall", new Vector3(x, 2f, 14f), new Vector3(1.4f, 4f, 0.5f), PropColor, zone);
        Door doorStone = SpawnDoor("Door_Stone", new Vector3(-5.2f, 0f, 14f), true, false, zone);
        Door doorBone = SpawnDoor("Door_BoneKey", new Vector3(-1f, 0f, 14f), true, false, zone);
        Door doorRunes = SpawnDoor("Door_Runes", new Vector3(3.2f, 0f, 14f), true, false, zone);
        SetField(doorStone, "lockedHint", p => p.stringValue = "Locked. Piece the stone tablet together at the pedestal to open it.");
        SetField(doorBone, "lockedHint", p => p.stringValue = "Locked. It takes the bone key: tie the three bone fragments together at the seaweed.");
        SetField(doorRunes, "lockedHint", p => p.stringValue = "Locked. Enter the three symbols on the rune lock beside it.");

        // Pedestal: with the 3 stone fragments on you, E opens the puzzle board; piecing the tablet together opens the left door.
        GameObject pedestal = Box("Pedestal", new Vector3(0f, 0.6f, 6f), new Vector3(1.2f, 1.2f, 1.2f), PropColor, zone);
        PuzzleBuildTools.SolveOpens(PuzzleBuildTools.AddStation(pedestal, "Puzzle_StoneTablet", "Item_StoneFragment", 3, true, "piece the tablet together"), doorStone);
        PodiumModelTools.ApplyTo(pedestal, pedestal.transform.position + Vector3.back * 5f);   // the team's podium model, facing the way in

        // Seaweed: 3 bone key fragments + E = the tying minigame = a bone key (the GDD tie-them-together step). A low-poly
        // clump (the Seaweed component grows it) that sways, parts around the player and swoops when the key is tied;
        // the collider is a trigger, so you swim through it and the E prompt still finds it.
        var seaweed = new GameObject("Seaweed");
        seaweed.transform.SetParent(zone, false);
        seaweed.transform.position = new Vector3(-5f, 0f, 8f);
        var seaweedCollider = seaweed.AddComponent<BoxCollider>();
        seaweedCollider.center = new Vector3(0f, 1.7f, 0f);
        seaweedCollider.size = new Vector3(1f, 3.4f, 1f);
        seaweedCollider.isTrigger = true;
        Seaweed weed = AddSeaweed(seaweed, Seaweed.Kind.Kelp, 7);
        ItemSocket seaweedSocket = Socket(seaweed, "Item_BoneKeyFragment", 3, true, "tie", null, null);   // the knot minigame gives the key
        AddKnotMinigame(seaweed, seaweedSocket, weed);
        // A few more kinds round it, so the zone shows the variety: sea grass, broad leaves, ribbons.
        SeaweedAt(new Vector3(-7.5f, 0f, 10.5f), Seaweed.Kind.SeaGrass, 11, zone);
        SeaweedAt(new Vector3(-2.5f, 0f, 10.5f), Seaweed.Kind.BroadLeaf, 12, zone);
        SeaweedAt(new Vector3(-7.5f, 0f, 5.5f), Seaweed.Kind.Ribbon, 13, zone);

        // Lock: the bone key opens the middle door.
        GameObject lockBox = Box("Lock_BoneKey", new Vector3(2.1f, 1.2f, 13.4f), new Vector3(0.5f, 0.5f, 0.3f), new Color(0.8f, 0.7f, 0.3f), zone);
        OpenOnFilled(Socket(lockBox, "Item_BoneKey", 1, true, "unlock with", null, null), doorBone);

        // Rune lock: the symbol puzzle on the board opens the right door.
        GameObject runeLock = Box("RuneLock", new Vector3(6.3f, 1.6f, 13.4f), new Vector3(0.7f, 0.7f, 0.3f), new Color(0.8f, 0.7f, 0.3f), zone);
        PuzzleBuildTools.SolveOpens(PuzzleBuildTools.AddStation(runeLock, "Puzzle_Runes", null, 0, false, "enter the runes"), doorRunes);

        Label("PUZZLES - pedestal: the 3 stone fragments, then piece the tablet together on the board -> left door\nseaweed: 3 bone fragments -> bone key -> middle door\nrune lock: the three symbols in order (triangle, square, spiral) -> right door", new Vector3(0f, 6f, 12f), zone);
    }

    private static void BuildDoors()
    {
        Transform zone = Group("Zone_Doors");
        Patch(zone, new Vector2(9f, -12f), new Vector2(10f, 6f), new Color(0.35f, 0.28f, 0.22f));
        Door swing = SpawnDoor("Door_Swing", new Vector3(6f, 0f, -12f), false, false, zone);
        SetField(swing, "motion", p => p.enumValueIndex = (int)Door.Motion.Swing);
        SpawnDoor("Door_ClosesBehind", new Vector3(10f, 0f, -12f), false, true, zone);
        Label("DOORS - left: E opens / closes (swings)\nright: slides up, then shuts and locks once you're through", new Vector3(9f, 5.4f, -11f), zone);   // above the door tops
    }

    // A raised deck with a hatch in its roof and a "basement" inside (with a key to find), like the GDD's kellari.
    private static void BuildHatch()
    {
        Transform zone = Group("Zone_Hatch");
        Vector3 c = new Vector3(-4f, 0f, -13f);
        Patch(zone, new Vector2(c.x, c.z), new Vector2(8f, 8f), new Color(0.28f, 0.3f, 0.32f));
        Box("Deck_N", c + new Vector3(0f, 1.25f, 3f), new Vector3(6f, 2.5f, 0.3f), PropColor, zone);
        Box("Deck_S", c + new Vector3(0f, 1.25f, -3f), new Vector3(6f, 2.5f, 0.3f), PropColor, zone);
        Box("Deck_E", c + new Vector3(3f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 6f), PropColor, zone);
        Box("Deck_W", c + new Vector3(-3f, 1.25f, 0f), new Vector3(0.3f, 2.5f, 6f), PropColor, zone);
        // Roof with a 2 x 2 hole in the middle, covered by the hatch.
        Box("Roof_N", c + new Vector3(0f, 2.5f, 2f), new Vector3(6f, 0.3f, 2f), PropColor, zone);
        Box("Roof_S", c + new Vector3(0f, 2.5f, -2f), new Vector3(6f, 0.3f, 2f), PropColor, zone);
        Box("Roof_E", c + new Vector3(2f, 2.5f, 0f), new Vector3(2f, 0.3f, 2f), PropColor, zone);
        Box("Roof_W", c + new Vector3(-2f, 2.5f, 0f), new Vector3(2f, 0.3f, 2f), PropColor, zone);
        SpawnTrapdoor("Trapdoor", c + new Vector3(-1f, 2.5f, -1f), zone);
        CreatePickup(c + new Vector3(0f, 0.6f, 0f), "Item_SymbolKey", zone);
        Label("HATCH - E opens the trapdoor, the basement is below", c + new Vector3(0f, 5f, 0f), zone);
    }

    internal static Door SpawnTrapdoor(string name, Vector3 hinge, Transform parent)
    {
        GameObject prefab = EnsureTrapdoorPrefab();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(hinge, Quaternion.identity);
        instance.name = name;
        return instance.GetComponent<Door>();
    }

    // Root = hinge + doorway trigger + Door + highlight; child Visual = a 2 x 3 panel with its collider. Swap the Visual for real art.
    internal static GameObject EnsureDoorPrefab()
    {
        return EnsureDoorLikePrefab(DoorPrefabPath, "Door_Placeholder",
            new Vector3(1f, 1.5f, 0f), new Vector3(2.4f, 3.2f, 1.6f),
            new Vector3(1f, 1.5f, 0f), new Vector3(2f, 3f, 0.15f), new Color(0.45f, 0.3f, 0.2f), null);
    }

    // Same as the door but lying flat: hinge along the root's Z edge, lid swings up, "through" is down.
    private static GameObject EnsureTrapdoorPrefab()
    {
        return EnsureDoorLikePrefab(TrapdoorPrefabPath, "Trapdoor_Placeholder",
            new Vector3(1f, 0f, 1f), new Vector3(2.4f, 1.6f, 2.4f),
            new Vector3(1f, 0f, 1f), new Vector3(2f, 0.15f, 2f), new Color(0.4f, 0.28f, 0.18f), so =>
            {
                so.FindProperty("motion").enumValueIndex = (int)Door.Motion.Swing;
                so.FindProperty("swingAxis").vector3Value = Vector3.forward;
                so.FindProperty("swingAngle").floatValue = 100f;
                so.FindProperty("throughAxis").vector3Value = Vector3.down;
                so.FindProperty("openPrompt").stringValue = "open hatch";
                so.FindProperty("closePrompt").stringValue = "close hatch";
            });
    }

    // The big double door (the symbol room's). Root = the doorway: the DoubleDoor script, a trigger over the opening
    // and the highlight. Leaf_Left / Leaf_Right are the hinges at the outer edges, each with a Visual panel (2.4 x 4)
    // to swap for real art; Lock on the right leaf is the plate the key goes into (the builders put the socket on it).
    private static GameObject EnsureDoubleDoorPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoubleDoorPrefabPath);
        if (existing != null)
            return existing;

        var root = new GameObject("DoubleDoor_Placeholder");
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 2.2f, 0f);
        trigger.size = new Vector3(5.2f, 4.4f, 1.6f);
        var door = root.AddComponent<DoubleDoor>();
        root.AddComponent<InteractableHighlight>();

        Transform left = DoubleDoorLeaf(root.transform, "Leaf_Left", -2.4f, 1.2f);
        Transform right = DoubleDoorLeaf(root.transform, "Leaf_Right", 2.4f, -1.2f);

        GameObject lockPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lockPlate.name = "Lock";
        lockPlate.transform.SetParent(right, false);
        lockPlate.transform.localPosition = new Vector3(-2.1f, 1.4f, -0.13f);   // on the -Z face: the side you come from at yaw 0
        lockPlate.transform.localScale = new Vector3(0.5f, 0.6f, 0.12f);
        lockPlate.AddComponent<RendererTint>().Tint = new Color(0.8f, 0.7f, 0.3f);

        var so = new SerializedObject(door);
        so.FindProperty("leftLeaf").objectReferenceValue = left;
        so.FindProperty("rightLeaf").objectReferenceValue = right;
        so.FindProperty("lockPlate").objectReferenceValue = lockPlate;
        so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_DungeonBolt_Bennynz_CC0.mp3");
        so.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_HeavySlam_Kyles_CC0.mp3");
        so.FindProperty("lockedSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_LockedRattle_CastIronCarousel_CC0.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = SavePrefab(root, DoubleDoorPrefabPath);
        return prefab;
    }

    private static Transform DoubleDoorLeaf(Transform root, string name, float hingeX, float panelX)
    {
        var hinge = new GameObject(name).transform;
        hinge.SetParent(root, false);
        hinge.localPosition = new Vector3(hingeX, 0f, 0f);
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(hinge, false);
        visual.transform.localPosition = new Vector3(panelX, 2f, 0f);
        visual.transform.localScale = new Vector3(2.4f, 4f, 0.15f);
        visual.AddComponent<RendererTint>().Tint = new Color(0.4f, 0.26f, 0.16f);
        return hinge;
    }

    // The double door at the bottom centre of its opening, with its frame; yaw 0 = the leaves span X and you pass
    // through along Z. The wall wants a gap DoubleDoorGap wide with its lintel from DoubleDoorTop up.
    internal static DoubleDoor SpawnDoubleDoor(string name, Vector3 centre, float yaw, bool locked, Transform parent)
    {
        GameObject prefab = EnsureDoubleDoorPrefab();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, true);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.SetPositionAndRotation(centre, rotation);
        instance.name = name;
        var door = instance.GetComponent<DoubleDoor>();
        SetField(door, "locked", p => p.boolValue = locked);
        // Each leaf wears its half of the team's double door (Double_Doors), in place of the plain panel.
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Material doubleMaterial = AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialFolder + "M_double_doors.mat");
        foreach (var (leafName, part, panelX) in new[] { ("Leaf_Left", 0, 1.2f), ("Leaf_Right", 1, -1.2f) })
        {
            Transform leaf = instance.transform.Find(leafName);
            if (leaf == null)
                continue;
            Transform panel = leaf.Find("Visual");
            if (panel != null)
                Object.DestroyImmediate(panel.gameObject);
            Transform visual = FittedModel(leaf, "Visual", "Double_Doors", part, doubleMaterial, new Vector3(panelX, 2f, 0f), new Vector3(2.4f, 4f, 0.15f), new Color(0.4f, 0.26f, 0.16f));
            visual.SetAsFirstSibling();
        }
        FramePiece("Frame_Post", centre, rotation, new Vector3(-2.6f, 2.2f, 0f), new Vector3(0.4f, 4.4f, 0.5f), parent);
        FramePiece("Frame_Post", centre, rotation, new Vector3(2.6f, 2.2f, 0f), new Vector3(0.4f, 4.4f, 0.5f), parent);
        FramePiece("Frame_Top", centre, rotation, new Vector3(0f, 4.2f, 0f), new Vector3(5.6f, 0.4f, 0.5f), parent);
        // The double door's leaves have their top outer corners cut off (a slant DoubleDoorCut of the leaf across and
        // down): the frame fills those corners, so the doorway is the door's own shape and nothing shows round it.
        FrameCorner("Frame_Corner_L", centre, rotation, new Vector3(-2.4f, 4f, 0f), 1f, parent);
        FrameCorner("Frame_Corner_R", centre, rotation, new Vector3(2.4f, 4f, 0f), -1f, parent);
        return door;
    }

    // How much of each double-door leaf (2.4 x 4 m) the model's slanted top corner cuts away: 45 % across, 30 % down
    // (Double_Doors: a 4 x 6 leaf with its corner cut 1.8 by 1.8).
    private static readonly Vector2 DoubleDoorCut = new Vector2(2.4f * 0.45f, 4f * 0.3f);

    // A solid wedge filling a top corner of a doorway: the corner at `offset` (from the door's centre, in its frame),
    // running `side` (+1 right, -1 left) along the lintel and down the post by Double Door Cut, as deep as the frame.
    private static void FrameCorner(string name, Vector3 centre, Quaternion rotation, Vector3 offset, float side, Transform parent)
    {
        GameObject piece = Box(name, centre + rotation * offset, Vector3.one, PropColor, parent);
        piece.transform.rotation = rotation;
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        Mesh wedge = Wedge(DoubleDoorCut.x * side, DoubleDoorCut.y, 0.5f);
        wedge.name = name;
        piece.GetComponent<MeshFilter>().sharedMesh = wedge;
        var solid = piece.AddComponent<MeshCollider>();
        solid.sharedMesh = wedge;
        solid.convex = true;
    }

    // A right-angled wedge: the square corner at the origin, one leg `across` along X, the other `down` metres down Y,
    // `depth` thick along Z. Every face both ways round, so it shows from either side whichever way `across` points.
    private static Mesh Wedge(float across, float down, float depth)
    {
        Vector3 a = Vector3.zero, b = new Vector3(across, 0f, 0f), c = new Vector3(0f, -down, 0f);
        Vector3 front = new Vector3(0f, 0f, -depth * 0.5f), back = new Vector3(0f, 0f, depth * 0.5f);
        var vertices = new System.Collections.Generic.List<Vector3>();
        var triangles = new System.Collections.Generic.List<int>();
        void Face(params Vector3[] p)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                int start = vertices.Count;
                vertices.AddRange(p);
                for (int i = 1; i + 1 < p.Length; i++)
                {
                    triangles.Add(start);
                    triangles.Add(pass == 0 ? start + i : start + i + 1);
                    triangles.Add(pass == 0 ? start + i + 1 : start + i);
                }
            }
        }
        Face(a + front, b + front, c + front);
        Face(a + back, b + back, c + back);
        Face(a + front, b + front, b + back, a + back);
        Face(b + front, c + front, c + back, b + back);
        Face(c + front, a + front, a + back, c + back);
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    internal static void OpenOnFilled(ItemSocket socket, DoubleDoor door)
    {
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(socket.onFilled, door.Open);
    }

    private static GameObject EnsureDoorLikePrefab(string path, string name, Vector3 triggerCenter, Vector3 triggerSize,
        Vector3 visualPosition, Vector3 visualScale, Color color, System.Action<SerializedObject> configure)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return existing;

        var root = new GameObject(name);
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = triggerCenter;
        trigger.size = triggerSize;
        var door = root.AddComponent<Door>();
        root.AddComponent<InteractableHighlight>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = visualPosition;
        visual.transform.localScale = visualScale;
        visual.AddComponent<RendererTint>().Tint = color;

        var so = new SerializedObject(door);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_Open_Wood_CC0.mp3");
        so.FindProperty("unlatchSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_Creak_Wood_CC0.mp3");
        so.FindProperty("openStopSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_MetalClunk_Grinnell_CC0.mp3");
        so.FindProperty("closeStopSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_Shut_Wood_CC0.mp3");
        so.FindProperty("lockedSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorSoundFolder + "Door_LockedRattle_CastIronCarousel_CC0.mp3");
        configure?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = SavePrefab(root, path);
        Debug.Log("Created " + path);
        return prefab;
    }

    internal static ItemSocket Socket(GameObject host, string itemAsset, int amount, bool consume, string verb, string rewardAsset, GameObject[] visuals)
    {
        var socket = host.AddComponent<ItemSocket>();
        host.AddComponent<InteractableHighlight>();
        SetField(socket, "requiredItem", p => p.objectReferenceValue = ItemTools.Load(itemAsset));
        SetField(socket, "requiredAmount", p => p.intValue = amount);
        SetField(socket, "consumeItems", p => p.boolValue = consume);
        SetField(socket, "verb", p => p.stringValue = verb);
        if (rewardAsset != null)
            SetField(socket, "rewardItem", p => p.objectReferenceValue = ItemTools.Load(rewardAsset));

        if (visuals != null)
        {
            var so = new SerializedObject(socket);
            SerializedProperty list = so.FindProperty("placedVisuals");
            list.arraySize = visuals.Length;
            for (int i = 0; i < visuals.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = visuals[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return socket;
    }

    // The bone key tying minigame on a seaweed: a Knot child with its own trigger collider (so the E prompt reaches it
    // after the socket has switched itself off) carrying Bone Key Tying, armed and opened by the socket's On Filled;
    // it hands over the bone key itself and swoops the seaweed when the key is tied. The socket gives no reward.
    internal static BoneKeyTying AddKnotMinigame(GameObject seaweed, ItemSocket socket, Seaweed weed = null)
    {
        var knot = new GameObject("Knot");
        knot.transform.SetParent(seaweed.transform, false);
        var reach = knot.AddComponent<BoxCollider>();
        var hostBox = seaweed.GetComponent<BoxCollider>();   // the same space as the seaweed itself, a little bigger
        reach.center = hostBox != null ? hostBox.center : new Vector3(0f, 1.2f, 0f);
        reach.size = hostBox != null ? hostBox.size * 1.1f : new Vector3(1f, 2.4f, 1f);
        reach.isTrigger = true;
        var tying = knot.AddComponent<BoneKeyTying>();
        tying.enabled = false;
        SetField(tying, "fragmentItem", p => p.objectReferenceValue = ItemTools.Load("Item_BoneKeyFragment"));
        SetField(tying, "rewardItem", p => p.objectReferenceValue = ItemTools.Load("Item_BoneKey"));
        SetField(tying, "keyPickup", p => p.objectReferenceValue = FindPrefab("Pickup_Placeholder"));   // the tied key is shown in the inspect view
        // One picture per piece: Noora's drawings of the three (Art/UI/bonekey1-3); without them, the fragment's own
        // icon for the first and icons rendered from the other two piece models for the rest.
        ItemDefinition fragment = ItemTools.Load("Item_BoneKeyFragment");
        var pieces = new Sprite[3];
        for (int i = 0; i < 3; i++)
        {
            pieces[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/UI/bonekey{i + 1}.png");
            if (pieces[i] != null)
                continue;
            GameObject pieceModel = i == 0 ? null : ItemModelTools.FindModel("bone_key_piece" + (i + 1));
            pieces[i] = pieceModel != null ? ItemModelTools.RenderIcon(pieceModel, "Item_BoneKeyFragment_" + (i + 1)) : fragment != null ? fragment.Icon : null;
        }
        SetField(tying, "pieceIcons", p =>
        {
            p.arraySize = 3;
            for (int i = 0; i < 3; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i];
        });
        SetField(tying, "windSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Puzzles/Knot_Wrap_RopeTying_Kyles_CC0.mp3"));
        SetField(tying, "slideSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Puzzles/Knot_Slide_RopeSliding_Cmilo_CC0.mp3"));
        SetField(tying, "tiedSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Puzzles/Knot_Tied_RopeSnap_Zepurple_CC0.mp3"));
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(socket.onFilled, tying.Begin);
        if (weed != null)
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(tying.onTied, weed.Swoop);
        return tying;
    }

    // The low-poly seaweed clump on an object: the Seaweed component with the shared material, one of the kinds (kelp,
    // sea grass, broad leaf, ribbon) with its own random layout, built here so it shows in the Scene view. It sways,
    // parts around the player and can swoop.
    internal static Seaweed AddSeaweed(GameObject host, Seaweed.Kind kind, int seed)
    {
        var weed = host.AddComponent<Seaweed>();
        SetField(weed, "bladeMaterial", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Seaweed.mat"));
        weed.SetKind(kind, seed);
        return weed;
    }

    // A set-dressing clump of a given kind at a spot on the floor (no collider: nothing to bump into).
    internal static Seaweed SeaweedAt(Vector3 position, Seaweed.Kind kind, int seed, Transform parent)
    {
        var clump = new GameObject("Seaweed_" + kind);
        clump.transform.SetParent(parent, false);
        clump.transform.position = position;
        return AddSeaweed(clump, kind, seed);
    }

    internal static void OpenOnFilled(ItemSocket socket, Door door)
    {
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(socket.onFilled, door.Open);
    }

    // The Door_Placeholder prefab (made on first use) at a hinge point, with a frame around the 2 x 3 opening.
    // yaw turns the whole thing: 0 = the door spans +X and you pass through along Z, 90 = it spans -Z and you pass along X.
    // Which of the team's door models a door wears (Assets/Prefabs: Single_Door, Single_Door_With_Lock, Slide_Up_Door 1)
    // and so how it moves: the plain and the lock door swing on their hinge, the slide-up door slides up.
    internal enum DoorLook { Plain, Lock, SlideUp }

    private const string DoorModelFolder = "Assets/Prefabs/";
    private const string DoorMaterialFolder = "Assets/Prefabs/Door models and textures/";

    // A door, its hinge at `hinge` (the leaf spans +X from it, 2 x 3 m). Look: which model; left out, a door that locks
    // or shuts behind you (a puzzle's or a one-way door) slides up, any other door swings (the plain single door).
    internal static Door SpawnDoor(string name, Vector3 hinge, bool locked, bool closeBehind, Transform parent, float yaw = 0f, DoorLook? look = null)
    {
        GameObject prefab = EnsureDoorPrefab();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        // Its own object from here on: the look is built into it (the prefab keeps whatever visual it has, for doors
        // placed by hand).
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.transform.SetParent(parent, true);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.SetPositionAndRotation(hinge, rotation);
        instance.name = name;

        var door = instance.GetComponent<Door>();
        SetField(door, "locked", p => p.boolValue = locked);
        SetField(door, "closeBehindPlayer", p => p.boolValue = closeBehind);

        DoorLook wears = look ?? (locked || closeBehind ? DoorLook.SlideUp : DoorLook.Plain);
        for (int i = instance.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(instance.transform.GetChild(i).gameObject);
        string model = wears == DoorLook.SlideUp ? "Slide_Up_Door 1" : wears == DoorLook.Lock ? "Single_Door_With_Lock" : "Single_Door";
        Material material = wears == DoorLook.SlideUp ? null : AssetDatabase.LoadAssetAtPath<Material>(DoorMaterialFolder + "Material_single_door.mat");
        Transform visual = FittedModel(instance.transform, "Visual", model, -1, material, new Vector3(1f, 1.5f, 0f), new Vector3(2f, 3f, 0.15f), new Color(0.45f, 0.3f, 0.2f));
        // The hinged models have their knob on the edge they would hinge on here: mirrored (left to right, round the
        // leaf's middle, still facing the same way) so the knob is on the edge that swings.
        Transform fitted = wears != DoorLook.SlideUp ? visual.Find("Fit") : null;
        if (fitted != null)
        {
            fitted.localScale = Vector3.Scale(fitted.localScale, new Vector3(-1f, 1f, 1f));
            fitted.localPosition = Vector3.Scale(fitted.localPosition, new Vector3(-1f, 1f, 1f));
        }
        SetField(door, "visual", p => p.objectReferenceValue = visual);
        SetField(door, "motion", p => p.enumValueIndex = (int)(wears == DoorLook.SlideUp ? Door.Motion.Slide : Door.Motion.Swing));

        FramePiece("Frame_Post", hinge, rotation, new Vector3(-0.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.5f), parent);
        FramePiece("Frame_Post", hinge, rotation, new Vector3(2.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.5f), parent);
        FramePiece("Frame_Top", hinge, rotation, new Vector3(1f, 3.2f, 0f), new Vector3(2.8f, 0.4f, 0.5f), parent);   // as deep as a wall, so no cut face shows beside the frame
        return door;
    }

    // A door model (a prefab in Door Model Folder) fitted into a leaf: a "Visual" under `parent`, its middle at `centre`,
    // the model stretched to fill `size` across and up (the leaf; its depth scaled along in proportion), turned so its
    // thin side faces through the doorway, with a solid box collider the size of the leaf (the model's own colliders
    // go). Part = which piece of a model with several (the double doors' two leaves, left to right), -1 = all of it.
    // No model found: the plain placeholder panel, as before.
    internal static Transform FittedModel(Transform parent, string name, string modelPrefab, int part, Material material, Vector3 centre, Vector3 size, Color fallback)
    {
        var visual = new GameObject(name).transform;
        visual.SetParent(parent, false);
        visual.localPosition = centre;
        var solid = visual.gameObject.AddComponent<BoxCollider>();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorModelFolder + modelPrefab + ".prefab");
        if (prefab == null)
        {
            Debug.LogWarning($"Door model {DoorModelFolder}{modelPrefab}.prefab not found: {parent.name} gets the plain panel.");
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(panel.GetComponent<Collider>());
            panel.name = "Panel";
            panel.transform.SetParent(visual, false);
            panel.transform.localScale = size;
            panel.AddComponent<RendererTint>().Tint = fallback;
            solid.size = size;
            return visual;
        }

        // visual > Fit (stretches, along the leaf's own axes) > Turn (quarter turns only) > the model as its prefab has it.
        var fit = new GameObject("Fit").transform;
        fit.SetParent(visual, false);
        var turn = new GameObject("Turn").transform;
        turn.SetParent(fit, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.transform.SetParent(turn, false);
        model.transform.localPosition = Vector3.zero;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Object.DestroyImmediate(c);

        // Only the one piece (left to right as the model stands).
        if (part >= 0)
        {
            var pieces = new System.Collections.Generic.List<Renderer>(model.GetComponentsInChildren<Renderer>(true));
            pieces.Sort((a, b) => visual.InverseTransformPoint(a.bounds.center).x.CompareTo(visual.InverseTransformPoint(b.bounds.center).x));
            for (int i = 0; i < pieces.Count; i++)
                if (i != part && pieces[i].gameObject != model)
                    Object.DestroyImmediate(pieces[i].gameObject);
        }
        if (material != null)
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = material;

        // Its thin side through the doorway (along Z): a model lying the other way is turned a quarter.
        Bounds b = LocalBounds(model, visual);
        if (b.size.x < b.size.z * 0.8f)
        {
            turn.localRotation = Quaternion.Euler(0f, 90f, 0f);
            b = LocalBounds(model, visual);
        }
        if (b.size.x < 0.0001f || b.size.y < 0.0001f)
            return visual;
        float across = size.x / b.size.x, up = size.y / b.size.y;
        fit.localScale = new Vector3(across, up, Mathf.Sqrt(across * up));
        b = LocalBounds(model, visual);
        fit.localPosition = -b.center;   // centred on the leaf's middle
        solid.size = new Vector3(size.x, size.y, Mathf.Max(size.z, b.size.z));
        return visual;
    }

    // The renderers' box in `space` (from their meshes' own bounds, so it is exact whatever the rotations).
    private static Bounds LocalBounds(GameObject model, Transform space)
    {
        bool any = false;
        var result = new Bounds();
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Bounds own = r.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = own.center + Vector3.Scale(own.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(corner));
                if (!any)
                {
                    result = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                    result.Encapsulate(p);
            }
        }
        return result;
    }

    private static void FramePiece(string name, Vector3 hinge, Quaternion rotation, Vector3 offset, Vector3 size, Transform parent)
    {
        GameObject piece = Box(name, hinge + rotation * offset, size, PropColor, parent);
        piece.transform.rotation = rotation;
    }

    // ---- chase (GDD map room 9: the hallway, the room after it, the corridor where the rubble comes down) -----------

    internal static AudioClip Sfx(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + file);

    // A pufferfish nest (Puffer Nest): the enemy pufferfish come out of it, Max Alive at a time. Facing = out of its
    // mouth (up from a floor, into the room from a wall); it builds its own look (a red, spiny, glowing mound) in play.
    internal static PufferNest NestAt(string name, Vector3 at, Vector3 facing, int maxAlive, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(facing, Mathf.Abs(facing.y) > 0.9f ? Vector3.forward : Vector3.up));
        var nest = go.AddComponent<PufferNest>();
        SetField(nest, "pufferPrefab", p => p.objectReferenceValue = FindPrefab("Pufferfish"));
        SetField(nest, "maxAlive", p => p.intValue = maxAlive);
        SetField(nest, "warnSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Ambience/Ambience_DeepBoom_ZaGames_CC0.mp3"));
        SetField(nest, "burstSound", p => p.objectReferenceValue = Sfx("Enemy fish Attack sound effect.mp3"));
        SetField(nest, "breakSound", p => p.objectReferenceValue = Sfx("Hit impact.wav"));
        return nest;
    }

    // A box without a collider: murals and other set dressing nothing should bump into.
    internal static GameObject Decor(string name, Vector3 center, Vector3 size, Color color, Transform parent)
    {
        GameObject box = Box(name, center, size, color, parent);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    // One of the three symbols painted about the ship (Noora's drawings, Art/UI/symbol1-3): a flat picture `size`
    // metres across at `center`, facing `facing` (up for the floor, into the room for a wall). No collider. Each symbol
    // has its own material (Art/Materials/Symbol1-3.mat, made once: URP Lit, cut out round the paint, glowing a little
    // so it reads in the dark water). Falls back to a plain square of `color` if the drawing is gone.
    internal static GameObject PaintedSymbol(string name, int symbol, Vector3 center, float size, Vector3 facing, Color color, Transform parent)
    {
        Material material = SymbolMaterial(symbol, color);
        if (material == null)
        {
            Vector3 flat = new Vector3(Mathf.Abs(facing.x) > 0.5f ? 0.04f : size, Mathf.Abs(facing.y) > 0.5f ? 0.04f : size, Mathf.Abs(facing.z) > 0.5f ? 0.04f : size);
            return Decor(name, center, flat, color, parent);
        }
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(parent, false);
        // A quad shows its face along -Z: turn that face to `facing` (on the floor, the drawing's top towards +Z).
        Vector3 up = Mathf.Abs(facing.y) > 0.5f ? Vector3.forward : Vector3.up;
        quad.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-facing, up));
        quad.transform.localScale = new Vector3(size, size, 1f);
        quad.GetComponent<Renderer>().sharedMaterial = material;
        quad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad;
    }

    private static Material SymbolMaterial(int symbol, Color color)
    {
        string path = $"Assets/Art/Materials/Symbol{symbol}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Art/UI/symbol{symbol}.png");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (texture == null || shader == null)
            return null;
        material = new Material(shader) { name = "Symbol" + symbol };
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.15f);
        material.SetFloat("_AlphaClip", 1f);
        material.SetFloat("_Cutoff", 0.5f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.EnableKeyword("_EMISSION");
        material.SetTexture("_EmissionMap", texture);
        material.SetColor("_EmissionColor", color * 0.6f);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
            AssetDatabase.CreateFolder("Assets/Art", "Materials");
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // A rock of the chase rubble: one of the five in Art/Models/Rocks.fbx (`index` picks which, wrapping round), with
    // the artist's Rock materials, stretched to `size` (metres) at `center`, with a convex mesh collider so it blocks
    // as it looks. A plain box of `color` if the model is gone.
    internal static GameObject RockModel(string name, Vector3 center, Vector3 size, int index, Color color, Transform parent)
    {
        var meshes = new System.Collections.Generic.List<MeshFilter>();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Rocks.fbx");
        if (model != null)
            foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null)
                    meshes.Add(filter);
        if (meshes.Count == 0)
            return Box(name, center, size, color, parent);
        meshes.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        MeshFilter source = meshes[((index % meshes.Count) + meshes.Count) % meshes.Count];
        Mesh mesh = source.sharedMesh;

        var rock = new GameObject(name);
        rock.transform.SetParent(parent, false);
        rock.transform.position = center;
        rock.AddComponent<MeshFilter>().sharedMesh = mesh;
        rock.AddComponent<MeshRenderer>().sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
        Vector3 own = mesh.bounds.size;
        rock.transform.localScale = new Vector3(size.x / Mathf.Max(0.01f, own.x), size.y / Mathf.Max(0.01f, own.y), size.z / Mathf.Max(0.01f, own.z));
        var solid = rock.AddComponent<MeshCollider>();
        solid.sharedMesh = mesh;
        solid.convex = true;
        return rock;
    }

    // ---- the treasure chest ----------------------------------------------------------------------------------------

    internal const string ChestPrefabPath = "Assets/Prefabs/TreasureChest.prefab";
    private const string ChestModelFolder = "Assets/Art/Models/objects/treasure_chest/";
    private const float ChestWidth = 1.6f;   // across the front, metres (the model's own proportions give the rest)
    // Bumped when the way the prefab is put together changes: an older prefab is made again from the models.
    // (2: the lid sits on the front rim, not on the hinge knuckles at the back, which stand higher: it looked ajar.)
    private const int ChestPrefabVersion = 2;

    [MenuItem("Tools/Out of the Depths/Create Treasure Chest Prefab")]
    private static void CreateChestPrefabMenu()
    {
        bool had = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath) != null;
        GameObject prefab = EnsureChestPrefab();
        if (prefab != null)
        {
            Selection.activeObject = prefab;
            Debug.Log(had ? $"{ChestPrefabPath} is already there (delete it to make it again from the models)." : $"Made {ChestPrefabPath}.", prefab);
        }
    }

    // Prefabs/TreasureChest: the chest's base and lid models (Art/Models/objects/treasure_chest) put together as one
    // object, made once. TreasureChest (root: a solid box collider round the closed chest, Treasure Chest, the
    // highlight) > Model (turned so the chest's front faces +Z) > Base, and Lid: the hinge along the back of the base's
    // top, with the lid model on it (the lid is modelled round its hinge, reaching forward from it). The base is scaled
    // to Chest Width across, sitting on the root's origin; the lid gets the same scale.
    internal static GameObject EnsureChestPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefabPath);
        if (existing != null)
        {
            var made = existing.GetComponent<TreasureChest>();
            if (made == null || made.MadeVersion >= ChestPrefabVersion)
                return existing;
            AssetDatabase.DeleteAsset(ChestPrefabPath);   // an older make: again, the current way
        }
        var baseModel = AssetDatabase.LoadAssetAtPath<GameObject>(ChestModelFolder + "treasure_chest_base.fbx");
        var lidModel = AssetDatabase.LoadAssetAtPath<GameObject>(ChestModelFolder + "treasure_chest_lid.fbx");
        if (baseModel == null || lidModel == null)
        {
            Debug.LogWarning($"The treasure chest models are not in {ChestModelFolder}: plain boxes stand in for the chests.");
            return null;
        }

        var root = new GameObject("TreasureChest");
        try
        {
            Transform model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);

            GameObject body = Object.Instantiate(baseModel, model, false);
            body.name = "Base";
            Bounds b = LocalBounds(body, model);
            float scale = ChestWidth / Mathf.Max(0.001f, b.size.x);
            body.transform.localScale *= scale;
            b = LocalBounds(body, model);
            body.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            b = LocalBounds(body, model);

            Transform hinge = new GameObject("Lid").transform;
            hinge.SetParent(model, false);
            GameObject cover = Object.Instantiate(lidModel, hinge, false);
            cover.name = "LidModel";
            cover.transform.localScale *= scale;
            Bounds l = LocalBounds(cover, hinge);
            cover.transform.localPosition -= new Vector3(l.center.x, 0f, 0f);   // centred across, the hinge where it is
            float reach = l.center.z >= 0f ? 1f : -1f;   // the way the lid reaches from its hinge: the chest's front
            // The hinge: at the height of the base's front rim (its back, with the hinge knuckles, stands higher), so
            // the lid lies flat on the rim with its own knuckles down beside the base's; along the back so the lid's
            // back edge (a little behind its hinge) lines up with the base's.
            float rim = FrontRimTop(body, model, b, reach);
            hinge.localPosition = new Vector3(0f, rim, reach > 0f ? b.min.z - l.min.z : b.max.z - l.max.z);
            if (reach < 0f)
                model.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Bounds all = LocalBounds(root, root.transform);
            var solid = root.AddComponent<BoxCollider>();
            solid.center = all.center;
            solid.size = all.size;
            var chest = root.AddComponent<TreasureChest>();
            root.AddComponent<InteractableHighlight>();
            SetField(chest, "lid", p => p.objectReferenceValue = hinge);
            // Turning the hinge round X by a negative angle lifts a lid that reaches along +Z (and the other way round).
            SetField(chest, "openEuler", p => p.vector3Value = new Vector3(reach > 0f ? -105f : 105f, 0f, 0f));
            SetField(chest, "openSound", p => p.objectReferenceValue = Sfx("Chest opening  closing sound effect.mp3"));
            SetField(chest, "madeVersion", p => p.intValue = ChestPrefabVersion);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ChestPrefabPath);
            if (saved != null)
                Debug.Log($"Made {ChestPrefabPath} from the chest models.", saved);
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Where things in a chest float once out: `above` metres over the top of the shut chest (its collider). A plain
    // box stand-in (no Treasure Chest): over its own top.
    internal static float ChestTop(GameObject chest, float above)
    {
        var box = chest.GetComponent<BoxCollider>();
        if (box == null)
            return chest.transform.position.y + 1f + above;
        return chest.transform.TransformPoint(box.center + Vector3.up * (box.size.y * 0.5f)).y + above;
    }

    // The things in a chest (its Contents), lying inside it: hidden while it is shut, rising out of it to float over it
    // when it opens.
    internal static void ChestHolds(TreasureChest chest, params GameObject[] things)
    {
        SetField(chest, "contents", p =>
        {
            p.arraySize = things.Length;
            for (int i = 0; i < things.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = things[i] != null ? things[i].transform : null;
        });
    }

    // The top of the base's front wall, in `space`: the highest point of its front quarter (`reach` = which way is
    // front along Z), away from the hinge knuckles at the back.
    private static float FrontRimTop(GameObject body, Transform space, Bounds b, float reach)
    {
        float top = float.MinValue;
        foreach (MeshFilter filter in body.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.sharedMesh.isReadable)   // (Read/Write is on for the chest models)
                continue;
            Matrix4x4 m = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 p = m.MultiplyPoint3x4(v);
                if ((p.z - b.center.z) * reach > b.size.z * 0.25f)
                    top = Mathf.Max(top, p.y);
            }
        }
        return top > float.MinValue ? top : b.max.y;
    }

    // A treasure chest from Prefabs/TreasureChest standing at `at` (its bottom), its front facing `yaw` (0 = +Z). Its
    // own object (unpacked), so the builder can add a lock and wire it. Null if the models are gone.
    internal static TreasureChest SpawnChest(string name, Vector3 at, float yaw, bool startOpen, Transform parent)
    {
        GameObject prefab = EnsureChestPrefab();
        if (prefab == null)
            return null;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        instance.name = name;
        var chest = instance.GetComponent<TreasureChest>();
        if (startOpen)
            SetField(chest, "startOpen", p => p.boolValue = true);
        return chest;
    }

    // The chase rubble's rocks, heaped like a fallen roof rather than stacked in rows: `width` across (along
    // `across`), up to `height`, about `depth` thick, round `foot` (the middle of the pile's base). Big rocks at the
    // bottom and smaller ones higher up, each layer over the gaps of the one below, every rock turned and squashed its
    // own way; a few small ones spilt at the foot on both sides. The same pile every build (`seed`).
    internal static void RubblePile(Transform parent, Vector3 foot, Vector3 across, float width, float height, float depth, Color fallback, int seed)
    {
        var random = new System.Random(seed);
        float R(float a, float b) => a + (float)random.NextDouble() * (b - a);
        across.y = 0f;
        across = across.sqrMagnitude > 0.0001f ? across.normalized : Vector3.right;
        Vector3 through = Vector3.Cross(across, Vector3.up);

        float[] levels = { 0.17f, 0.41f, 0.64f, 0.84f };   // the middle of each layer, as a share of the height
        float[] sizes = { 0.46f, 0.39f, 0.32f, 0.27f };    // its rocks, as a share of the height
        int n = 0;
        for (int layer = 0; layer < levels.Length; layer++)
        {
            float size = height * sizes[layer];
            int count = Mathf.Max(2, Mathf.CeilToInt(width / (size * 0.8f)));
            float step = width / count;
            for (int i = 0; i < count; i++)
            {
                float s = size * R(0.8f, 1.2f);
                float along = -width * 0.5f + step * (i + 0.5f) + (layer % 2 == 1 ? step * 0.5f : 0f) + R(-0.2f, 0.2f) * step;
                if (along > width * 0.5f)
                    along -= width;
                along = Mathf.Clamp(along, -width * 0.5f + s * 0.3f, width * 0.5f - s * 0.3f);
                Vector3 at = foot + across * along + through * (R(-0.3f, 0.3f) * depth) + Vector3.up * (height * levels[layer] + R(-0.12f, 0.12f) * s);
                Vector3 shape = new Vector3(s * R(0.95f, 1.3f), s * R(0.7f, 0.95f), s * R(0.85f, 1.15f));
                GameObject rock = RockModel("Rock", at, shape, n * 3 + layer, fallback, parent);
                rock.transform.rotation = Quaternion.LookRotation(across, Vector3.up) * Quaternion.Euler(R(-25f, 25f), R(0f, 360f), R(-25f, 25f));
                n++;
            }
        }
        for (int i = 0; i < 6; i++)
        {
            float s = height * R(0.08f, 0.15f);
            float side = i % 2 == 0 ? 1f : -1f;
            Vector3 at = foot + across * (R(-0.4f, 0.4f) * width) + through * (side * depth * R(0.7f, 1.1f)) + Vector3.up * (s * 0.3f);
            GameObject rock = RockModel("Rock", at, new Vector3(s * R(1f, 1.4f), s * R(0.6f, 0.9f), s), n * 3, fallback, parent);
            rock.transform.rotation = Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f));
            n++;
        }
    }

    // A mural: a picture slot on a wall. Facing = which way it looks (into the room); size = width and height in
    // metres. Plain until someone drops a picture on its Mural component (Picture). Every mural shares Mural.mat.
    internal static GameObject MuralAt(Vector3 center, Vector2 size, Vector3 facing, Transform parent)
    {
        var mural = new GameObject("Mural");
        mural.transform.SetParent(parent, false);
        mural.transform.SetPositionAndRotation(center, Quaternion.LookRotation(-facing, Vector3.up));
        GameObject picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
        picture.name = "Picture";
        Object.DestroyImmediate(picture.GetComponent<Collider>());
        picture.transform.SetParent(mural.transform, false);
        Material material = EnsureMuralMaterial();
        if (material != null)
            picture.GetComponent<Renderer>().sharedMaterial = material;
        mural.AddComponent<Mural>().Setup(size, new Color(0.45f, 0.2f, 0.7f));
        return mural;
    }

    // Mural.mat: URP Lit, plain white with emission switched on, so each mural can show its own picture (and glow a
    // little) through a property block, without a material of its own.
    private static Material EnsureMuralMaterial()
    {
        const string path = "Assets/Art/Materials/Mural.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        material = new Material(shader != null ? shader : Shader.Find("Standard"));
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Smoothness", 0.3f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void BuildChaseCorridor()
    {
        Transform zone = Group("Zone_Chase");
        Color hull = new Color(0.14f, 0.16f, 0.2f);
        Color deck = new Color(0.18f, 0.2f, 0.24f);
        Color stone = new Color(0.7f, 0.65f, 0.5f);
        Color rock = new Color(0.3f, 0.27f, 0.24f);

        // Briefing and a checkpoint in front of the final door (the door itself sits in the arena's north wall).
        GameObject plate = Spawn("RespawnPlate", new Vector3(-1.5f, 0.05f, 27.8f), zone);   // beside the door, not in front of it
        if (plate != null)
            plate.name = "Checkpoint_Chase";
        Door finalDoor = SpawnDoor("Door_Final", new Vector3(2f, 0f, 30.5f), true, true, zone);
        SetField(finalDoor, "lockBehind", p => p.boolValue = false);
        SetField(finalDoor, "openPrompt", p => p.stringValue = "open the rune door");
        // The rune lock beside it: the symbol puzzle on the board unlocks the door.
        // In front of the wall (it is a metre thick, faces at z 30 and 31), where it can be seen and reached.
        GameObject runeLock = Box("RuneLock", new Vector3(5.6f, 1.6f, 29.8f), new Vector3(0.7f, 0.7f, 0.3f), new Color(0.8f, 0.7f, 0.3f), zone);
        PuzzleStation runeStation = PuzzleBuildTools.AddStation(runeLock, "Puzzle_Runes", null, 0, false, "enter the runes");
        PuzzleBuildTools.SolveUnlocks(runeStation, finalDoor);
        PuzzleBuildTools.SolveOpens(runeStation, finalDoor);   // unlocked and swung open: solving the runes starts the way to the chase
        Label("CHASE - E on the plate first, then the rune lock beside the door (the three symbols, 1-2-3) unlocks it. Through the door the grate at the far end of the hallway behind you bursts and three chase pufferfish come out: the dagger does nothing to them, three bites and you're dead. Grab the two stone fragments (Space / Ctrl), slot them in the tablet by the far door, then take the trident at the end of the long corridor and the rubble seals it behind you.", new Vector3(10f, 3.4f, 28f), zone);

        // Floor and ceilings outside the arena wall: the hallway (room 9), the room after it and the long corridor.
        Box("Floor_Chase", new Vector3(12f, -0.1f, 46.5f), new Vector3(46f, 0.2f, 31f), deck, zone);
        // The doorway through the arena wall (z 30..31): neither floor reaches under it, so it gets its own strip.
        Box("Floor_Doorway", new Vector3(3f, -0.1f, 30.5f), new Vector3(3.2f, 0.2f, 1.1f), deck, zone);
        Box("Ceiling_Hall", new Vector3(7.5f, 4.65f, 33.25f), new Vector3(36f, 0.3f, 4.5f), hull, zone);
        Box("Ceiling_Room", new Vector3(29.75f, 4.65f, 35.25f), new Vector3(8.5f, 0.3f, 8.5f), hull, zone);
        Box("Ceiling_Corridor", new Vector3(30.25f, 4.65f, 51f), new Vector3(4.5f, 0.3f, 22.5f), hull, zone);

        // Hallway: x -10..25.5, z 31..35.5, murals on the north wall like the map. The door is 12 m along it, so the
        // vent in its west end (where the pack waits) is well behind the player when they come in.
        Box("Hall_N", new Vector3(7.5f, 2.4f, 35.75f), new Vector3(36f, 4.8f, 0.5f), hull, zone);
        Box("Hall_W_Low", new Vector3(-10.25f, 1.2f, 33.25f), new Vector3(0.5f, 2.4f, 4.5f), hull, zone);
        Box("Hall_W_High", new Vector3(-10.25f, 4.4f, 33.25f), new Vector3(0.5f, 0.8f, 4.5f), hull, zone);
        Box("Hall_W_S", new Vector3(-10.25f, 3.2f, 31.5f), new Vector3(0.5f, 1.6f, 1f), hull, zone);
        Box("Hall_W_N", new Vector3(-10.25f, 3.2f, 34.95f), new Vector3(0.5f, 1.6f, 1.1f), hull, zone);
        // The vent: a hollow duct behind the hole (back, floor, ceiling, sides), open into the hallway, so the pack waits in open water.
        Color duct = new Color(0.03f, 0.03f, 0.04f);
        Box("Vent_Back", new Vector3(-12.6f, 3.2f, 33.2f), new Vector3(0.2f, 2f, 2.8f), duct, zone);
        Box("Vent_Floor", new Vector3(-11.25f, 2.3f, 33.2f), new Vector3(2.5f, 0.2f, 2.8f), duct, zone);
        Box("Vent_Ceiling", new Vector3(-11.25f, 4.1f, 33.2f), new Vector3(2.5f, 0.2f, 2.8f), duct, zone);
        Box("Vent_S", new Vector3(-11.25f, 3.2f, 31.9f), new Vector3(2.5f, 2f, 0.2f), duct, zone);
        Box("Vent_N", new Vector3(-11.25f, 3.2f, 34.5f), new Vector3(2.5f, 2f, 0.2f), duct, zone);
        MuralAt(new Vector3(-4f, 2.4f, 35.45f), new Vector2(5f, 1.6f), Vector3.back, zone);
        MuralAt(new Vector3(8f, 2.4f, 35.45f), new Vector2(5f, 1.6f), Vector3.back, zone);
        MuralAt(new Vector3(17f, 2.4f, 35.45f), new Vector2(4f, 1.6f), Vector3.back, zone);

        // The pack: three chase pufferfish asleep in the vent; the trigger just inside the door wakes them.
        var chaseGo = new GameObject("ChaseSequence");
        chaseGo.transform.SetParent(zone, false);
        chaseGo.transform.position = new Vector3(-11.25f, 3.2f, 33.2f);
        var chase = chaseGo.AddComponent<ChaseSequence>();
        SetField(chase, "startSound", p => p.objectReferenceValue = Sfx("Deep Sea Monster sound effect.mp3"));
        SetField(chase, "endSound", p => p.objectReferenceValue = Sfx("Puzzle Completed Sound effect.mp3"));
        SetField(chase, "tensionLoop", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Ambience/Ambience_HorrorRumble_Medoob_CC0.mp3"));
        SetField(chase, "nearSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Player/Swim_UnderwaterMovement_Cabusta_CC0.mp3"));
        SetField(chase, "hushSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Ambience/Ambience_MetallicGroan_Hinchinbrook_CC0.mp3"));
        GameObject pursuerPrefab = ChaseTools.EnsurePursuerPrefab();
        Vector3[] slots = { new Vector3(-12f, 3.2f, 32.6f), new Vector3(-11.3f, 3.2f, 33.2f), new Vector3(-10.6f, 3.2f, 33.8f) };
        for (int i = 0; i < slots.Length; i++)
        {
            var fish = (GameObject)PrefabUtility.InstantiatePrefab(pursuerPrefab, zone.gameObject.scene);
            fish.transform.SetParent(chaseGo.transform, true);
            fish.transform.SetPositionAndRotation(slots[i], Quaternion.Euler(0f, 90f, 0f));
            fish.name = "ChasePufferfish_" + (i + 1);
            fish.SetActive(false);
        }

        // The grate over the vent: blown off and dropped to the floor when the chase starts, and the camera is drawn to it.
        var grate = new GameObject("VentGrate");
        grate.transform.SetParent(zone, false);
        grate.transform.SetPositionAndRotation(new Vector3(-10.25f, 3.2f, 33.2f), Quaternion.Euler(0f, 90f, 0f));
        Color iron = new Color(0.2f, 0.2f, 0.22f);
        foreach (float z in new[] { 32.55f, 33.2f, 33.85f })
            Decor("Bar", new Vector3(-10.25f, 3.2f, z), new Vector3(0.08f, 1.6f, 0.08f), iron, zone).transform.SetParent(grate.transform, true);
        foreach (float y in new[] { 2.85f, 3.55f })
            Decor("Bar", new Vector3(-10.25f, y, 33.2f), new Vector3(0.08f, 0.08f, 2.4f), iron, zone).transform.SetParent(grate.transform, true);
        SetField(chase, "grate", p => p.objectReferenceValue = grate.transform);
        SetField(chase, "grateSound", p => p.objectReferenceValue = Sfx("Hit impact.wav"));

        var start = new GameObject("ChaseStart");
        start.transform.SetParent(zone, false);
        // The whole hallway, from a metre in off the door's wall: the chase starts once the player is properly
        // through the door, never while they stand in the doorway.
        start.transform.position = new Vector3(7.75f, 2.4f, 33.55f);
        var startBox = start.AddComponent<BoxCollider>();
        startBox.isTrigger = true;
        startBox.size = new Vector3(34.5f, 4.8f, 3.9f);
        SetField(chase, "startTrigger", p => p.objectReferenceValue = start.AddComponent<PlayerAreaTrigger>());

        // Under pressure: two stone fragments, one up by the ceiling and one on the floor, for the tablet that opens the far door.
        CreatePickup(new Vector3(9f, 3.7f, 33.2f), "Item_StoneFragment", zone);
        CreatePickup(new Vector3(17f, 0.5f, 34.5f), "Item_StoneFragment", zone);
        Box("Hall_E_S", new Vector3(25.75f, 2.4f, 31.9f), new Vector3(0.5f, 4.8f, 1.8f), hull, zone);
        Box("Hall_E_Top", new Vector3(25.75f, 4.1f, 34.2f), new Vector3(0.5f, 1.4f, 2.8f), hull, zone);
        Box("Hall_E_N", new Vector3(25.75f, 2.4f, 35.8f), new Vector3(0.5f, 4.8f, 0.6f), hull, zone);
        Door hallDoor = SpawnDoor("Door_Hallway", new Vector3(25.75f, 0f, 35.2f), true, false, zone, 90f);
        GameObject tablet = Box("RuneTablet", new Vector3(23.5f, 1.8f, 35.4f), new Vector3(1.1f, 1.3f, 0.2f), stone, zone);
        var slotted = new GameObject[2];
        for (int i = 0; i < slotted.Length; i++)
        {
            slotted[i] = Box("Placed_Stone_" + (i + 1), new Vector3(23.2f + i * 0.6f, 1.8f, 35.22f), new Vector3(0.3f, 0.35f, 0.16f), new Color(0.4f, 0.8f, 0.85f), zone);
            slotted[i].SetActive(false);
        }
        OpenOnFilled(Socket(tablet, "Item_StoneFragment", 2, true, "slot", null, slotted), hallDoor);

        // The room after the hallway (normal fish, like the map) and the long corridor north out of it.
        Box("Room_W", new Vector3(25.75f, 2.4f, 37.8f), new Vector3(0.5f, 4.8f, 3.6f), hull, zone);
        Box("Room_S", new Vector3(32.75f, 2.4f, 30.75f), new Vector3(3.5f, 4.8f, 0.5f), hull, zone);
        Box("Room_E", new Vector3(34.25f, 2.4f, 35.25f), new Vector3(0.5f, 4.8f, 9f), hull, zone);
        Box("Room_N_W", new Vector3(26.75f, 2.4f, 39.75f), new Vector3(2.5f, 4.8f, 0.5f), hull, zone);
        Box("Room_N_E", new Vector3(33.5f, 2.4f, 39.75f), new Vector3(2f, 4.8f, 0.5f), hull, zone);
        CreateSchool("Fish_Wanderer", 3, new Vector3(30f, 2.2f, 35f), zone);
        Box("Corridor_W", new Vector3(27.75f, 2.4f, 51f), new Vector3(0.5f, 4.8f, 22.5f), hull, zone);
        Box("Corridor_E", new Vector3(32.75f, 2.4f, 51f), new Vector3(0.5f, 4.8f, 22.5f), hull, zone);
        Box("Corridor_End", new Vector3(30.25f, 2.4f, 62.25f), new Vector3(5.5f, 4.8f, 0.5f), hull, zone);

        // Rubble: rocks placed where they land (Rubble Fall lifts them out of sight at start) and a blocker sealing the gaps.
        var rubbleGo = new GameObject("Rubble");
        rubbleGo.transform.SetParent(zone, false);
        rubbleGo.transform.position = new Vector3(30.25f, 0f, 53.5f);
        var blockerGo = new GameObject("Blocker");
        blockerGo.transform.SetParent(rubbleGo.transform, false);
        blockerGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        var blocker = blockerGo.AddComponent<BoxCollider>();
        blocker.size = new Vector3(4.5f, 4.5f, 1.8f);
        RubblePile(rubbleGo.transform, rubbleGo.transform.position, Vector3.right, 4.5f, 4.5f, 1.6f, rock, 53);
        var rubble = rubbleGo.AddComponent<RubbleFall>();
        SetField(rubble, "blocker", p => p.objectReferenceValue = blocker);
        SetField(rubble, "thudSound", p => p.objectReferenceValue = Sfx("Hit impact.wav"));
        SetField(rubble, "rumbleSound", p => p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Ambience/Ambience_OminousRumble_Mendenhall_CC0.mp3"));   // the roof giving way
        SetField(chase, "endRubble", p => p.objectReferenceValue = rubble);

        // The trident on its pedestal: taking it (E for now, the button mash comes later) drops the rubble.
        Box("Pedestal_Trident", new Vector3(30.25f, 0.6f, 57.5f), new Vector3(1.2f, 1.2f, 1.2f), PropColor, zone);
        GameObject trident = CreatePickup(new Vector3(30.25f, 1.7f, 57.5f), "Item_Trident", zone);
        PickupItem pickup = trident != null ? trident.GetComponentInChildren<PickupItem>() : null;
        SetField(rubble, "dropOnPickup", p => p.objectReferenceValue = pickup);
        GameObject exitPlate = Spawn("RespawnPlate", new Vector3(31.8f, 0.05f, 59.3f), zone);   // against the corridor wall, off the way to the trident
        if (exitPlate != null)
            exitPlate.name = "Checkpoint_Exit";
        Label("EXIT - E takes the trident (button mash later) and the rubble comes down behind you. Trident combat starts here.", new Vector3(30.25f, 3.55f, 61.3f), zone);   // up by the ceiling at the end of the corridor
    }

    // ---- player / settings --------------------------------------------------------------------------------------

    private static void PlacePlayer()
    {
        var player = Object.FindFirstObjectByType<SwimController>();
        if (player != null)
        {
            player.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);
            if (player.GetComponent<PlayerInventory>() == null)
                player.gameObject.AddComponent<PlayerInventory>();
        }

        GameObject spawnPoint = GameObject.Find("SpawnPoint");
        if (spawnPoint == null)
            spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.SetPositionAndRotation(SpawnPosition, Quaternion.identity);

        var death = Object.FindFirstObjectByType<DeathManager>();
        if (death != null)
            SetField(death, "respawnPoint", p =>
            {
                if (p.objectReferenceValue == null)
                    p.objectReferenceValue = spawnPoint.transform;
            });
    }

    internal static void TuneWarningThresholds()
    {
        foreach (var hunger in Object.FindObjectsByType<HungerSystem>(FindObjectsSortMode.None))
            SetField(hunger, "warningThreshold01", p => p.floatValue = 0.25f);
        foreach (var health in Object.FindObjectsByType<HealthSystem>(FindObjectsSortMode.None))
            SetField(health, "warningThreshold01", p => p.floatValue = 0.3f);
    }

    private static void AddPickupToAdminPanel()
    {
        GameObject prefab = FindPrefab("Pickup_Placeholder");
        var admin = Object.FindFirstObjectByType<AdminPanel>(FindObjectsInactive.Include);
        if (prefab == null || admin == null)
            return;

        var so = new SerializedObject(admin);
        SerializedProperty list = so.FindProperty("spawnables");
        if (list == null)
            return;

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
                return;
        }

        list.arraySize++;
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("label").stringValue = "Pickup";
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The admin panel's Give list: every item asset, so any of them can be handed out while testing.
    internal static void WireAdminPanel()
    {
        AddPickupToAdminPanel();
        var admin = Object.FindFirstObjectByType<AdminPanel>(FindObjectsInactive.Include);
        if (admin == null)
            return;

        ItemDefinition[] items = ItemTools.LoadAll();
        var so = new SerializedObject(admin);
        SerializedProperty list = so.FindProperty("items");
        if (list == null)
            return;
        list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    internal static Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(arenaRoot, false);
        return group;
    }

    internal static GameObject Box(string name, Vector3 center, Vector3 size, Color color, Transform parent)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.AddComponent<RendererTint>().Tint = color;
        return box;
    }

    // A signpost: post, framed board, and the text on both faces as crisp world-space UI - a big accent-coloured title
    // ("PICKUPS") over the body text, with a drop shadow so it reads against anything. The board turns to face the
    // player while they are near (ProximityLabel) so it reads from any side, and fades in as they approach.
    // Text format: "TITLE - body text"; the body wraps by itself and the board grows to fit.
    internal static void Label(string text, Vector3 position, Transform parent)
    {
        // "TITLE - body" or "TITLE\nbody": the title is whatever comes before the first " - ", or the first line if
        // that comes sooner. Anything else would end up as a giant bold title spilling off the board.
        int split = text.IndexOf(" - ", System.StringComparison.Ordinal);
        int splitLength = 3;
        int newline = text.IndexOf('\n');
        if (newline >= 0 && (split < 0 || newline < split))
        {
            split = newline;
            splitLength = 1;
        }
        string title = split >= 0 ? text.Substring(0, split) : text;
        string body = split >= 0 ? text.Substring(split + splitLength) : string.Empty;
        string[] bodyLines = string.IsNullOrEmpty(body) ? new string[0] : Wrap(body, 34);

        const float boardWidth = 3.4f;
        const float titleHeight = 0.42f;
        const float lineHeight = 0.24f;
        const float padding = 0.2f;
        float boardHeight = padding * 2f + titleHeight + bodyLines.Length * lineHeight;

        // The sign floats at the height the caller gives (no post): the board is centred on the root, which bobs and
        // turns toward the player (ProximityLabel).
        var sign = new GameObject("Sign_" + title);
        sign.transform.SetParent(parent, false);
        sign.transform.position = new Vector3(position.x, Mathf.Max(position.y, boardHeight * 0.5f + 1.2f), position.z);
        Vector3 fromSpawn = sign.transform.position - labelFocus;
        fromSpawn.y = 0f;
        if (fromSpawn.sqrMagnitude > 0.01f)
            sign.transform.rotation = Quaternion.LookRotation(fromSpawn, Vector3.up);

        // The frame is a slightly larger, thinner slab behind the board, so only its rim shows as a glowing border.
        RimPiece(sign, "Frame", Vector3.zero, new Vector3(boardWidth + 0.12f, boardHeight + 0.12f, 0.06f), SignAccent);
        var glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/RespawnPlate.mat");
        if (glow != null)
            sign.transform.Find("Frame").GetComponent<Renderer>().sharedMaterial = glow;
        RimPiece(sign, "Board", Vector3.zero, new Vector3(boardWidth, boardHeight, 0.1f), new Color(0.06f, 0.08f, 0.12f));
        SignFace(sign, title, bodyLines, new Vector3(0f, 0f, -0.056f), 0f, boardWidth, boardHeight, titleHeight, lineHeight, padding);
        SignFace(sign, title, bodyLines, new Vector3(0f, 0f, 0.056f), 180f, boardWidth, boardHeight, titleHeight, lineHeight, padding);

        var proximity = sign.AddComponent<ProximityLabel>();
        SetField(proximity, "showDistance", p => p.floatValue = 16f);
        SetField(proximity, "fadeWidth", p => p.floatValue = 5f);
        SetField(proximity, "facePlayer", p => p.boolValue = true);
    }

    // Text on one face of a board. Font sizes follow the row heights (in metres), so the same routine does big zone
    // signs and small plaques.
    private static void SignFace(GameObject sign, string title, string[] bodyLines, Vector3 localPosition, float yaw, float width, float height, float titleHeight, float lineHeight, float padding)
    {
        // Canvas units to metres. Small, so the fonts are rendered big and stay crisp when you swim right up to the board.
        const float scale = 0.004f;
        int titleFont = Mathf.RoundToInt(titleHeight / scale * 0.8f);
        int bodyFont = Mathf.RoundToInt(lineHeight / scale * 0.8f);

        var face = new GameObject("Face", typeof(RectTransform));
        face.transform.SetParent(sign.transform, false);
        face.transform.localPosition = localPosition;
        face.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        face.transform.localScale = Vector3.one * scale;
        var canvas = face.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        face.AddComponent<CanvasGroup>();
        face.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);

        float pad = padding / scale;
        float titleRows = titleHeight / scale;

        RectTransform titleRect = SignText(face.transform, "Title", title.ToUpperInvariant(), titleFont, GameFont.Bold, SignAccent, TextAnchor.MiddleCenter);
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -pad);
        titleRect.sizeDelta = new Vector2(-2f * pad, titleRows);

        if (bodyLines.Length == 0)
            return;

        RectTransform bodyRect = SignText(face.transform, "Body", string.Join("\n", bodyLines), bodyFont, FontStyle.Normal, LabelColor, TextAnchor.UpperCenter);
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(pad, pad);
        bodyRect.offsetMax = new Vector2(-pad, -(pad + titleRows));
    }

    private static RectTransform SignText(Transform face, string name, string content, int fontSize, FontStyle style, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(face, false);

        var text = go.AddComponent<Text>();
        text.text = content;
        text.font = labelFont;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = anchor;
        text.color = color;
        text.lineSpacing = 1.05f;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(3f, -3f);

        return go.GetComponent<RectTransform>();
    }

    // A tinted patch on the floor marking a zone's area.
    internal static void Patch(Transform zone, Vector2 centre, Vector2 size, Color color)
    {
        GameObject patch = Box("Patch", new Vector3(centre.x, 0.01f, centre.y), new Vector3(size.x, 0.02f, size.y), color, zone);
        Object.DestroyImmediate(patch.GetComponent<Collider>());
    }

    private static string[] Wrap(string text, int maxChars)
    {
        var lines = new System.Collections.Generic.List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = string.Empty;
            foreach (string word in paragraph.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > maxChars)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = line.Length == 0 ? word : line + " " + word;
                }
            }
            lines.Add(line);
        }
        return lines.ToArray();
    }

    // A Fish School object with the fish placed as prefab-linked children in a ring, so they show in the editor and can be tuned.
    // The wandering fish swim in proper swarms: any school of them placed with fish in it gets 8 to 12.
    internal const int SwarmMin = 8, SwarmMax = 12;

    internal static FishSchool CreateSchool(string prefabName, int count, Vector3 position, Transform parent)
    {
        var school = new GameObject("FishSchool_" + prefabName);
        school.transform.SetParent(parent, false);
        school.transform.position = position;
        var pack = school.AddComponent<FishSchool>();

        if (count > 0 && prefabName == "Fish_Wanderer")
            count = Random.Range(SwarmMin, SwarmMax + 1);
        // A loose ball round the middle (a sunflower spiral, a little up and down), not a ring.
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.39996f;
            float radius = 0.5f + 1.4f * Mathf.Sqrt((i + 0.5f) / count);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, ((i * 7) % 5 - 2) * 0.18f, Mathf.Sin(angle) * radius);
            Spawn(prefabName, position + offset, school.transform, 90f);
        }
        return pack;
    }

    // The ship's look in the arena (Lighting Tools): the high sun and the ocean surface overhead (the arena is open to
    // the sky, so you see them looking up), the water light through the fish room windows, a cool fill down the roofed
    // chase corridor, and warm accents on the puzzles and the pickup shelf.
    private static void BuildLighting()
    {
        Transform lights = Group("Lights");
        SunAndSurface(arenaRoot);

        foreach (FishWindow window in arenaRoot.GetComponentsInChildren<FishWindow>(true))
            if (Mathf.Abs(window.transform.forward.y) < 0.3f)
                WindowBeam(window.transform, true, lights);

        var fill = new Color(0.42f, 0.66f, 0.95f);
        foreach (var (name, x, z) in new[] { ("Hall_W", -4f, 33.25f), ("Hall_Mid", 8f, 33.25f), ("Hall_E", 20f, 33.25f), ("Room", 29.75f, 35.25f), ("Corridor_S", 30.25f, 45f), ("Corridor_N", 30.25f, 56f) })
            RoomLight("Light_Chase_" + name, new Vector3(x, 3.2f, z), fill, 0.85f, 14f, lights);

        foreach (var (name, target, from) in new[]
        {
            ("Pedestal", new Vector3(0f, 1.2f, 6f), new Vector3(0f, 4.6f, 4f)),
            ("Seaweed", new Vector3(-5f, 1f, 8f), new Vector3(-5f, 4.6f, 5.8f)),
            ("BoneLock", new Vector3(2.1f, 1.2f, 13.4f), new Vector3(2.1f, 4.2f, 11.4f)),
            ("RuneLock", new Vector3(6.3f, 1.6f, 13.4f), new Vector3(6.3f, 4.2f, 11.2f)),
            ("PickupShelf", new Vector3(18f, 1.25f, -20f), new Vector3(18f, 5.5f, -17f)),
        })
            Accent(name, target, from, lights);
    }

    // Ship windows onto open water: the FishWindow prefabs cut their own holes in the north wall; a dark seabed and a few
    // rocks outside give depth. Fish spawn deep below the sill just outside the hull, rise into view and swim in.
    private static void BuildFishWindows(Transform zone, FishSchool school)
    {
        var spawner = new GameObject("FishSpawner_Windows");
        spawner.transform.SetParent(zone, false);
        spawner.transform.position = new Vector3(-19f, 2.5f, 24f);

        Color seabed = new Color(0.05f, 0.08f, 0.1f);
        float wallInner = 30f;
        float wallOuter = 31f;

        // Outside the hull.
        Box("Outside_Seabed", new Vector3(-19f, -6f, wallOuter + 10f), new Vector3(40f, 0.2f, 20f), seabed, zone);
        Box("Outside_Rock", new Vector3(-27f, -3.5f, wallOuter + 7f), new Vector3(4f, 5f, 3f), seabed, zone);
        Box("Outside_Rock", new Vector3(-19f, -4.5f, wallOuter + 11f), new Vector3(6f, 3f, 4f), seabed, zone);
        Box("Outside_Rock", new Vector3(-10f, -2f, wallOuter + 9f), new Vector3(3f, 8f, 3f), seabed, zone);

        // One FishWindow prefab per window, as children of the spawner: it picks them up by itself, and Ctrl+D adds more.
        // Each one cuts its own hole through the wall box behind it.
        GameObject windowPrefab = EnsureFishWindowPrefab();
        for (int i = 0; i < WindowXs.Length; i++)
        {
            var window = (GameObject)PrefabUtility.InstantiatePrefab(windowPrefab, zone.gameObject.scene);
            window.transform.SetParent(spawner.transform, true);
            window.transform.SetPositionAndRotation(new Vector3(WindowXs[i], WindowY, wallInner), Quaternion.Euler(0f, 180f, 0f));
            window.name = "FishWindow_" + (i + 1);
            window.GetComponent<FishWindow>().CutHole(false);
        }

        // The pack only comes in while the player is inside the fish room; the trigger fills the room.
        var area = spawner.AddComponent<BoxCollider>();
        area.isTrigger = true;
        area.center = new Vector3(-18.5f, 5f, 19f) - spawner.transform.position;
        area.size = new Vector3(22f, 10f, 22f);

        var component = spawner.AddComponent<FishSpawner>();
        var so = new SerializedObject(component);
        so.FindProperty("fishPrefab").objectReferenceValue = FindPrefab("Fish_Wanderer");
        so.FindProperty("count").intValue = 10;   // a swarm (8 to 12)
        so.FindProperty("joinSchool").objectReferenceValue = school;
        so.FindProperty("startMode").enumValueIndex = (int)FishSpawner.StartMode.PlayerEntersTrigger;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Root = the opening (blue arrow into the room) with Fish Window and its entry path; children = a rim around the hole.
    // The hole itself is cut in the wall by the level; the prefab sits on the room-side face over it.
    internal static GameObject EnsureFishWindowPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(FishWindowPrefabPath);
        if (existing != null)
        {
            UseWindowModel();
            return AssetDatabase.LoadAssetAtPath<GameObject>(FishWindowPrefabPath);
        }

        var root = new GameObject("FishWindow_Placeholder");
        root.AddComponent<FishWindow>();

        Color rim = new Color(0.22f, 0.2f, 0.18f);
        float t = 0.25f;
        float halfW = WindowWidth * 0.5f;
        float halfH = WindowHeight * 0.5f;
        RimPiece(root, "Sill", new Vector3(0f, -halfH - t * 0.5f, 0.1f), new Vector3(WindowWidth + t * 2f, t, 0.3f), rim);
        RimPiece(root, "Lintel", new Vector3(0f, halfH + t * 0.5f, 0.1f), new Vector3(WindowWidth + t * 2f, t, 0.3f), rim);
        RimPiece(root, "Jamb_L", new Vector3(-halfW - t * 0.5f, 0f, 0.1f), new Vector3(t, WindowHeight, 0.3f), rim);
        RimPiece(root, "Jamb_R", new Vector3(halfW + t * 0.5f, 0f, 0.1f), new Vector3(t, WindowHeight, 0.3f), rim);

        SavePrefab(root, FishWindowPrefabPath);
        Debug.Log("Created " + FishWindowPrefabPath);
        UseWindowModel();
        return AssetDatabase.LoadAssetAtPath<GameObject>(FishWindowPrefabPath);
    }

    // The porthole (Art/Models/environment/window: a round frame round glass with a hole broken in it). Put on the
    // Fish Window prefab once, in place of the placeholder rim: turned to face along the window's arrow, glass to the
    // outside, its middle on the window's origin, scaled so the square hole cut in the wall hides behind the frame's
    // ring. The frame and the glass get colliders (solid to swim into, and what Fish Window measures its passage
    // against, so fish only come through the broken hole), plus an invisible Player Blocker over the whole opening so
    // the player cannot squeeze out through the hole. Already done, or no model yet: nothing happens.
    private const string WindowModelPath = "Assets/Art/Models/environment/window/window.obj";
    private const float WindowModelScale = 1.1f;
    private const float WindowModelMiddle = 2.2f;   // height of the porthole's middle in the model, in its units
    // The porthole ring, in model units: its inside edge (the flat sides of the octagon) and its outside edge.
    private const float WindowRingInner = 1.11f;
    private const float WindowRingOuter = 1.442f;
    // The wall cut: just bigger than the ring's opening so its edges hide behind the ring; its corners, which stick out
    // past the ring, are filled in by Fish Window (Frame Edge).
    private static readonly Vector2 WindowCut = Vector2.one * (WindowRingInner * WindowModelScale * 2f + 0.1f);
    private const string WindowGlassPath = "Assets/Art/Materials/WindowGlass.mat";
    private const string WindowFramePath = "Assets/Art/Materials/WindowFrame.mat";

    private static void UseWindowModel()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(WindowModelPath);
        if (model == null)
            return;
        GameObject root = PrefabUtility.LoadPrefabContents(FishWindowPrefabPath);
        try
        {
            Transform existing = root.transform.Find("Model");
            GameObject porthole;
            if (existing != null)
            {
                porthole = existing.gameObject;
            }
            else
            {
                foreach (string rim in new[] { "Sill", "Lintel", "Jamb_L", "Jamb_R" })
                {
                    Transform piece = root.transform.Find(rim);
                    if (piece != null)
                        Object.DestroyImmediate(piece.gameObject);
                }
                porthole = Object.Instantiate(model, root.transform);
                porthole.name = "Model";
                porthole.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);   // the model's thickness (x) along the arrow
                porthole.transform.localScale = Vector3.one * WindowModelScale;
                porthole.transform.localPosition = new Vector3(0f, -WindowModelMiddle * WindowModelScale, 0f);
                foreach (MeshFilter filter in porthole.GetComponentsInChildren<MeshFilter>())
                    if (filter.sharedMesh != null)
                        filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            }

            // The glass is the smallest part (its own slot when the model is one mesh with a slot per material, as the
            // .obj imports; else its own mesh); it gets the see-through material, everything else the dark metal.
            Mesh smallest = null;
            foreach (MeshFilter filter in porthole.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && (smallest == null || filter.sharedMesh.vertexCount < smallest.vertexCount))
                    smallest = filter.sharedMesh;
            foreach (MeshRenderer renderer in porthole.GetComponentsInChildren<MeshRenderer>())
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>() != null ? renderer.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh == null)
                    continue;
                var materials = new Material[mesh.subMeshCount];
                int glassSlot = -1;
                if (mesh.subMeshCount > 1)
                {
                    glassSlot = 0;
                    for (int i = 1; i < mesh.subMeshCount; i++)
                        if (mesh.GetIndexCount(i) < mesh.GetIndexCount(glassSlot))
                            glassSlot = i;
                }
                else if (mesh == smallest && porthole.GetComponentsInChildren<MeshFilter>().Length > 1)
                {
                    glassSlot = 0;
                }
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = i == glassSlot ? WindowGlassMaterial() : WindowFrameMaterial();
                renderer.sharedMaterials = materials;
            }

            Transform blockerTransform = root.transform.Find(FishWindow.PlayerBlockerName);
            GameObject blocker = blockerTransform != null ? blockerTransform.gameObject : new GameObject(FishWindow.PlayerBlockerName);
            blocker.transform.SetParent(root.transform, false);
            BoxCollider box = blocker.GetComponent<BoxCollider>();
            if (box == null)
                box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(WindowCut.x, WindowCut.y, 0.15f);
            box.center = new Vector3(0f, 0f, -0.1f);

            var window = root.GetComponent<FishWindow>();
            if (window != null)
            {
                var so = new SerializedObject(window);
                so.FindProperty("holeSize").vector2Value = WindowCut;
                so.FindProperty("frameEdge").floatValue = WindowRingOuter * WindowModelScale;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, FishWindowPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // WindowGlass.mat: URP Lit, see-through sea green and glossy (the model's own glass colour), saved once.
    private static Material WindowGlassMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(WindowGlassPath);
        if (material != null)
            return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WindowGlass" };
        material.SetColor("_BaseColor", new Color(0.1f, 0.75f, 0.72f, 0.25f));
        material.SetFloat("_Smoothness", 0.92f);
        material.SetFloat("_Surface", 1f);   // transparent
        material.SetFloat("_Blend", 0f);     // alpha
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, WindowGlassPath);
        return material;
    }

    // WindowFrame.mat: URP Lit, dark worn metal, saved once.
    private static Material WindowFrameMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(WindowFramePath);
        if (material != null)
            return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "WindowFrame" };
        material.SetColor("_BaseColor", new Color(0.08f, 0.085f, 0.09f));
        material.SetFloat("_Metallic", 0.6f);
        material.SetFloat("_Smoothness", 0.45f);
        AssetDatabase.CreateAsset(material, WindowFramePath);
        return material;
    }

    internal static void RimPiece(GameObject parent, string name, Vector3 localPosition, Vector3 size, Color color)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = name;
        piece.transform.SetParent(parent.transform, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = size;
        piece.AddComponent<RendererTint>().Tint = color;
    }

    // Saves a freshly built prefab and always removes the scene copy, even when saving fails, so a failed build never
    // leaves loose *_Placeholder objects behind in the scene.
    private static GameObject SavePrefab(GameObject root, string path)
    {
        try
        {
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Loose *_Placeholder objects at the scene root that are not prefab instances: leftovers of a prefab build that
    // failed part way. Both builders clear them before building.
    internal static void RemoveStrayPlaceholders(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name.EndsWith("_Placeholder", System.StringComparison.OrdinalIgnoreCase) && !PrefabUtility.IsPartOfPrefabInstance(root))
                Object.DestroyImmediate(root);
    }

    internal static GameObject Spawn(string prefabName, Vector3 position, Transform parent, float yaw = 0f)
    {
        GameObject prefab = FindPrefab(prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"{buildName}: no prefab named '{prefabName}' under {PrefabRoot}, skipped.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return instance;
    }

    internal static GameObject FindPrefab(string name) => InteractablePrefabTools.FindPrefab(name);
    private static ParticleSystem FindSparkle() => InteractablePrefabTools.FindSparkle();

    // Uses Pickup_Placeholder.prefab when the team has made one, otherwise builds the same thing from primitives.
    // Which look of its item a pickup shows: 0 = the item's World Model, 1, 2... = its World Model Variants.
    internal static void PickupVariant(GameObject pickup, int variant)
    {
        var item = pickup != null ? pickup.GetComponent<PickupItem>() : null;
        if (item != null)
            SetField(item, "modelVariant", p => p.intValue = variant);
    }

    internal static GameObject CreatePickup(Vector3 position, string itemAssetName, Transform parent)
    {
        GameObject root;
        GameObject prefab = FindPrefab("Pickup_Placeholder");
        if (prefab != null)
        {
            root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
            root.transform.SetParent(parent, true);
            root.transform.position = position;
        }
        else
        {
            root = new GameObject("Pickup_Placeholder");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.AddComponent<BoxCollider>().size = Vector3.one * 0.35f;
            root.AddComponent<PickupItem>();
            root.AddComponent<InteractableHighlight>();
            var indicator = root.AddComponent<InteractableIndicator>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.34f, 0.45f, 0.02f);   // a dog-photo plaque, like the prefab
            Material photo = EnsurePhotoMaterial();
            if (photo != null)
                visual.GetComponent<Renderer>().sharedMaterial = photo;

            ParticleSystem sparkle = FindSparkle();
            if (sparkle != null)
                SetField(indicator, "effectPrefab", p => p.objectReferenceValue = sparkle);
        }

        ItemDefinition item = ItemTools.Load(itemAssetName);
        var pickup = root.GetComponentInChildren<PickupItem>();
        if (pickup != null)
            SetField(pickup, "pickupSound", p => { if (p.objectReferenceValue == null) p.objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/Items/Pickup_MagicKeyJingle_Tunetank.wav"); });
        if (pickup != null && item != null)
            SetField(pickup, "item", p => p.objectReferenceValue = item);
        root.name = "Pickup_" + itemAssetName.Replace("Item_", "");
        return root;
    }


    internal static void SetField(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"{buildName}: '{target.GetType().Name}' has no field '{field}', skipped.");
            return;
        }
        set(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

// The two puzzles the game ships with, as assets an artist can fill: Tools > Out of the Depths > Create Puzzle Assets
// draws placeholder pictures into Art/UI/Puzzle (the board, a slot, a tile frame, the rune tiles, the three tablet
// pieces) and makes Assets/Puzzles/Puzzle_StoneTablet.asset and Puzzle_Runes.asset from them; nothing already
// there is touched, so replacing a PNG or a sprite on the asset is all the real art needs. AddStation puts a
// PuzzleStation on a scene object for the builders; SolveOpens / SolveUnlocks wire what solving it does.
// (It lives in this file because Unity kept leaving a file of its own out of the editor assembly.)
public static class PuzzleBuildTools
{
    public const string PuzzleFolder = "Assets/Puzzles";
    public const string ArtFolder = "Assets/Art/UI/Puzzle";

    private static readonly Color Parchment = new Color(0.93f, 0.87f, 0.7f);
    private static readonly Color Ink = new Color(0.2f, 0.16f, 0.12f);

    [MenuItem("Tools/Out of the Depths/Create Puzzle Assets")]
    public static void CreateAssetsMenu() => EnsureAssets(true);

    public static void EnsureAssets(bool verbose)
    {
        Folder("Assets/Art/UI");
        Folder(ArtFolder);
        Folder(PuzzleFolder);

        Sprite board = Painted("Board", 480, 300, Rounded(480, 300, 40f, 14f, new Color(0.45f, 0.3f, 0.15f), new Color(0.24f, 0.14f, 0.07f)), 56);
        Sprite slot = Painted("Slot", 128, 128, Rounded(128, 128, 18f, 6f, new Color(0.2f, 0.13f, 0.07f), new Color(0.1f, 0.06f, 0.03f)), 24);
        Sprite frame = Painted("TileFrame", 128, 128, Rounded(128, 128, 12f, 5f, Parchment, new Color(0.58f, 0.47f, 0.32f)), 20);

        // The three symbols painted about the ship (their colours), and five that are not.
        Sprite triangle = Glyph("Rune_Triangle", (u, v) => v >= -0.55f && Mathf.Abs(u) <= 0.62f * (0.6f - v) / 1.15f, new Color(0.9f, 0.15f, 0.1f));
        Sprite square = Glyph("Rune_Square", (u, v) => Mathf.Abs(u) <= 0.52f && Mathf.Abs(v) <= 0.52f, new Color(0.15f, 0.2f, 0.9f));
        Sprite spiral = Glyph("Rune_Spiral", (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float turn = (Mathf.Atan2(v, u) + Mathf.PI) / (2f * Mathf.PI);
            for (int k = 0; k < 2; k++)
                if (Mathf.Abs(r - (0.12f + 0.28f * (k + turn))) < 0.085f && r < 0.72f)
                    return true;
            return false;
        }, new Color(0.2f, 0.85f, 0.35f));
        Sprite circle = Glyph("Rune_Circle", (u, v) => { float r = Mathf.Sqrt(u * u + v * v); return r >= 0.44f && r <= 0.62f; }, Ink);
        Sprite cross = Glyph("Rune_Cross", (u, v) => (Mathf.Abs(u) <= 0.14f && Mathf.Abs(v) <= 0.62f) || (Mathf.Abs(v) <= 0.14f && Mathf.Abs(u) <= 0.62f), Ink);
        Sprite bars = Glyph("Rune_Bars", (u, v) => Mathf.Abs(v) <= 0.58f && (Mathf.Abs(u + 0.45f) <= 0.12f || Mathf.Abs(u) <= 0.12f || Mathf.Abs(u - 0.45f) <= 0.12f), Ink);
        Sprite dots = Glyph("Rune_Dots", (u, v) =>
        {
            foreach (float i in new[] { -0.42f, 0f, 0.42f })
                foreach (float j in new[] { -0.42f, 0f, 0.42f })
                    if ((u - i) * (u - i) + (v - j) * (v - j) <= 0.13f * 0.13f)
                        return true;
            return false;
        }, Ink);
        Sprite diamond = Glyph("Rune_Diamond", (u, v) => Mathf.Abs(u) + Mathf.Abs(v) <= 0.62f, Ink);

        // The stone tablet (Noora's drawing of it whole, shown dark as the shape to fill) and its three pieces, cut to
        // their own size out of her full-size drawings (Art/UI: full stone, stone1-3); the spots below are where each
        // piece sits in that drawing. The older disc drawing (Stone_Disc, Stone_Piece1-3) stands in if they are gone.
        Sprite stoneDisc = Art("Tablet_Full") ?? Art("Stone_Disc");
        Sprite stonePiece1 = Art("Tablet_Piece2") ?? Art("Stone_Piece1");
        Sprite stonePiece2 = Art("Tablet_Piece1") ?? Art("Stone_Piece2");
        Sprite stonePiece3 = Art("Tablet_Piece3") ?? Art("Stone_Piece3");

        bool made = false;
        made |= Puzzle("Puzzle_StoneTablet", p =>
        {
            p.title = "Piece the tablet together";
            p.hint = "Drag each fragment into its place in the stone, or click it to send it there. Click a placed one to take it back.";
            p.solvedText = "The tablet is whole.";
            p.board = board;
            p.slot = slot;
            p.tileFrame = frame;
            p.layout = PuzzleDefinition.Layout.Picture;
            // Noora's mockup: no board, the podium (Art/UI/podium) drawn as it is on the left over the dimmed game,
            // the stone dark in its hollow (the silhouettes of the pieces), the three fragments on the right as the
            // stone broken apart. The pieces were drawn on the podium's own canvas, so the spots are where they sit
            // on it (shares of the picture's height). Without the podium, the older disc drawing.
            Sprite podium = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/podium.png");
            p.textColor = new Color(0.85f, 0.92f, 1f);
            p.looseScale = 0.95f;
            if (podium != null)
            {
                p.boardTint = new Color(1f, 1f, 1f, 0f);
                p.boardSize = new Vector2(1920f, 1080f);
                p.backdropDim = 0.45f;
                p.picture = podium;
                p.pictureSize = 1000f;
                p.pictureTint = Color.white;
                p.pictureOffset = new Vector2(128f, -123f);
                p.showSilhouettes = true;
                p.silhouetteColor = new Color(0.36f, 0.4f, 0.46f, 1f);
                p.looseExploded = true;
                p.looseCentre = new Vector2(470f, 150f);
                p.looseSpread = 1.3f;
                p.spots = new[]
                {
                    new PuzzleDefinition.Spot { center = new Vector2(0.0919f, 0.1756f), size = new Vector2(0.1847f, 0.3183f) },
                    new PuzzleDefinition.Spot { center = new Vector2(-0.048f, 0.098f), size = new Vector2(0.2725f, 0.2591f) },
                    new PuzzleDefinition.Spot { center = new Vector2(-0.0257f, 0.3082f), size = new Vector2(0.3023f, 0.2314f) },
                };
            }
            else
            {
                p.picture = stoneDisc;
                p.pictureSize = 520f;
                p.pictureTint = new Color(0.28f, 0.3f, 0.34f);
                p.showSilhouettes = false;
                p.looseScale = 0.62f;
                p.spots = new[]
                {
                    new PuzzleDefinition.Spot { center = new Vector2(0.2018f, -0.0451f), size = new Vector2(0.4055f, 0.699f) },
                    new PuzzleDefinition.Spot { center = new Vector2(-0.1054f, -0.2156f), size = new Vector2(0.5983f, 0.5689f) },
                    new PuzzleDefinition.Spot { center = new Vector2(-0.0565f, 0.246f), size = new Vector2(0.6638f, 0.5081f) },
                };
            }
            p.tiles = new[]
            {
                new PuzzleDefinition.Tile { id = "right", art = stonePiece1, label = "right", looseAngle = 8f },
                new PuzzleDefinition.Tile { id = "bottom", art = stonePiece2, label = "bottom", looseAngle = -6f },
                new PuzzleDefinition.Tile { id = "top", art = stonePiece3, label = "top", looseAngle = 10f },
            };
            p.solution = new[] { "right", "bottom", "top" };
        });
        made |= Puzzle("Puzzle_Runes", p =>
        {
            p.title = "Enter the runes";
            p.hint = "The three symbols painted about the ship, in the order you found them. Drag or click the tiles into the slots.";
            p.solvedText = "The lock turns.";
            p.board = board;
            p.slot = slot;
            p.tileFrame = frame;
            p.tiles = new[]
            {
                new PuzzleDefinition.Tile { id = "symbol1", art = triangle, label = "1" },
                new PuzzleDefinition.Tile { id = "symbol2", art = square, label = "2" },
                new PuzzleDefinition.Tile { id = "symbol3", art = spiral, label = "3" },
                new PuzzleDefinition.Tile { id = "circle", art = circle, label = "o" },
                new PuzzleDefinition.Tile { id = "cross", art = cross, label = "+" },
                new PuzzleDefinition.Tile { id = "bars", art = bars, label = "|||" },
                new PuzzleDefinition.Tile { id = "dots", art = dots, label = ":::" },
                new PuzzleDefinition.Tile { id = "diamond", art = diamond, label = "<>" },
            };
            p.solution = new[] { "symbol1", "symbol2", "symbol3" };
        });
        if (made)
            AssetDatabase.SaveAssets();
        if (verbose)
            Debug.Log($"Puzzle assets are in {PuzzleFolder}, their placeholder pictures in {ArtFolder}. Drop the real sprites onto the puzzle assets, or replace the PNGs.");
    }

    // ---- for the builders --------------------------------------------------------------------------------------------

    // A puzzle station on a scene object (it needs a collider): E opens the puzzle; requiredItem x amount must be in
    // the inventory first (null = nothing), consumed on solving if consume.
    internal static PuzzleStation AddStation(GameObject host, string puzzleAsset, string requiredItem, int amount, bool consume, string prompt)
    {
        EnsureAssets(false);
        var station = host.AddComponent<PuzzleStation>();
        if (host.GetComponent<InteractableHighlight>() == null)
            host.AddComponent<InteractableHighlight>();
        var puzzle = AssetDatabase.LoadAssetAtPath<PuzzleDefinition>($"{PuzzleFolder}/{puzzleAsset}.asset");
        TestArenaBuilder.SetField(station, "puzzle", p => p.objectReferenceValue = puzzle);
        TestArenaBuilder.SetField(station, "prompt", p => p.stringValue = prompt);
        if (!string.IsNullOrEmpty(requiredItem))
        {
            TestArenaBuilder.SetField(station, "requiredItem", p => p.objectReferenceValue = ItemTools.Load(requiredItem));
            TestArenaBuilder.SetField(station, "requiredAmount", p => p.intValue = amount);
            TestArenaBuilder.SetField(station, "consume", p => p.boolValue = consume);
        }
        return station;
    }

    internal static void SolveOpens(PuzzleStation station, Door door) => UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(station.onSolved, door.Open);
    internal static void SolveUnlocks(PuzzleStation station, Door door) => UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(station.onSolved, door.Unlock);

    // ---- making the assets -------------------------------------------------------------------------------------------

    private static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    // A puzzle asset, made only if there is none of that name.
    private static bool Puzzle(string name, System.Action<PuzzleDefinition> fill)
    {
        string path = $"{PuzzleFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<PuzzleDefinition>(path) != null)
            return false;
        var puzzle = ScriptableObject.CreateInstance<PuzzleDefinition>();
        fill(puzzle);
        AssetDatabase.CreateAsset(puzzle, path);
        Debug.Log("Created " + path);
        return true;
    }

    // A picture that comes with the project (the stone disc and pieces), or null if it has gone.
    private static Sprite Art(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{file}.png");

    // A rune: the shape in u, v (-1..1 across the tile) in one colour on nothing.
    private static Sprite Glyph(string file, System.Func<float, float, bool> inside, Color color)
    {
        return Painted(file, 128, 128, (x, y) => inside((x + 0.5f) / 64f - 1f, (y + 0.5f) / 64f - 1f) ? color : (Color?)null, 0);
    }

    // A rounded rectangle with a rim, in pixels.
    private static System.Func<int, int, Color?> Rounded(int width, int height, float radius, float rim, Color fill, Color rimColor)
    {
        return (x, y) =>
        {
            float px = Mathf.Abs(x + 0.5f - width * 0.5f) - (width * 0.5f - radius);
            float py = Mathf.Abs(y + 0.5f - height * 0.5f) - (height * 0.5f - radius);
            float d = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
            if (d > 0f)
                return null;
            return d > -rim ? rimColor : fill;
        };
    }

    // A PNG painted pixel by pixel and imported as a sprite (border = 9-slice edges); an existing file is kept.
    private static Sprite Painted(string file, int width, int height, System.Func<int, int, Color?> paint, int border)
    {
        string path = $"{ArtFolder}/{file}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null)
            return existing;

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = paint(x, y) ?? Color.clear;
        texture.SetPixels(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(border, border, border, border);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
