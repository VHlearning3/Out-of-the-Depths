using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The game's font: the one file in Assets/Resources/Fonts (SuperCarnival.ttf today). Everything that shows text uses
// it: the HUD, the signs and the prompts get it when they are built, the pause menu takes it when its theme names no
// font, and when a scene loads every Text still on Unity's built-in font is switched over, so a scene or prefab that
// Tools > Out of the Depths > Use Game Font never touched shows it all the same. To change the font, put one other
// file in that folder and run that tool once (All Scenes) so the scenes and prefabs say so too.
// No bold with it: a font file has one weight, so Unity makes "bold" by smearing the letters wider, which on a heavy
// display font like Super Carnival looks thick and blotchy. Code asks for GameFont.Bold (plain while the game font is
// in), and every Text on the game font that a scene or prefab set to bold is made plain as the scene loads.
public static class GameFont
{
    public const string ResourcesFolder = "Fonts";

    private static Font custom;
    private static bool looked;

    // The font in Resources/Fonts, or null when there is none.
    public static Font Custom
    {
        get
        {
            if (!looked)
            {
                looked = true;
                Font[] fonts = Resources.LoadAll<Font>(ResourcesFolder);
                System.Array.Sort(fonts, (a, b) => string.CompareOrdinal(a.name, b.name));
                custom = fonts.Length > 0 ? fonts[0] : null;
            }
            return custom;
        }
    }

    // The font to use: the game's, else Unity's built-in one.
    public static Font Font => Custom != null ? Custom : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    // Use instead of FontStyle.Bold: plain with the game font (it has no bold of its own), bold with Unity's.
    public static FontStyle Bold => Custom != null ? FontStyle.Normal : FontStyle.Bold;

    // A text's style without the faked bold, when it is on the game font. True when it changed.
    public static bool Unbold(Text text)
    {
        if (text == null || Custom == null || text.font != Custom)
            return false;
        FontStyle plain = text.fontStyle == FontStyle.Bold ? FontStyle.Normal : text.fontStyle == FontStyle.BoldAndItalic ? FontStyle.Italic : text.fontStyle;
        if (plain == text.fontStyle)
            return false;
        text.fontStyle = plain;
        return true;
    }

    // After a font was added to or removed from the folder in the editor.
    public static void Forget()
    {
        looked = false;
        custom = null;
    }

    // Unity's built-in font, the one a new Text starts with (nobody chose it on purpose).
    public static bool IsBuiltIn(Font font)
    {
        return font == null || font.name == "LegacyRuntime" || font.name == "Arial";
    }

    // Every Text under the root onto the font; onlyBuiltIn = leave a font someone chose on purpose alone.
    // Returns how many changed.
    public static int Apply(GameObject root, Font font, bool onlyBuiltIn)
    {
        if (root == null || font == null)
            return 0;
        int changed = 0;
        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            bool now = false;
            if (text.font != font && !(onlyBuiltIn && !IsBuiltIn(text.font)))
            {
                text.font = font;
                now = true;
            }
            now |= Unbold(text);
            if (now)
                changed++;
        }
        return changed;
    }

    public static int Apply(Scene scene, Font font, bool onlyBuiltIn)
    {
        if (!scene.IsValid() || font == null)
            return 0;
        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
            changed += Apply(root, font, onlyBuiltIn);
        return changed;
    }

    // While playing: every scene, as it loads, gets the game font on whatever text was left on the built-in one.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void SwitchScenesAsTheyLoad()
    {
        SceneManager.sceneLoaded += (scene, mode) => Apply(scene, Custom, true);
        Apply(SceneManager.GetActiveScene(), Custom, true);
    }
}
