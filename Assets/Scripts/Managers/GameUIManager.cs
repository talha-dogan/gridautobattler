using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Central UI manager that owns all TextMeshProUGUI references and listens to
/// GameEvents to update them. No gameplay system (BattleManager, LevelManager, etc.)
/// should hold a direct reference to any UI component — all UI updates flow through
/// this class via the event bus.
///
/// Removed in cleanup:
///   • meleeButtonText / rangedButtonText SerializedFields (drawing system is dead)
///   • HandleDrawLimitsChanged event handler
/// </summary>
public class GameUIManager : MonoBehaviour
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
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Subscribe to all UI-relevant events on the bus.
        GameEvents.OnStatusTextChanged += HandleStatusTextChanged;
        GameEvents.OnLevelIndexChanged += HandleLevelIndexChanged;
        GameEvents.OnGoldChanged       += HandleGoldChanged;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent stale delegate references after scene unload.
        GameEvents.OnStatusTextChanged -= HandleStatusTextChanged;
        GameEvents.OnLevelIndexChanged -= HandleLevelIndexChanged;
        GameEvents.OnGoldChanged       -= HandleGoldChanged;
    }

    // -------------------------------------------------------------------------
    // GameEvents handlers — each handler owns exactly one UI concern
    // -------------------------------------------------------------------------

    /// <summary>Updates the status text label with the incoming message.</summary>
    private void HandleStatusTextChanged(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    /// <summary>
    /// Updates the level label.
    /// displayIndex is 1-based (currentLevelIndex + 1).
    /// </summary>
    private void HandleLevelIndexChanged(int displayIndex)
    {
        if (levelText != null)
            levelText.text = "LEVEL " + displayIndex;
    }

    /// <summary>Updates the gold label with the new total.</summary>
    private void HandleGoldChanged(int newTotal)
    {
        if (goldText != null)
            goldText.text = newTotal + " G";
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
        SceneManager.LoadScene(menuSceneName);
    }
}
