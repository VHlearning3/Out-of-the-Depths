using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Main menu buttons: New Game loads the level, Settings opens the info panel with a message, Credits fills the panel
// with the credits page (the team, each name a row with their photo that opens to show what they did and pictures
// of their work, then every sound, model and font the game uses, a folding list per kind; all from
// Assets/Resources/Credits.asset, kept up by Tools > Out of the Depths > Update Credits), Quit exits. The credits
// page is built where the info text sits, in a scroll view, each time it opens.
public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private string gameplaySceneName = "Main_Scene";
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Info Panel")]
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private Text infoPanelText;
    [SerializeField] private Button infoPanelBackButton;
    [SerializeField, TextArea] private string settingsMessage = "Settings coming soon.";

    [Header("Credits")]
    [Tooltip("The team and the asset credits. Empty = Assets/Resources/Credits.asset.")]
    [SerializeField] private CreditsList credits;
    [Tooltip("Behind each name and each list heading (click to open).")]
    [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.08f);
    [Tooltip("What a name opens to while nothing is written for it on the credits asset.")]
    [SerializeField, TextArea] private string nothingWrittenYet = "work here";
    [Tooltip("The size of the pictures under a name, on a 1920 x 1080 screen.")]
    [SerializeField] private Vector2 pictureSize = new Vector2(240f, 180f);

    private GameObject creditsView;

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        settingsButton.onClick.AddListener(OnSettings);
        creditsButton.onClick.AddListener(OnCredits);
        quitButton.onClick.AddListener(OnQuit);
        infoPanelBackButton.onClick.AddListener(CloseInfoPanel);

        infoPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (infoPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseInfoPanel();
    }

    private void OnNewGame()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    private void OnSettings()
    {
        ShowInfoPanel(settingsMessage);
    }

    private void OnCredits()
    {
        ShowInfoPanel("");
        infoPanelText.gameObject.SetActive(false);
        BuildCredits();
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowInfoPanel(string message)
    {
        DestroyCredits();
        infoPanelText.gameObject.SetActive(true);
        infoPanelText.text = message;
        infoPanel.SetActive(true);
    }

    private void CloseInfoPanel()
    {
        DestroyCredits();
        infoPanelText.gameObject.SetActive(true);
        infoPanel.SetActive(false);
    }

    // ---- the credits page ---------------------------------------------------------------------------------------

    private void DestroyCredits()
    {
        if (creditsView != null)
            Destroy(creditsView);
        creditsView = null;
    }

    private void BuildCredits()
    {
        if (credits == null)
            credits = Resources.Load<CreditsList>("Credits");

        // A scroll view where the info text sits, same place and size.
        RectTransform area = infoPanelText.rectTransform;
        creditsView = new GameObject("CreditsView", typeof(RectTransform));
        var view = (RectTransform)creditsView.transform;
        view.SetParent(area.parent, false);
        view.anchorMin = area.anchorMin;
        view.anchorMax = area.anchorMax;
        view.pivot = area.pivot;
        view.anchoredPosition = area.anchoredPosition;
        view.sizeDelta = area.sizeDelta;
        view.SetSiblingIndex(area.GetSiblingIndex());
        creditsView.AddComponent<RectMask2D>();
        var scroll = creditsView.AddComponent<ScrollRect>();

        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(view, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        Column(content.gameObject, 6f, new RectOffset(8, 8, 8, 8));
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        int size = infoPanelText.fontSize;
        Color color = infoPanelText.color;
        if (credits == null)
        {
            Line(content, "Credits", size + 8, color, TextAnchor.MiddleCenter);
            Line(content, "No credits asset yet: Tools > Out of the Depths > Update Credits makes Assets/Resources/Credits.asset.", size - 6, color, TextAnchor.UpperLeft);
            return;
        }

        Line(content, credits.title, size + 8, color, TextAnchor.MiddleCenter);
        Line(content, "TEAM", size - 2, color, TextAnchor.MiddleLeft);
        foreach (CreditsList.Member member in credits.team)
        {
            if (member == null || string.IsNullOrEmpty(member.name))
                continue;
            string words = string.IsNullOrEmpty(member.contributions) ? nothingWrittenYet : member.contributions;
            Folding(content, member.name, words, size, color, member.photo, member.pictures);
        }

        Line(content, credits.assetsHeading, size - 4, color, TextAnchor.MiddleLeft);
        foreach (string category in credits.Categories())
        {
            var lines = new System.Text.StringBuilder();
            foreach (CreditsList.Entry entry in credits.In(category))
                lines.Append(lines.Length > 0 ? "\n" : "").Append(entry.Line);
            string heading = (string.IsNullOrEmpty(category) ? "Other" : category) + " (" + credits.In(category).Count + ")";
            Folding(content, heading, lines.ToString(), size - 2, color, null, null);
        }
    }

    // A row you click to open what is under it (words, then pictures), and click again to fold it away.
    private void Folding(RectTransform parent, string name, string words, int size, Color color, Sprite photo, Sprite[] pictures)
    {
        float rowHeight = photo != null ? Mathf.Max(size + 22f, 76f) : size + 22f;
        var row = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<Image>().color = rowColor;
        row.GetComponent<LayoutElement>().preferredHeight = rowHeight;

        float labelLeft = 14f;
        if (photo != null)
        {
            Image portrait = Picture(row.transform, photo);
            RectTransform rect = portrait.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(8f, 0f);
            rect.sizeDelta = new Vector2(rowHeight - 12f, rowHeight - 12f);
            labelLeft = rowHeight + 8f;
        }
        Text label = NewText((RectTransform)row.transform, "[+]  " + name, size, color, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(labelLeft, 0f);
        label.rectTransform.offsetMax = new Vector2(-14f, 0f);
        label.raycastTarget = false;

        var details = new GameObject("Details", typeof(RectTransform));
        details.transform.SetParent(parent, false);
        Column(details, 8f, new RectOffset(24, 8, 2, 10));
        Line((RectTransform)details.transform, words, size - 6, color, TextAnchor.UpperLeft);
        if (pictures != null && pictures.Length > 0)
        {
            var grid = new GameObject("Pictures", typeof(RectTransform), typeof(GridLayoutGroup));
            grid.transform.SetParent(details.transform, false);
            var layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = pictureSize;
            layout.spacing = new Vector2(10f, 10f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Mathf.Max(1, Mathf.FloorToInt((infoPanelText.rectTransform.rect.width - 40f) / (pictureSize.x + 10f)));
            foreach (Sprite sprite in pictures)
                if (sprite != null)
                    Picture(grid.transform, sprite);
        }
        details.SetActive(false);

        row.GetComponent<Button>().onClick.AddListener(() =>
        {
            bool open = !details.activeSelf;
            details.SetActive(open);
            label.text = (open ? "[-]  " : "[+]  ") + name;
        });
    }

    private static void Column(GameObject host, float spacing, RectOffset padding)
    {
        var layout = host.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = padding;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
    }

    private static Image Picture(Transform parent, Sprite sprite)
    {
        var image = new GameObject("Picture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Text Line(RectTransform parent, string content, int size, Color color, TextAnchor anchor)
    {
        var holder = new GameObject("Line", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        return NewText((RectTransform)holder.transform, content, size, color, anchor, true);
    }

    private Text NewText(RectTransform parent, string content, int size, Color color, TextAnchor anchor, bool onParent = false)
    {
        GameObject host = onParent ? parent.gameObject : new GameObject("Text", typeof(RectTransform));
        if (!onParent)
            host.transform.SetParent(parent, false);
        var text = host.AddComponent<Text>();
        text.text = content;
        text.font = infoPanelText.font != null ? infoPanelText.font : GameFont.Font;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }
}
