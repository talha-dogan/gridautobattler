using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TDEV.Core;

/// <summary>
/// Manages level progression, economy, and board resets.
/// All UI updates are broadcast through GameEvents — LevelManager holds no
/// direct TextMeshProUGUI references. Win/loss signals are received from
/// GameEvents (fired by BattleManager) rather than called directly.
/// </summary>
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Game Mode")]
    // Set this to true in WaveScene Inspector
    public bool isWaveMode = false;

    [Header("Level Sequence")]
    public List<LevelDataSO> levels;

    private int _currentLevelIndex = 0;

    [Header("Economy Settings")]
    public int currentGold = 0;
    public int goldPerUnusedUnit = 25;

    // Flavour defeat messages referencing strategy tropes
    private string[] defeatJokes = new string[]
    {
        "DEFEAT!\nOuch... Have you considered the Crescent Tactic?",
        "DEFEAT!\nSun Tzu is crying right now. Retrying...",
        "DEFEAT!\nMaybe don't put your ranged units in the front? Just a thought.",
        "DEFEAT!\nRome wasn't built in a day, but your army fell in a minute.",
        "DEFEAT!\nDid you just Leeroy Jenkins your entire formation? Try again.",
        "DEFEAT!\nYour 'master plan' needs a little more 'master'. Retrying...",
        "DEFEAT!\nRetreat! I mean... Retry!"
    };

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // Subscribe to the status text event so this is the ONLY place that
        // writes to the statusText UI component. All other systems broadcast
        // through GameEvents.SetStatusText() and never touch the UI directly.
        GameEvents.OnStatusTextChanged += HandleStatusTextChanged;

        // Listen for battle outcomes broadcast by BattleManager.
        // LevelManager no longer needs a direct reference to BattleManager for outcomes.
        GameEvents.OnLevelWin  += HandleLevelWin;
        GameEvents.OnLevelLose += HandleLevelLose;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent stale references after scene unload.
        GameEvents.OnStatusTextChanged -= HandleStatusTextChanged;
        GameEvents.OnLevelWin          -= HandleLevelWin;
        GameEvents.OnLevelLose         -= HandleLevelLose;
    }

    private void Start()
    {
        // Broadcast initial gold so the UI label is correct from frame one.
        GameEvents.SetGold(currentGold);
        LoadLevel(_currentLevelIndex);
    }

    // -------------------------------------------------------------------------
    // GameEvents handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Receives status text updates from the event bus and forwards them to
    /// GameUIManager via another event — LevelManager never touches UI directly.
    /// </summary>
    private void HandleStatusTextChanged(string message)
    {
        // Re-broadcast so GameUIManager can update the TextMeshProUGUI component.
        // This keeps LevelManager free of any UI component references.
        // (GameUIManager subscribes to OnStatusTextChanged directly as well,
        //  so this handler is intentionally left as a pass-through hook for
        //  any future LevelManager-specific status logic.)
    }

    /// <summary>
    /// Handles the win signal broadcast by BattleManager via GameEvents.
    /// The empty rewardMessage parameter is ignored here; the full reward
    /// string is built locally where the economy data is available.
    /// </summary>
    private void HandleLevelWin(string _)
    {
        OnLevelWin();
    }

    /// <summary>
    /// Handles the loss signal broadcast by BattleManager via GameEvents.
    /// </summary>
    private void HandleLevelLose(string _)
    {
        OnLevelLose();
    }

    // -------------------------------------------------------------------------
    // Win / Lose logic
    // -------------------------------------------------------------------------

    public void OnLevelWin()
    {
        int unusedUnits  = DrawingManager.Instance.meleeLimit + DrawingManager.Instance.rangedLimit;
        int baseReward   = levels[_currentLevelIndex].goldReward;
        int bonusReward  = unusedUnits * goldPerUnusedUnit;

        AddGold(baseReward + bonusReward);

        string modeText      = isWaveMode ? "WAVE" : "LEVEL";
        string rewardMessage = $"{modeText} CLEARED!\nBase Reward: {baseReward} G\nBonus: {bonusReward} G";
        GameEvents.SetStatusText(rewardMessage);

        _currentLevelIndex++;

        if (_currentLevelIndex < levels.Count)
            StartCoroutine(NextLevelRoutine());
        else
            GameEvents.SetStatusText("VICTORY!\nAll tasks completed.");
    }

    public void OnLevelLose()
    {
        string randomJoke = defeatJokes[Random.Range(0, defeatJokes.Length)];
        GameEvents.SetStatusText(randomJoke);

        StartCoroutine(RestartLevelRoutine());
    }

    // -------------------------------------------------------------------------
    // Level loading
    // -------------------------------------------------------------------------

    private void LoadLevel(int index)
    {
        ClearBoard();
        BattleManager.Instance.ResetBattleState();

        if (levels == null || levels.Count <= index) return;

        LevelDataSO nextLevelData = levels[index];
        UnitSpawner.Instance.currentLevelData = nextLevelData;

        // Broadcast the new level index so GameUIManager can update the label.
        GameEvents.SetLevelIndex(index + 1, isWaveMode);

        if (index == 0)
            GameEvents.SetStatusText("1. Select Unit (Left Icons)\n2. Draw on Map\n3. Press WAR!");
        else
            GameEvents.SetStatusText(string.Empty);

        DrawingManager.Instance.SetupLimits(nextLevelData.meleeLimit, nextLevelData.rangedLimit);
        DrawingManager.Instance.meleeUnitData   = nextLevelData.meleeData;
        DrawingManager.Instance.rangedUnitData  = nextLevelData.rangedData;
        DrawingManager.Instance.currentSelectedUnit = nextLevelData.meleeData;
        DrawingManager.Instance.canDraw         = true;

        if (!isWaveMode)
            UnitSpawner.Instance.PrepareLevel();
    }

    // -------------------------------------------------------------------------
    // Economy
    // -------------------------------------------------------------------------

    public void AddGold(int amount)
    {
        currentGold += amount;
        // Broadcast the new total — GameUIManager updates the gold label.
        GameEvents.SetGold(currentGold);
    }

    // -------------------------------------------------------------------------
    // Coroutine-based delayed transitions (replaces fragile Invoke calls)
    // -------------------------------------------------------------------------

    private IEnumerator NextLevelRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (this == null) yield break; // Guard: object may have been destroyed
        LoadLevel(_currentLevelIndex);
    }

    private IEnumerator RestartLevelRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (this == null) yield break;
        LoadLevel(_currentLevelIndex);
    }

    // -------------------------------------------------------------------------
    // Board cleanup
    // -------------------------------------------------------------------------

    private void ClearBoard()
    {
        // FindObjectsInactive.Exclude ensures we only find units that are currently
        // ACTIVE in the scene. Units already returned to the pool are inactive and
        // must NOT be released again — doing so causes a double-release exception.
        BaseUnit[] remainingUnits = FindObjectsByType<BaseUnit>(FindObjectsInactive.Exclude);
        foreach (var unit in remainingUnits)
        {
            if (UnitFactory.Instance != null)
                UnitFactory.Instance.ReleaseUnit(unit);
            else
                Destroy(unit.gameObject);
        }
    }
}
