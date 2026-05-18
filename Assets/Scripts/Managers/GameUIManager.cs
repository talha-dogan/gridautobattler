using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Central UI manager that owns all TextMeshProUGUI references and listens to
/// GameEvents to update them. No gameplay system (BattleManager, LevelManager,
/// WaveDirector, DrawingManager, etc.) should hold a direct reference to any UI
/// component — all UI updates flow through this class via the event bus.
/// </summary>
public class GameUIManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector-assigned UI references
    // -------------------------------------------------------------------------

    [Header("HUD Labels")]
    [Tooltip("Displays the current level or wave number.")]
    [SerializeField] private TextMeshProUGUI levelText;

    [Tooltip("Displays the player's current gold total.")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Tooltip("Displays status messages (battle results, instructions, etc.).")]
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Placement Button Labels")]
    [Tooltip("Counter label on the Melee placement button (e.g. 'Mele\\n(5)').")]
    [SerializeField] private TextMeshProUGUI meleeButtonText;

    [Tooltip("Counter label on the Ranged placement button (e.g. 'Ranged\\n(3)').")]
    [SerializeField] private TextMeshProUGUI rangedButtonText;

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
        GameEvents.OnDrawLimitsChanged += HandleDrawLimitsChanged;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent stale delegate references after scene unload.
        GameEvents.OnStatusTextChanged -= HandleStatusTextChanged;
        GameEvents.OnLevelIndexChanged -= HandleLevelIndexChanged;
        GameEvents.OnGoldChanged       -= HandleGoldChanged;
        GameEvents.OnDrawLimitsChanged -= HandleDrawLimitsChanged;
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
    /// isWaveMode switches the prefix between "LEVEL" and "WAVE".
    /// </summary>
    private void HandleLevelIndexChanged(int displayIndex, bool isWaveMode)
    {
        if (levelText != null)
            levelText.text = (isWaveMode ? "WAVE " : "LEVEL ") + displayIndex;
    }

    /// <summary>Updates the gold label with the new total.</summary>
    private void HandleGoldChanged(int newTotal)
    {
        if (goldText != null)
            goldText.text = newTotal + " G";
    }

    /// <summary>
    /// Updates the melee and ranged placement button counter labels.
    /// Triggered by DrawingManager via GameEvents.SetDrawLimits() whenever
    /// limits are initialised or a draw stroke is committed.
    /// </summary>
    private void HandleDrawLimitsChanged(int meleeLimit, int rangedLimit)
    {
        if (meleeButtonText  != null) meleeButtonText.text  = $"Mele\n({meleeLimit})";
        if (rangedButtonText != null) rangedButtonText.text = $"Ranged\n({rangedLimit})";
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
