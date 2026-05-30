using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TDEV.Core;

/// <summary>
/// Manages level progression, economy, and board resets.
/// All UI updates are broadcast through GameEvents — LevelManager holds no
/// direct TextMeshProUGUI references. Win/loss signals are received from
/// GameEvents (fired by BattleManager) rather than called directly.
///
/// Save/Load: PlayerPrefs yerine GameSaveService (binary + AES) kullanır.
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

        GameEvents.OnStatusTextChanged += HandleStatusTextChanged;
        GameEvents.OnLevelWin          += HandleLevelWin;
        GameEvents.OnLevelLose         += HandleLevelLose;
    }

    private void Start()
{
    if (GameSaveService.Instance != null)
    {
        currentGold        = GameSaveService.Instance.GetGold();
        _currentLevelIndex = GameSaveService.Instance.GetLevelIndex();
    }

    GameEvents.SetGold(currentGold);
    StartCoroutine(WaitAndLoadLevel());
}

private IEnumerator WaitAndLoadLevel()
{
    float timeout = 3f;
    while (UnitSpawner.Instance == null && timeout > 0f)
    {
        timeout -= Time.deltaTime;
        yield return null;
    }

    if (UnitSpawner.Instance == null)
    {
        Debug.LogError("[LevelManager] UnitSpawner 3 saniye içinde hazır olmadı!");
        yield break;
    }

    LoadLevel(_currentLevelIndex);
}

    private void OnDestroy()
    {
        GameEvents.OnStatusTextChanged -= HandleStatusTextChanged;
        GameEvents.OnLevelWin          -= HandleLevelWin;
        GameEvents.OnLevelLose         -= HandleLevelLose;
    }



    // -------------------------------------------------------------------------
    // GameEvents handlers
    // -------------------------------------------------------------------------

    private void HandleStatusTextChanged(string message) { }

    private void HandleLevelWin(string _)  => OnLevelWin();
    private void HandleLevelLose(string _) => OnLevelLose();

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

        // Level index'i kaydet
        GameSaveService.Instance?.SetLevelIndex(_currentLevelIndex);

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

        GameEvents.SetLevelIndex(index + 1);

        if (index == 0)
            GameEvents.SetStatusText("Press WAR! to start the battle.");
        else
            GameEvents.SetStatusText(string.Empty);

        UnitSpawner.Instance.PrepareLevel();
    }

    // -------------------------------------------------------------------------
    // Economy
    // -------------------------------------------------------------------------

    public void AddGold(int amount)
    {
        currentGold += amount;
        GameSaveService.Instance?.SetGold(currentGold);
        GameEvents.SetGold(currentGold);
    }

    public void SpendGold(int amount)
    {
        currentGold -= amount;
        if (currentGold < 0) currentGold = 0;
        GameSaveService.Instance?.SetGold(currentGold);
        GameEvents.SetGold(currentGold);
    }

    // -------------------------------------------------------------------------
    // Coroutines
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
