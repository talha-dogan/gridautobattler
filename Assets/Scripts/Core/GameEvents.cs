using System;

/// <summary>
/// Central static event bus for decoupled, cross-system communication.
///
/// Rules of use:
///   • Any system that needs to BROADCAST a change invokes the event here.
///   • Any system that needs to REACT subscribes/unsubscribes in Awake/OnEnable
///     and OnDestroy/OnDisable respectively.
///   • No MonoBehaviour reference is required — static events work across scenes
///     as long as subscribers manage their own lifecycle correctly.
/// </summary>
public static class GameEvents
{
    // -------------------------------------------------------------------------
    // UI Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Raised whenever any gameplay system wants to display a message in the
    /// status text area. Subscribers (e.g. GameUIManager) update the actual
    /// TextMeshProUGUI component — gameplay code never touches UI directly.
    /// </summary>
    public static event Action<string> OnStatusTextChanged;

    /// <summary>Broadcasts a new status message to all UI subscribers.</summary>
    public static void SetStatusText(string message)
    {
        OnStatusTextChanged?.Invoke(message);
    }

    // -------------------------------------------------------------------------
    // Level / Economy Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Raised when the current level index changes (e.g. after a win or restart).
    /// Parameter: 1-based display index (currentLevelIndex + 1).
    /// Subscribers (e.g. GameUIManager) update the level label.
    /// </summary>
    public static event Action<int> OnLevelIndexChanged;

    /// <summary>Broadcasts the new 1-based level display index.</summary>
    public static void SetLevelIndex(int displayIndex)
    {
        OnLevelIndexChanged?.Invoke(displayIndex);
    }

    /// <summary>
    /// Raised whenever the player's gold amount changes.
    /// Parameter: new gold total.
    /// </summary>
    public static event Action<int> OnGoldChanged;

    /// <summary>Broadcasts the updated gold total to all UI subscribers.</summary>
    public static void SetGold(int newTotal)
    {
        OnGoldChanged?.Invoke(newTotal);
    }

    // -------------------------------------------------------------------------
    // Battle Lifecycle Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Raised when the battle officially begins (after the WAR! button is pressed
    /// and all units are registered). Subscribers can react to combat starting.
    /// </summary>
    public static event Action OnBattleStarted;

    /// <summary>Broadcasts that the battle has started.</summary>
    public static void BattleStarted()
    {
        OnBattleStarted?.Invoke();
    }

    /// <summary>
    /// Raised when the player wins the current level.
    /// Parameter: gold reward breakdown string for the status display.
    /// </summary>
    public static event Action<string> OnLevelWin;

    /// <summary>Broadcasts a level win with the reward summary message.</summary>
    public static void LevelWin(string rewardMessage)
    {
        OnLevelWin?.Invoke(rewardMessage);
    }

    /// <summary>
    /// Raised when the player loses the current level.
    /// Parameter: flavour defeat message for the status display.
    /// </summary>
    public static event Action<string> OnLevelLose;

    /// <summary>Broadcasts a level loss with the defeat flavour message.</summary>
    public static void LevelLose(string defeatMessage)
    {
        OnLevelLose?.Invoke(defeatMessage);
    }

    // -------------------------------------------------------------------------
    // Unit Lifecycle Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Raised when any unit is spawned and registered to the battlefield.
    /// Useful for analytics, tutorial triggers, or visual feedback systems.
    /// </summary>
    public static event Action<BaseUnit> OnUnitSpawned;

    /// <summary>Broadcasts that a unit has been spawned.</summary>
    public static void UnitSpawned(BaseUnit unit)
    {
        OnUnitSpawned?.Invoke(unit);
    }

    /// <summary>
    /// Raised when any unit dies and is unregistered from the battlefield.
    /// Useful for kill-count tracking, achievements, or VFX triggers.
    /// </summary>
    public static event Action<BaseUnit> OnUnitDied;

    /// <summary>Broadcasts that a unit has died.</summary>
    public static void UnitDied(BaseUnit unit)
    {
        OnUnitDied?.Invoke(unit);
    }

    // -------------------------------------------------------------------------
    // Cleanup helper — call this when loading a new scene to prevent stale
    // subscribers from a previous scene from receiving events.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Clears all subscribers from every event in this bus.
    /// Call from a scene-transition manager or on scene unload.
    /// </summary>
    public static void ClearAllEvents()
    {
        OnStatusTextChanged = null;
        OnLevelIndexChanged = null;
        OnGoldChanged       = null;
        OnBattleStarted     = null;
        OnLevelWin          = null;
        OnLevelLose         = null;
        OnUnitSpawned       = null;
        OnUnitDied          = null;
    }
}
