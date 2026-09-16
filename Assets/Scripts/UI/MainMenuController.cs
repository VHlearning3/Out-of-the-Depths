using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
    [SerializeField] private string settingsMessage = "Settings coming soon.";
    [SerializeField] private string creditsMessage = "Out the Depths Demo\n\nNoora, Otto, Sara, Ibrahim, Rebe, Vili";

    private void Awake()
    {
        newGameButton.onClick.AddListener(OnNewGame);
        settingsButton.onClick.AddListener(OnSettings);
        creditsButton.onClick.AddListener(OnCredits);
        quitButton.onClick.AddListener(OnQuit);
        infoPanelBackButton.onClick.AddListener(CloseInfoPanel);

        infoPanel.SetActive(false);
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
        ShowInfoPanel(creditsMessage);
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
        infoPanelText.text = message;
        infoPanel.SetActive(true);
    }

    private void CloseInfoPanel()
    {
        infoPanel.SetActive(false);
    }
}
