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

    [Header("Level Sequence")]
    public List<LevelDataSO> levels;

    private int _currentLevelIndex = 0;

    [Header("Economy Settings")]
    public int currentGold = 0;
    public int goldPerUnusedUnit = 25;

    // Defines the key used to save and load gold from PlayerPrefs
    private const string GOLD_SAVE_KEY = "PlayerGold";

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
        GameEvents.OnLevelWin += HandleLevelWin;
        GameEvents.OnLevelLose += HandleLevelLose;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent stale references after scene unload.
        GameEvents.OnStatusTextChanged -= HandleStatusTextChanged;
        GameEvents.OnLevelWin -= HandleLevelWin;
        GameEvents.OnLevelLose -= HandleLevelLose;
    }

    private void Start()
    {
        // Load the saved gold amount from device storage. Default is 0 if no save exists.
        currentGold = PlayerPrefs.GetInt(GOLD_SAVE_KEY, 0);

        // Broadcast initial gold so the UI label is correct from frame one.
        GameEvents.SetGold(currentGold);
        LoadLevel(_currentLevelIndex);
    }

    // -------------------------------------------------------------------------
    // GameEvents handlers
    // -------------------------------------------------------------------------

    private void HandleStatusTextChanged(string message)
    {
        // Re-broadcast so GameUIManager can update the TextMeshProUGUI component.
    }

    private void HandleLevelWin(string _)
    {
        OnLevelWin();
    }

    private void HandleLevelLose(string _)
    {
        OnLevelLose();
    }

    // -------------------------------------------------------------------------
    // Win / Lose logic
    // -------------------------------------------------------------------------

    public void OnLevelWin()
    {
        int baseReward = levels[_currentLevelIndex].goldReward;

        AddGold(baseReward);

        string rewardMessage = $"LEVEL CLEARED!\nBase Reward: {baseReward} G";
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
        GameEvents.SetLevelIndex(index + 1);

        if (index == 0)
            GameEvents.SetStatusText("Press WAR! to start the battle.");
        else
            GameEvents.SetStatusText(string.Empty);

        // Always prepare the level in grid/campaign mode.
        UnitSpawner.Instance.PrepareLevel();
    }

    // -------------------------------------------------------------------------
    // Economy
    // -------------------------------------------------------------------------

    public void AddGold(int amount)
    {
        currentGold += amount;

        // Save the updated gold amount to device storage
        PlayerPrefs.SetInt(GOLD_SAVE_KEY, currentGold);
        PlayerPrefs.Save();

        // Broadcast the new total — GameUIManager and PawnShopUI updates the gold label.
        GameEvents.SetGold(currentGold);
    }

    /// <summary>
    /// Call this method when spending gold (e.g., buying a pawn).
    /// </summary>
    public void SpendGold(int amount)
    {
        currentGold -= amount;

        // Ensure it doesn't drop below zero
        if (currentGold < 0) currentGold = 0;

        // Save the updated gold amount to device storage
        PlayerPrefs.SetInt(GOLD_SAVE_KEY, currentGold);
        PlayerPrefs.Save();

        // Broadcast the new total
        GameEvents.SetGold(currentGold);
    }

    // -------------------------------------------------------------------------
    // Coroutine-based delayed transitions
    // -------------------------------------------------------------------------

    private IEnumerator NextLevelRoutine()
    {
        yield return new WaitForSeconds(3f);
        if (this == null) yield break;
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
        BaseUnit[] remainingUnits = FindObjectsByType<BaseUnit>(FindObjectsInactive.Exclude);
        foreach (var unit in remainingUnits)
        {
            if (UnitFactory.Instance != null)
                UnitFactory.Instance.ReleaseUnit(unit);
            else
                Destroy(unit.gameObject);
        }

        if (GridManager.Instance != null)
            GridManager.Instance.ResetGrid();
    }
}