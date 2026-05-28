using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Cycles battle speed through 1x → 2x → 3x on each button press.
/// The selected speed is persisted via PlayerPrefs so it survives level transitions.
///
/// Usage:
///   1. Attach this script to any GameObject in the scene.
///   2. Assign the Button and the label (TextMeshProUGUI or Text) in the Inspector.
///   3. The button's OnClick should call CycleSpeed().
/// </summary>
public class BattleSpeedController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector references
    // -------------------------------------------------------------------------

    [Header("UI References")]
    [Tooltip("The button that cycles the speed. Its OnClick should call CycleSpeed().")]
    [SerializeField] private Button speedButton;

    [Tooltip("TMP label on the button that shows the current speed (e.g. '1x').")]
    [SerializeField] private TextMeshProUGUI speedLabel;

    // -------------------------------------------------------------------------
    // Configuration
    // -------------------------------------------------------------------------

    private static readonly float[] SpeedSteps = { 1f, 2f, 3f };
    private const string SPEED_SAVE_KEY = "BattleSpeedIndex";

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private int _speedIndex = 0;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Restore the last-used speed index so the player keeps their preference
        // across level transitions and app restarts.
        _speedIndex = PlayerPrefs.GetInt(SPEED_SAVE_KEY, 0);
        _speedIndex = Mathf.Clamp(_speedIndex, 0, SpeedSteps.Length - 1);
    }

    private void Start()
    {
        // Wire up the button click if not already done via the Inspector.
        if (speedButton != null)
            speedButton.onClick.AddListener(CycleSpeed);

        // Apply the restored speed immediately.
        ApplySpeed();
    }

    private void OnDestroy()
    {
        if (speedButton != null)
            speedButton.onClick.RemoveListener(CycleSpeed);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Advances to the next speed step (wraps around after 3x → back to 1x).
    /// Assign this to the button's OnClick event in the Inspector, or it is
    /// wired automatically in Start().
    /// </summary>
    public void CycleSpeed()
    {
        _speedIndex = (_speedIndex + 1) % SpeedSteps.Length;
        PlayerPrefs.SetInt(SPEED_SAVE_KEY, _speedIndex);
        PlayerPrefs.Save();
        ApplySpeed();
    }

    /// <summary>
    /// Returns the currently active time-scale multiplier.
    /// </summary>
    public float CurrentSpeed => SpeedSteps[_speedIndex];

    // -------------------------------------------------------------------------
    // Internal helpers
    // -------------------------------------------------------------------------

    private void ApplySpeed()
    {
        float speed = SpeedSteps[_speedIndex];

        // Only modify Time.timeScale when a battle is actually running so we
        // don't accidentally speed up the pre-battle setup phase.
        // During setup the scale stays at 1 regardless of the stored preference;
        // the chosen speed kicks in the moment the battle starts.
        if (BattleManager.Instance != null && BattleManager.Instance.isBattleStarted)
            Time.timeScale = speed;

        UpdateLabel(speed);
    }

    private void UpdateLabel(float speed)
    {
        if (speedLabel == null) return;

        // Format: "1x", "2x", "3x"
        speedLabel.text = (int)speed + "x";
    }

    // -------------------------------------------------------------------------
    // Battle event integration
    // -------------------------------------------------------------------------
    // We listen to GameEvents so the speed is applied the moment the battle
    // starts (in case the player pre-selected 2x or 3x before pressing WAR!).


private void OnEnable()
{
    GameEvents.OnBattleStarted += HandleBattleStarted;
    GameEvents.OnLevelWin      += HandleBattleEnded;
    GameEvents.OnLevelLose     += HandleBattleEnded;

    if (GameInputHandler.Instance != null)
        GameInputHandler.Instance.OnSpeedCycle += CycleSpeed;
}

private void OnDisable()
{
    GameEvents.OnBattleStarted -= HandleBattleStarted;
    GameEvents.OnLevelWin      -= HandleBattleEnded;
    GameEvents.OnLevelLose     -= HandleBattleEnded;

    if (GameInputHandler.Instance != null)
        GameInputHandler.Instance.OnSpeedCycle -= CycleSpeed;
}

    private void HandleBattleStarted()
    {
        // Apply the stored speed as soon as the battle begins.
        Time.timeScale = SpeedSteps[_speedIndex];
        UpdateLabel(SpeedSteps[_speedIndex]);
    }

    private void HandleBattleEnded(string _)
    {
        // Reset time scale to normal after the battle ends so the
        // post-battle delay (NextLevelRoutine / RestartLevelRoutine) runs at 1x.
        Time.timeScale = 1f;
    }
}
