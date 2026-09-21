// A page of the pause menu. Any MonoBehaviour in the scene that implements this shows up in the menu's sidebar (Map,
// Settings, Keybindings and Admin today). PageTitle is the entry, Order sorts the entries (lower first), OnPageShown
// runs when the page is opened, DrawPage draws it with IMGUI (GUILayout) inside the menu's scroll view. Draw buttons,
// switches, rows and key caps with MenuGUI so they match the rest and the hover tick works.
public interface IPauseMenuPage
{
    string PageTitle { get; }
    int Order { get; }
    void OnPageShown();
    void DrawPage();
}
