using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    // The panel containing Start, Settings, and Exit buttons
    public GameObject mainMenuPanel;
    // The panel containing Tactical Challenge, Wave Defense, and Back buttons
    public GameObject modeSelectionPanel;

    [Header("Game Scenes")]
    // Make sure these match EXACTLY with your scenes in Build Settings
    public string campaignSceneName = "TacticalScene";
    public string waveSceneName = "WaveAiScene";

    private void Start()
    {
        // Ensure only the main menu is visible when the scene loads
        ShowMainMenu();
    }

    // --- PANEL NAVIGATION ---

    // Assign this to the "START" button on the Main Menu
    public void ShowModeSelection()
    {
        mainMenuPanel.SetActive(false);
        modeSelectionPanel.SetActive(true);
    }

    // Assign this to the "BACK TO MENU" button on the Mode Selection panel
    public void ShowMainMenu()
    {
        modeSelectionPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
    }

    // --- SCENE LOADING ---

    // Assign this to the "PLAY CAMPAIGN" button
    public void LoadCampaignMode()
    {
        SceneManager.LoadScene(campaignSceneName);
    }

    // Assign this to the "PLAY AI DUEL" button
    public void LoadWaveMode()
    {
        SceneManager.LoadScene(waveSceneName);
    }

    // Assign this to the "EXIT" button
    public void QuitGame()
    {
        // This log only appears in the Unity Editor console
        Debug.Log("Game is exiting...");

        // This closes the actual built game
        Application.Quit();
    }
}