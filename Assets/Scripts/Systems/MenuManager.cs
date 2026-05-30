using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    [Header("Game Scene")]
    public string gameSceneName = "UpgradeScene";

    private void Start()
    {
        EnsureSoundManager();

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayMenuBGM();
    }

    public void StartGame()
    {
        // SceneLoader varsa additive geçiş kullan
        if (SceneLoader.Instance != null)
        {
            // Geçiş zaten devam ediyorsa ikinci tıklamayı yoksay
            if (SceneLoader.Instance.IsTransitioning)
            {
                Debug.LogWarning("[MenuManager] Geçiş zaten devam ediyor, istek yoksayildi.");
                return;
            }

            // StartScene'i unload et, UpgradeScene'i yükle
            SceneLoader.Instance.TransitionTo(
                targetScene:   gameSceneName,
                sceneToUnload: "StartScene",
                onComplete:    () => Debug.Log($"[MenuManager] '{gameSceneName}' yüklendi.")
            );
        }
        else
        {
            // Fallback: SceneLoader yoksa CoreScene'i yükle, o StartScene'i yükler
            // Doğrudan UpgradeScene'e geç
            SceneManager.LoadScene(gameSceneName);
        }
    }

    public void QuitGame()
    {
        Debug.Log("Game is exiting...");
        Application.Quit();
    }

    // -------------------------------------------------------------------------
    // SoundManager yoksa Resources'tan prefabı yükleyip instantiate et.
    // GridScene'den geliniyorsa DontDestroyOnLoad sayesinde zaten var olur,
    // bu metod sadece StartScene ilk açıldığında devreye girer.
    // -------------------------------------------------------------------------
    private void EnsureSoundManager()
    {
        if (SoundManager.Instance != null) return;

        // Assets/Resources/SoundManager.prefab'ı yükle
        GameObject prefab = Resources.Load<GameObject>("SoundManager");
        if (prefab != null)
        {
            Instantiate(prefab);
            return;
        }

        Debug.LogWarning("[MenuManager] Assets/Resources/SoundManager.prefab bulunamadı! " +
                         "Prefabı Assets/Resources/ klasörüne taşı.");
    }
}
