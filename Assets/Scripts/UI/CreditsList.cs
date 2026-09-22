using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// The credits page of the main menu: the team (click a name to see what they did) and every sound, model and font
// the game uses, with who made it and under what licence. Assets/Resources/Credits.asset. Tools > Out of the
// Depths > Update Credits adds a line for every file it finds (the sound table in Assets/Sound/CREDITS.md fills in
// titles, authors and sources) and never touches a line already there, so edit the words here freely.
[CreateAssetMenu(fileName = "Credits", menuName = "Out of the Depths/Credits")]
public class CreditsList : ScriptableObject
{
    [Serializable]
    public class Member
    {
        public string name = "";
        [Tooltip("A photo beside the name.")]
        public Sprite photo;
        [Tooltip("What they did, shown when their name is clicked. One thing per line reads best.")]
        [TextArea(2, 6)] public string contributions = "";
        [Tooltip("Pictures of their work, shown under the words when the name is clicked.")]
        public Sprite[] pictures = new Sprite[0];
    }

    [Serializable]
    public class Entry
    {
        [Tooltip("The file it is about, relative to its folder (the key the tool matches on).")]
        public string file = "";
        [Tooltip("Sounds, Models, Fonts... a heading on the page.")]
        public string category = "Sounds";
        [Tooltip("What it is called on the page.")]
        public string what = "";
        public string author = "";
        [Tooltip("CC0, CC-BY 3.0, ...")]
        public string licence = "";
        [Tooltip("Where it came from (a URL).")]
        public string source = "";
        [Tooltip("Leave it off the page (a spare that never plays).")]
        public bool hidden = false;

        public string Line
        {
            get
            {
                var line = new StringBuilder(string.IsNullOrEmpty(what) ? file : what);
                if (!string.IsNullOrEmpty(author))
                    line.Append(" - ").Append(author);
                if (!string.IsNullOrEmpty(licence))
                    line.Append(" (").Append(licence).Append(')');
                return line.ToString();
            }
        }
    }

    [Tooltip("The first line of the page.")]
    public string title = "Out the Depths";
    [Tooltip("The team, in order. On the page each name opens to show what they did.")]
    public Member[] team = new Member[0];
    [Tooltip("Above the asset list.")]
    public string assetsHeading = "Made with these sounds, models and fonts by kind people:";
    public Entry[] entries = new Entry[0];

    // The categories in the order they first appear.
    public List<string> Categories()
    {
        var categories = new List<string>();
        foreach (Entry entry in entries)
            if (entry != null && !entry.hidden && !categories.Contains(entry.category ?? ""))
                categories.Add(entry.category ?? "");
        return categories;
    }

    public List<Entry> In(string category)
    {
        var list = new List<Entry>();
        foreach (Entry entry in entries)
            if (entry != null && !entry.hidden && (entry.category ?? "") == category)
                list.Add(entry);
        return list;
    }
}
