using System;
using System.Collections.Generic;
using UnityEngine;

// The Credits page of the pause menu: the same credits as the main menu (Assets/Resources/Credits.asset), drawn with
// the menu's controls. Each team name is a row with their photo that folds open to what they did and pictures of
// their work; the sounds, models and fonts fold out per kind. On the Player with the other pages.
public class CreditsPage : MonoBehaviour, IPauseMenuPage
{
    [SerializeField] private string pageTitle = "Credits";
    [Tooltip("The team and the asset credits. Empty = Assets/Resources/Credits.asset.")]
    [SerializeField] private CreditsList credits;
    [Tooltip("What a name opens to while nothing is written for it on the credits asset.")]
    [SerializeField] private string nothingWrittenYet = "work here";
    [Tooltip("How tall the pictures under a name are drawn, in menu pixels.")]
    [SerializeField] private float pictureHeight = 140f;
    [SerializeField] private float photoSize = 36f;

    private readonly HashSet<string> open = new HashSet<string>();

    public string PageTitle => pageTitle;
    public int Order => 30;

    public void OnPageShown()
    {
        if (credits == null)
            credits = Resources.Load<CreditsList>("Credits");
    }

    public void DrawPage()
    {
        GUIStyle note = MenuGUI.NoteStyle ?? GUI.skin.label;
        if (credits == null)
        {
            GUILayout.Label("No credits asset yet: Tools > Out of the Depths > Update Credits makes Assets/Resources/Credits.asset.", note);
            return;
        }

        GUILayout.Label(credits.title, MenuGUI.RowLabelStyle ?? GUI.skin.label);
        MenuGUI.Heading("Team");
        foreach (CreditsList.Member member in credits.team)
        {
            if (member == null || string.IsNullOrEmpty(member.name))
                continue;
            CreditsList.Member who = member;
            Fold("team:" + who.name, who.name, who.photo, () =>
            {
                GUILayout.Label(string.IsNullOrEmpty(who.contributions) ? nothingWrittenYet : who.contributions, note);
                Pictures(who.pictures);
            });
        }

        MenuGUI.Heading("Made with");
        if (!string.IsNullOrEmpty(credits.assetsHeading))
            GUILayout.Label(credits.assetsHeading, note);
        foreach (string category in credits.Categories())
        {
            List<CreditsList.Entry> entries = credits.In(category);
            string heading = (string.IsNullOrEmpty(category) ? "Other" : category) + " (" + entries.Count + ")";
            Fold("kind:" + category, heading, null, () =>
            {
                foreach (CreditsList.Entry entry in entries)
                    GUILayout.Label(entry.Line, note);
            });
        }
    }

    // A row that folds open: the photo (if any), then a button with the name; the body under it while open.
    private void Fold(string key, string label, Sprite photo, Action body)
    {
        bool isOpen = open.Contains(key);
        GUILayout.BeginHorizontal();
        if (photo != null)
        {
            Rect rect = GUILayoutUtility.GetRect(photoSize, photoSize, GUILayout.Width(photoSize), GUILayout.Height(photoSize));
            DrawSprite(rect, photo);
            GUILayout.Space(8f);
        }
        if (MenuGUI.Button((isOpen ? "[-]  " : "[+]  ") + label, MenuGUI.SmallButtonStyle, GUILayout.ExpandWidth(true)))
        {
            if (isOpen)
                open.Remove(key);
            else
                open.Add(key);
            isOpen = !isOpen;
        }
        GUILayout.EndHorizontal();
        if (!isOpen)
            return;
        GUILayout.BeginHorizontal();
        GUILayout.Space(24f);
        GUILayout.BeginVertical();
        GUILayout.Space(4f);
        body();
        GUILayout.Space(10f);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    // Pictures of someone's work, in rows of up to three, each as tall as Picture Height and as wide as its shape needs.
    private void Pictures(Sprite[] pictures)
    {
        if (pictures == null)
            return;
        int inRow = 0;
        foreach (Sprite sprite in pictures)
        {
            if (sprite == null)
                continue;
            if (inRow == 0)
                GUILayout.BeginHorizontal();
            float width = pictureHeight * Mathf.Max(0.2f, sprite.rect.width / Mathf.Max(1f, sprite.rect.height));
            Rect rect = GUILayoutUtility.GetRect(width, pictureHeight, GUILayout.Width(width), GUILayout.Height(pictureHeight));
            DrawSprite(rect, sprite);
            GUILayout.Space(10f);
            if (++inRow == 3)
            {
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(10f);
                inRow = 0;
            }
        }
        if (inRow > 0)
        {
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }

    // A sprite (its part of its texture), fitted inside the rect, keeping its shape.
    private static void DrawSprite(Rect rect, Sprite sprite)
    {
        if (Event.current.type != EventType.Repaint || sprite == null || sprite.texture == null)
            return;
        Rect part = sprite.textureRect;
        var uv = new Rect(part.x / sprite.texture.width, part.y / sprite.texture.height, part.width / sprite.texture.width, part.height / sprite.texture.height);
        float aspect = part.width / Mathf.Max(1f, part.height);
        float width = rect.width, height = rect.height;
        if (width / height > aspect)
            width = height * aspect;
        else
            height = width / aspect;
        var fitted = new Rect(rect.x + (rect.width - width) * 0.5f, rect.y + (rect.height - height) * 0.5f, width, height);
        GUI.DrawTextureWithTexCoords(fitted, sprite.texture, uv);
    }
}
