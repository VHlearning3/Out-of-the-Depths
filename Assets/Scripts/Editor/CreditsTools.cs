using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Fills Assets/Resources/Credits.asset: Tools > Out of the Depths > Update Credits makes it (with the team) if there
// is none, then adds one line for every sound under Assets/Sound (titles, authors and sources from the table in
// Assets/Sound/CREDITS.md), every font in Assets/Resources/Fonts and every model under Assets/Art/Models that the
// asset does not list yet. Lines already there are never changed, so anything edited by hand stays.
public static class CreditsTools
{
    public const string AssetPath = "Assets/Resources/Credits.asset";
    private const string SoundFolder = "Assets/Sound";
    private const string SoundTable = "Assets/Sound/CREDITS.md";
    private const string FontFolder = "Assets/Resources/Fonts";
    private const string ModelFolder = "Assets/Art/Models";
    private static readonly string[] DefaultTeam = { "Noora", "Otto", "Sara", "Ibrahim", "Rebe", "Vili" };

    [MenuItem("Tools/Out of the Depths/Update Credits")]
    public static void UpdateCredits()
    {
        var credits = AssetDatabase.LoadAssetAtPath<CreditsList>(AssetPath);
        if (credits == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            credits = ScriptableObject.CreateInstance<CreditsList>();
            credits.team = new CreditsList.Member[DefaultTeam.Length];
            for (int i = 0; i < DefaultTeam.Length; i++)
                credits.team[i] = new CreditsList.Member { name = DefaultTeam[i] };
            AssetDatabase.CreateAsset(credits, AssetPath);
            Debug.Log("Created " + AssetPath + " (write what each of the team did on it).");
        }

        var entries = new List<CreditsList.Entry>(credits.entries ?? new CreditsList.Entry[0]);
        int added = 0;

        Dictionary<string, (string title, string source, string author)> table = ReadSoundTable();
        foreach (string path in Files(SoundFolder, ".mp3", ".wav", ".ogg"))
        {
            string key = path.Substring(SoundFolder.Length + 1);
            if (Listed(entries, key))
                continue;
            var entry = new CreditsList.Entry { file = key, category = "Sounds", what = Pretty(Path.GetFileNameWithoutExtension(path)) };
            if (table.TryGetValue(key, out var row))
            {
                entry.what = row.title;
                entry.author = row.author;
                entry.source = row.source;
                entry.licence = "CC0";
            }
            else if (key.Contains("Tunetank"))
            {
                entry.author = "Tunetank";
                entry.licence = "tunetank.com licence";
            }
            else if (key.Contains("_CC0"))
                entry.licence = "CC0";
            else
                entry.author = "the team";
            entries.Add(entry);
            added++;
        }

        foreach (string path in Files(FontFolder, ".ttf", ".otf"))
        {
            string key = "Fonts/" + Path.GetFileName(path);
            if (Listed(entries, key))
                continue;
            entries.Add(new CreditsList.Entry { file = key, category = "Fonts", what = Pretty(Path.GetFileNameWithoutExtension(path)) });
            added++;
        }

        foreach (string path in Files(ModelFolder, ".fbx", ".obj"))
        {
            string key = path.Substring(ModelFolder.Length + 1);
            if (Listed(entries, key))
                continue;
            var entry = new CreditsList.Entry { file = key, category = "Models", what = Pretty(Path.GetFileNameWithoutExtension(path)) };
            if (key.ToLowerInvariant().Contains("seaweed"))
            {
                entry.author = "Laney XR Labs";
                entry.licence = "CC-BY 3.0";
                entry.source = "https://poly.pizza/u/Laney%20XR%20Labs";
            }
            else
                entry.author = "the team";
            entries.Add(entry);
            added++;
        }

        credits.entries = entries.ToArray();
        EditorUtility.SetDirty(credits);
        AssetDatabase.SaveAssets();
        Debug.Log($"Credits: {added} line(s) added, {entries.Count} in all, in {AssetPath}.");
    }

    private static bool Listed(List<CreditsList.Entry> entries, string key)
    {
        foreach (CreditsList.Entry entry in entries)
            if (entry != null && entry.file == key)
                return true;
        return false;
    }

    private static IEnumerable<string> Files(string folder, params string[] extensions)
    {
        if (!Directory.Exists(folder))
            yield break;
        var paths = new List<string>(Directory.GetFiles(folder, "*", SearchOption.AllDirectories));
        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        foreach (string raw in paths)
        {
            string path = raw.Replace('\\', '/');
            string extension = Path.GetExtension(path).ToLowerInvariant();
            foreach (string wanted in extensions)
                if (extension == wanted)
                {
                    yield return path;
                    break;
                }
        }
    }

    // "Door_HeavySlam_Kyles_CC0" -> "Door HeavySlam Kyles CC0"; the table gives nicer names for the fetched ones.
    private static string Pretty(string name)
    {
        return Regex.Replace(name.Replace('_', ' '), @"\s+", " ").Trim();
    }

    // The rows of the sound table: | `file` | used as | URL "title" | author |
    private static Dictionary<string, (string title, string source, string author)> ReadSoundTable()
    {
        var table = new Dictionary<string, (string, string, string)>();
        if (!File.Exists(SoundTable))
            return table;
        var row = new Regex(@"^\|\s*`([^`]+)`\s*\|[^|]*\|\s*(\S+)\s+""([^""]*)""\s*\|\s*([^|]*?)\s*\|");
        foreach (string line in File.ReadAllLines(SoundTable))
        {
            Match match = row.Match(line);
            if (match.Success)
                table[match.Groups[1].Value] = (match.Groups[3].Value, match.Groups[2].Value, match.Groups[4].Value);
        }
        return table;
    }
}
