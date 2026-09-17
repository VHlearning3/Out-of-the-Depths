using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Out of the Depths > Rebuild Test Arena. Keeps Player, HUD, lights and the admin panel in TestArena.unity and
// replaces the arena itself with labelled test zones built from the placeholder prefabs. Safe to run again any time.
public static class TestArenaBuilder
{
    private const string ScenePath = "Assets/Scenes/TestArena.unity";
    private const string PrefabRoot = InteractablePrefabTools.PrefabRoot;
    private const string DoorPrefabPath = PrefabRoot + "/Placeholders/Door_Placeholder.prefab";
    private const string SfxFolder = "Assets/Sound/SFX Sound effects/";

    private static readonly Vector3 SpawnPosition = new Vector3(0f, 1.6f, -22f);

    // Root objects whose names start with one of these are arena content and get rebuilt. Everything else is kept.
    private static readonly string[] ArenaPrefixes =
        { "Arena", "Floor", "Wall_", "Checkpoint_", "Hazard", "Fish", "DeadFish", "WallFish", "Pufferfish", "Pickup" };

    private static readonly Color TileA = new Color(0.16f, 0.24f, 0.30f);
    private static readonly Color TileB = new Color(0.20f, 0.29f, 0.36f);
    private static readonly Color WallColor = new Color(0.12f, 0.15f, 0.20f);
    private static readonly Color PropColor = new Color(0.36f, 0.40f, 0.46f);
    private static readonly Color LabelColor = new Color(0.8f, 0.95f, 1f);

    private static Transform arenaRoot;
    private static Font labelFont;

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
        RemoveOldArena(scene);

        arenaRoot = new GameObject("Arena").transform;
        labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildFloorAndWalls();
        BuildSpawn();
        BuildMovementCourse();
        BuildFishAndFood();
        BuildCombatPen();
        BuildHazardLane();
        BuildPickups();
        BuildPuzzles();
        BuildDoors();
        PlacePlayer();
        ItemTools.EnsureInventoryHud();
        HudLayoutTools.Apply();
        TuneWarningThresholds();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("TestArena rebuilt and saved: " + ScenePath);
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
        Box("Wall_North", new Vector3(0f, 5f, 30.5f), new Vector3(62f, 10f, 1f), WallColor, walls);
        Box("Wall_South", new Vector3(0f, 5f, -30.5f), new Vector3(62f, 10f, 1f), WallColor, walls);
        Box("Wall_East", new Vector3(30.5f, 5f, 0f), new Vector3(1f, 10f, 62f), WallColor, walls);
        Box("Wall_West", new Vector3(-30.5f, 5f, 0f), new Vector3(1f, 10f, 62f), WallColor, walls);
    }

    private static void BuildSpawn()
    {
        Transform zone = Group("Zone_Spawn");
        GameObject plate = Spawn("RespawnPlate", new Vector3(SpawnPosition.x, 0.05f, SpawnPosition.z), zone);
        if (plate != null)
        {
            plate.name = "Checkpoint_Spawn";
            var checkpoint = plate.GetComponentInChildren<Checkpoint>();
            if (checkpoint != null)
                SetField(checkpoint, "startsActivated", p => p.boolValue = true);
        }
        CreatePickup(new Vector3(2f, 1.2f, -19f), "Item_Dagger", zone);
        Label("SPAWN - take the dagger (E) to be able to slash\nadmin panel = 0", new Vector3(0f, 4f, -18f), zone);
    }

    private static void BuildMovementCourse()
    {
        Transform zone = Group("Zone_Movement");

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
        CreateSchool("Fish_Wanderer", 6, new Vector3(-19f, 2.5f, 20f), zone);
        Spawn("Fish_Wanderer", new Vector3(-24f, 1.5f, 12f), zone, 240f);
        Spawn("WallFish", new Vector3(-28.5f, 2f, 22f), zone, 90f);
        Label("FISH - a pack of 6, a loner, wall fish", new Vector3(-19f, 5.5f, 20f), zone);

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
        Label("FOOD - E to eat (one respawns after 12 s)", new Vector3(-10f, 3.5f, 11f), food);
    }

    private static void BuildCombatPen()
    {
        Transform zone = Group("Zone_Combat");
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
        Box("Lane_N", new Vector3(19f, 1.5f, 4.5f), new Vector3(22f, 3f, 0.4f), PropColor, zone);
        Box("Lane_S", new Vector3(19f, 1.5f, -6.5f), new Vector3(22f, 3f, 0.4f), PropColor, zone);
        Spawn("Hazard", new Vector3(12f, 1f, 1f), zone, 0f);
        Spawn("Hazard", new Vector3(17f, 1f, -3f), zone, 0f);
        Spawn("Hazard", new Vector3(22f, 1f, 1f), zone, 0f);

        GameObject plate = Spawn("RespawnPlate", new Vector3(27.5f, 0.05f, -1f), zone);
        if (plate != null)
            plate.name = "Checkpoint_Far";
        Label("HAZARDS - checkpoint at the end", new Vector3(19f, 4.5f, 5f), zone);
    }

    private static void BuildPickups()
    {
        Transform zone = Group("Zone_Pickups");
        Box("Shelf", new Vector3(16f, 0.5f, -20f), new Vector3(16f, 1f, 1.5f), PropColor, zone);
        string[] items =
        {
            "Item_StoneFragment", "Item_StoneFragment", "Item_StoneFragment",
            "Item_BoneKeyFragment", "Item_BoneKeyFragment", "Item_BoneKeyFragment",
            "Item_Pearl",
        };
        for (int i = 0; i < items.Length; i++)
            CreatePickup(new Vector3(10f + i * 2f, 1.25f, -20f), items[i], zone);
        Label("PICKUPS - E to take, 1-5 / wheel selects a slot", new Vector3(16f, 3.5f, -19f), zone);
        AddPickupToAdminPanel();
    }

    private static void BuildPuzzles()
    {
        Transform zone = Group("Zone_Puzzles");
        Color stone = new Color(0.7f, 0.65f, 0.5f);

        // A wall with two locked doors: the pedestal opens the left one, the bone key the right one.
        Box("PuzzleWall_L", new Vector3(-5.5f, 2f, 14f), new Vector3(3f, 4f, 0.5f), PropColor, zone);
        Box("PuzzleWall_M", new Vector3(0f, 2f, 14f), new Vector3(4f, 4f, 0.5f), PropColor, zone);
        Box("PuzzleWall_R", new Vector3(5.5f, 2f, 14f), new Vector3(3f, 4f, 0.5f), PropColor, zone);
        Door doorStone = SpawnDoor("Door_Stone", new Vector3(-4f, 0f, 14f), true, false, zone);
        Door doorBone = SpawnDoor("Door_BoneKey", new Vector3(2f, 0f, 14f), true, false, zone);

        // Pedestal: E with stone fragments on you places them automatically (GDD); 3 of them open the left door.
        GameObject pedestal = Box("Pedestal", new Vector3(0f, 0.6f, 6f), new Vector3(1.2f, 1.2f, 1.2f), PropColor, zone);
        var pieces = new GameObject[3];
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i] = Box("Placed_Stone_" + (i + 1), new Vector3(-0.35f + i * 0.35f, 1.35f, 6f), new Vector3(0.25f, 0.3f, 0.25f), stone, zone);
            pieces[i].SetActive(false);
        }
        OpenOnFilled(Socket(pedestal, "Item_StoneFragment", 3, true, "place", null, pieces), doorStone);

        // Seaweed: 3 bone key fragments + E = a bone key (the GDD's tie-them-together step).
        GameObject seaweed = Box("Seaweed", new Vector3(-5f, 1.2f, 8f), new Vector3(0.6f, 2.4f, 0.6f), new Color(0.2f, 0.55f, 0.25f), zone);
        Socket(seaweed, "Item_BoneKeyFragment", 3, true, "tie", "Item_BoneKey", null);

        // Lock: the bone key opens the right door.
        GameObject lockBox = Box("Lock_BoneKey", new Vector3(4.6f, 1.2f, 13.4f), new Vector3(0.5f, 0.5f, 0.3f), new Color(0.8f, 0.7f, 0.3f), zone);
        OpenOnFilled(Socket(lockBox, "Item_BoneKey", 1, true, "unlock with", null, null), doorBone);

        Label("PUZZLES\npedestal: 3 stone fragments -> left door\nseaweed: 3 bone fragments -> bone key -> right door", new Vector3(0f, 6f, 12f), zone);
    }

    private static void BuildDoors()
    {
        Transform zone = Group("Zone_Doors");
        Door swing = SpawnDoor("Door_Swing", new Vector3(6f, 0f, -12f), false, false, zone);
        SetField(swing, "motion", p => p.enumValueIndex = (int)Door.Motion.Swing);
        SpawnDoor("Door_ClosesBehind", new Vector3(10f, 0f, -12f), false, true, zone);
        Label("DOORS - left: E opens / closes (swings)\nright: slides up, then shuts and locks once you're through", new Vector3(9f, 5f, -11f), zone);
    }

    // The Door_Placeholder prefab (made on first use) at a hinge point, with a frame around the 2 x 3 opening.
    private static Door SpawnDoor(string name, Vector3 hinge, bool locked, bool closeBehind, Transform parent)
    {
        GameObject prefab = EnsureDoorPrefab();
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(hinge, Quaternion.identity);
        instance.name = name;

        var door = instance.GetComponent<Door>();
        SetField(door, "locked", p => p.boolValue = locked);
        SetField(door, "closeBehindPlayer", p => p.boolValue = closeBehind);

        Box("Frame_Post", hinge + new Vector3(-0.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.4f), PropColor, parent);
        Box("Frame_Post", hinge + new Vector3(2.2f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.4f), PropColor, parent);
        Box("Frame_Top", hinge + new Vector3(1f, 3.2f, 0f), new Vector3(2.8f, 0.4f, 0.4f), PropColor, parent);
        return door;
    }

    // Root = hinge + doorway trigger + Door + highlight; child Visual = a 2 x 3 panel with its collider. Swap the Visual for real art.
    private static GameObject EnsureDoorPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        if (existing != null)
            return existing;

        var root = new GameObject("Door_Placeholder");
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(1f, 1.5f, 0f);
        trigger.size = new Vector3(2.4f, 3.2f, 1.6f);
        var door = root.AddComponent<Door>();
        root.AddComponent<InteractableHighlight>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(1f, 1.5f, 0f);
        visual.transform.localScale = new Vector3(2f, 3f, 0.15f);
        visual.AddComponent<RendererTint>().Tint = new Color(0.45f, 0.3f, 0.2f);

        var so = new SerializedObject(door);
        so.FindProperty("visual").objectReferenceValue = visual.transform;
        so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + "Door opening sound effect.mp3");
        so.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxFolder + "Door closing sound effect.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("Created " + DoorPrefabPath);
        return prefab;
    }

    private static ItemSocket Socket(GameObject host, string itemAsset, int amount, bool consume, string verb, string rewardAsset, GameObject[] visuals)
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

    private static void OpenOnFilled(ItemSocket socket, Door door)
    {
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(socket.onFilled, door.Open);
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

    private static void TuneWarningThresholds()
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

    // ---- helpers ------------------------------------------------------------------------------------------------

    private static Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(arenaRoot, false);
        return group;
    }

    private static GameObject Box(string name, Vector3 center, Vector3 size, Color color, Transform parent)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = center;
        box.transform.localScale = size;
        box.AddComponent<RendererTint>().Tint = color;
        return box;
    }

    private static void Label(string text, Vector3 position, Transform parent)
    {
        var go = new GameObject("Label_" + text);
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        var mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = labelFont;
        mesh.fontSize = 64;
        mesh.characterSize = 0.2f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = LabelColor;
        go.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
    }

    // A Fish School object with the fish placed as prefab-linked children in a ring, so they show in the editor and can be tuned.
    private static void CreateSchool(string prefabName, int count, Vector3 position, Transform parent)
    {
        var school = new GameObject("FishSchool_" + prefabName);
        school.transform.SetParent(parent, false);
        school.transform.position = position;
        school.AddComponent<FishSchool>();

        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            Vector3 offset = new Vector3(Mathf.Cos(angle), i % 2 == 0 ? 0.2f : -0.2f, Mathf.Sin(angle)) * 1.2f;
            Spawn(prefabName, position + offset, school.transform, 90f);
        }
    }

    private static GameObject Spawn(string prefabName, Vector3 position, Transform parent, float yaw = 0f)
    {
        GameObject prefab = FindPrefab(prefabName);
        if (prefab == null)
        {
            Debug.LogWarning($"TestArena: no prefab named '{prefabName}' under {PrefabRoot}, skipped.");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.gameObject.scene);
        instance.transform.SetParent(parent, true);
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        return instance;
    }

    private static GameObject FindPrefab(string name) => InteractablePrefabTools.FindPrefab(name);
    private static ParticleSystem FindSparkle() => InteractablePrefabTools.FindSparkle();

    // Uses Pickup_Placeholder.prefab when the team has made one, otherwise builds the same thing from primitives.
    private static void CreatePickup(Vector3 position, string itemAssetName, Transform parent)
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
            visual.transform.localScale = Vector3.one * 0.25f;

            ParticleSystem sparkle = FindSparkle();
            if (sparkle != null)
                SetField(indicator, "effectPrefab", p => p.objectReferenceValue = sparkle);
        }

        ItemDefinition item = ItemTools.Load(itemAssetName);
        var pickup = root.GetComponentInChildren<PickupItem>();
        if (pickup != null && item != null)
            SetField(pickup, "item", p => p.objectReferenceValue = item);
        root.name = "Pickup_" + itemAssetName.Replace("Item_", "");
    }

    private static void SetField(Object target, string field, System.Action<SerializedProperty> set)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"TestArena: '{target.GetType().Name}' has no field '{field}', skipped.");
            return;
        }
        set(prop);
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
