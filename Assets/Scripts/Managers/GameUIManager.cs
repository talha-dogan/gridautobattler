using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// MVP pattern — View katmanı.
///
/// Bu sınıf yalnızca UI bileşenlerini yönetir; iş mantığı GamePresenter'da.
/// GamePresenter, IGameView arayüzü üzerinden bu sınıfa erişir.
///
/// Sorumluluklar:
///   • TextMeshProUGUI referanslarını tutar.
///   • IGameView metodlarını implement eder (Show* metodları).
///   • GamePresenter'ı oluşturur ve yaşam döngüsünü yönetir.
///   • Hiçbir gameplay sistemi bu sınıfa doğrudan referans tutmaz.
/// </summary>
public class GameUIManager : MonoBehaviour, IGameView
{
    // -------------------------------------------------------------------------
    // Inspector-assigned UI references
    // -------------------------------------------------------------------------

    [Header("HUD Labels")]
    [Tooltip("Displays the current level number.")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Displays the player's current gold total.")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Tooltip("Displays status messages (battle results, instructions, etc.).")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Scene Settings")]
    [Tooltip("The exact name of the main menu scene.")]
    public string menuSceneName = "StartScene";

    // -------------------------------------------------------------------------
    // MVP — Presenter
    // -------------------------------------------------------------------------

    private GamePresenter _presenter;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Presenter'ı oluştur; bu sınıfı IGameView olarak geçir.
        // Presenter, GameEvents'e abone olur ve tüm UI güncellemelerini
        // IGameView metodları üzerinden bu sınıfa iletir.
        _presenter = new GamePresenter(this);
    }

    private void OnDestroy()
    {
        // Presenter'ın event aboneliklerini temizle.
        _presenter?.Dispose();
    }

    // -------------------------------------------------------------------------
    // IGameView — View implementasyonu
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    public void ShowStatusText(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    /// <inheritdoc/>
    public void ShowLevelIndex(int displayIndex)
    {
        if (levelText != null)
            levelText.text = "LEVEL " + displayIndex;
    }

    /// <inheritdoc/>
    public void ShowGold(int amount)
    {
        if (goldText != null)
            goldText.text = amount + " G";
    }

    // -------------------------------------------------------------------------
    // Button callbacks
    // -------------------------------------------------------------------------

    /// <summary>
    /// Assign this method to the OnClick event of the Back To Menu button.
    /// Resets time scale in case the game was paused before exiting.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        // SceneLoader varsa additive geçiş kullan
        if (SceneLoader.Instance != null)
        {
            string currentScene = SceneManager.GetActiveScene().name;
            SceneLoader.Instance.TransitionTo(
                targetScene:   menuSceneName,
                sceneToUnload: currentScene,
                onComplete:    () => Debug.Log($"[GameUIManager] '{menuSceneName}' yüklendi.")
            );
        }
        else
        {
            SceneManager.LoadScene(menuSceneName);
        }
    }
}
