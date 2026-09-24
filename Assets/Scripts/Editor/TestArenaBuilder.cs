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
        light.intensity = 4f;
        light.color = new Color(1f, 0.95f, 0.85f);
    }

    // DogPhoto.mat: URP Lit with the photo as base map plus a faint emissive copy so it reads in dim water.
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
        material.SetColor("_EmissionColor", Color.white * 0.35f);
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
        Label("PICKUPS - E to take, 1-5 / wheel selects a slot", new Vector3(18f, 3.4f, -24f), zone);   // behind the shelf, not over the pickups
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

        // Pedestal: with the 3 stone fragments on you, E opens the puzzle board; piecing the tablet together opens the left door.
        GameObject pedestal = Box("Pedestal", new Vector3(0f, 0.6f, 6f), new Vector3(1.2f, 1.2f, 1.2f), PropColor, zone);
        PuzzleBuildTools.SolveOpens(PuzzleBuildTools.AddStation(pedestal, "Puzzle_StoneTablet", "Item_StoneFragment", 3, true, "piece the tablet together"), doorStone);

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
        lockPlate.transform.localPosition = new Vector3(-2.1f, 1.4f, 0.13f);
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
        FramePiece("Frame_Post", centre, rotation, new Vector3(-2.6f, 2.2f, 0f), new Vector3(0.4f, 4.4f, 0.5f), parent);
        FramePiece("Frame_Post", centre, rotation, new Vector3(2.6f, 2.2f, 0f), new Vector3(0.4f, 4.4f, 0.5f), parent);
        FramePiece("Frame_Top", centre, rotation, new Vector3(0f, 4.2f, 0f), new Vector3(5.6f, 0.4f, 0.5f), parent);
        return door;
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
        // One picture per piece: the fragment's own icon for the first, icons rendered from the other two piece models
        // (Item_BoneKeyFragment_Icon_2/3.png) for the rest; a missing render falls back to the fragment's icon.
        ItemDefinition fragment = ItemTools.Load("Item_BoneKeyFragment");
        var pieces = new Sprite[3];
        pieces[0] = fragment != null ? fragment.Icon : null;
        for (int i = 1; i < 3; i++)
        {
            GameObject pieceModel = ItemModelTools.FindModel("bone_key_piece" + (i + 1));
            pieces[i] = pieceModel != null ? ItemModelTools.RenderIcon(pieceModel, "Item_BoneKeyFragment_" + (i + 1)) : null;
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
    internal static Door SpawnDoor(string name, Vector3 hinge, bool locked, bool closeBehind, Transform parent, float yaw = 0f)
    {
        GameObject prefab = EnsureDoorPrefab();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, true);
        Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
        instance.transform.SetPositionAndRotation(hinge, rotation);
        instance.name = name;

        var door = instance.GetComponent<Door>();
        SetField(door, "locked", p => p.boolValue = locked);
        SetField(door, "closeBehindPlayer", p => p.boolValue = closeBehind);

        FramePiece("Frame_Post", hinge, rotation, new Vector3(-0.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.5f), parent);
        FramePiece("Frame_Post", hinge, rotation, new Vector3(2.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.5f), parent);
        FramePiece("Frame_Top", hinge, rotation, new Vector3(1f, 3.2f, 0f), new Vector3(2.8f, 0.4f, 0.5f), parent);   // as deep as a wall, so no cut face shows beside the frame
        return door;
    }

    private static void FramePiece(string name, Vector3 hinge, Quaternion rotation, Vector3 offset, Vector3 size, Transform parent)
    {
        GameObject piece = Box(name, hinge + rotation * offset, size, PropColor, parent);
        piece.transform.rotation = rotation;
    }

    // ---- chase (GDD map room 9: the hallway, the room after it, the corridor where the rubble comes down) -----------

    internal static AudioClip Sfx(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + file);

    // A box without a collider: murals and other set dressing nothing should bump into.
    internal static GameObject Decor(string name, Vector3 center, Vector3 size, Color color, Transform parent)
    {
        GameObject box = Box(name, center, size, color, parent);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
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
        Vector3[] spots =
        {
            new Vector3(-1.5f, 0.75f, 0.3f), new Vector3(0f, 0.8f, -0.2f), new Vector3(1.5f, 0.7f, 0.2f),
            new Vector3(-0.9f, 2.1f, -0.1f), new Vector3(0.7f, 2.2f, 0.3f),
            new Vector3(-0.2f, 3.5f, 0f), new Vector3(1.4f, 3.4f, -0.3f), new Vector3(-1.6f, 3.6f, 0.2f),
        };
        Vector3[] sizes =
        {
            new Vector3(1.7f, 1.5f, 1.6f), new Vector3(1.8f, 1.6f, 1.7f), new Vector3(1.6f, 1.4f, 1.5f),
            new Vector3(1.5f, 1.4f, 1.4f), new Vector3(1.6f, 1.5f, 1.5f),
            new Vector3(1.5f, 1.3f, 1.4f), new Vector3(1.3f, 1.2f, 1.3f), new Vector3(1.2f, 1.2f, 1.2f),
        };
        for (int i = 0; i < spots.Length; i++)
        {
            GameObject r = Box("Rock", rubbleGo.transform.position + spots[i], sizes[i], rock, rubbleGo.transform);
            r.transform.rotation = Quaternion.Euler(i * 17f % 30f - 15f, i * 41f % 90f, i * 23f % 30f - 15f);
        }
        var rubble = rubbleGo.AddComponent<RubbleFall>();
        SetField(rubble, "blocker", p => p.objectReferenceValue = blocker);
        SetField(rubble, "thudSound", p => p.objectReferenceValue = Sfx("Hit impact.wav"));
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

        RectTransform titleRect = SignText(face.transform, "Title", title.ToUpperInvariant(), titleFont, FontStyle.Bold, SignAccent, TextAnchor.MiddleCenter);
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
    internal static FishSchool CreateSchool(string prefabName, int count, Vector3 position, Transform parent)
    {
        var school = new GameObject("FishSchool_" + prefabName);
        school.transform.SetParent(parent, false);
        school.transform.position = position;
        var pack = school.AddComponent<FishSchool>();

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 offset = new Vector3(Mathf.Cos(angle), i % 2 == 0 ? 0.2f : -0.2f, Mathf.Sin(angle)) * 1.2f;
            Spawn(prefabName, position + offset, school.transform, 90f);
        }
        return pack;
    }

    // The ship's look in the arena (Lighting Tools): the high sun and the ocean surface overhead (the arena is open to
    // the sky, so you see them looking up), the water light through the fish room windows, a cool fill down the roofed
    // chase corridor, warm accents on the puzzles and the pickup shelf, and glowing algae in the corners.
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

        var random = new System.Random(5);
        foreach (var (x, z) in new[] { (-28f, -28f), (27f, -27f), (28f, 28f), (-28f, 28f), (-27f, 0f), (5f, 25f) })
            Algae(new Vector3(x, 0f, z), random, lights);
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
        so.FindProperty("count").intValue = 7;
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
            return existing;

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

        GameObject prefab = SavePrefab(root, FishWindowPrefabPath);
        Debug.Log("Created " + FishWindowPrefabPath);
        return prefab;
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

        // The stone tablet (the artist's drawing of it whole, shown dark as the shape to fill) and its three pieces; the
        // spots below are where each piece sits in that drawing.
        Sprite stoneDisc = Art("Stone_Disc");
        Sprite stonePiece1 = Art("Stone_Piece1");
        Sprite stonePiece2 = Art("Stone_Piece2");
        Sprite stonePiece3 = Art("Stone_Piece3");

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
            p.picture = stoneDisc;
            p.pictureSize = 520f;
            p.pictureTint = new Color(0.28f, 0.3f, 0.34f);
            p.showSilhouettes = false;
            p.looseScale = 0.62f;
            p.spots = new[]
            {
                new PuzzleDefinition.Spot { center = new Vector2(0.1983f, -0.042f), size = new Vector2(0.4059f, 0.6762f) },
                new PuzzleDefinition.Spot { center = new Vector2(-0.1052f, -0.2269f), size = new Vector2(0.5923f, 0.5387f) },
                new PuzzleDefinition.Spot { center = new Vector2(-0.0581f, 0.2385f), size = new Vector2(0.6624f, 0.5157f) },
            };
            p.tiles = new[]
            {
                new PuzzleDefinition.Tile { id = "right", art = stonePiece1, label = "right", looseAngle = 16f },
                new PuzzleDefinition.Tile { id = "bottom", art = stonePiece2, label = "bottom", looseAngle = -12f },
                new PuzzleDefinition.Tile { id = "top", art = stonePiece3, label = "top", looseAngle = 20f },
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
