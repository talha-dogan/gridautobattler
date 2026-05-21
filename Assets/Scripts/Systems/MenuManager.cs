using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Game Scene")]
    // The exact name of the scene you want to load (must be added in Build Settings)
    public string gameSceneName = "TacticalScene";

    // Assign this to the "START" button's OnClick event
    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    // Assign this to the "EXIT" button's OnClick event
    public void QuitGame()
    {
        // This log only appears in the Unity Editor console
        Debug.Log("Game is exiting...");

        // This closes the actual built game application
        Application.Quit();
    }
}