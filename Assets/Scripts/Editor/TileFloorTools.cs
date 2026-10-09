using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The middle room's floor in the team's tile floor (Art/Materials/tilefloor_texture: the mosaic painted as one picture,
// 20 x 20 tiles, with its normal map for the grout) instead of the red greybox grid: the deck's Grid Floor laid with
// Mapping Fit (the picture once over the whole room, so a tile is about a metre), Tint white so the painted colours
// show. The deck stays a Grid Floor, so its size and place still come from the builder.
// ShipGreyboxBuilder calls Apply for the middle room's deck; Tools > Out of the Depths > Use Tile Floor In Middle Room
// does the open scene without a rebuild (and saves it). Safe to run again.
public static class TileFloorTools
{
    public const string MaterialPath = "Assets/Art/Materials/tilefloor_texture.mat";
    private const string RoomName = "Room_2_Middle";

    [MenuItem("Tools/Out of the Depths/Use Tile Floor In Middle Room")]
    public static void ApplyToOpenScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Tile floor: stop Play mode first (anything changed in Play mode is lost when it stops).");
            return;
        }
        int done = 0;
        foreach (GridFloor floor in Object.FindObjectsByType<GridFloor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(floor) || floor.transform.parent == null || floor.transform.parent.name != RoomName)
                continue;
            if (Apply(floor))
            {
                RoomDressTools.ForgetFloorLooks(floor);   // tiles again by hand: a rebuild no longer lays wood or stone here
                done++;
            }
        }
        if (done == 0)
        {
            Debug.LogWarning($"Tile floor: no Grid Floor under {RoomName} in the open scene, or no material at {MaterialPath}.");
            return;
        }
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            RoomDressTools.SaveScene(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
        Debug.Log($"Tile floor: the middle room's floor now shows {MaterialPath} (scene saved).");
    }

    public static bool Apply(GridFloor floor)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (floor == null || material == null)
        {
            if (material == null)
                Debug.LogWarning($"Tile floor: no material at {MaterialPath}, the floor keeps its grid.");
            return false;
        }
        Undo.RecordObject(floor, "Use Tile Floor");
        floor.enabled = true;
        floor.SetLook(GridFloor.Mapping.Fit, Color.white, material);
        EditorUtility.SetDirty(floor);
        return true;
    }
}
