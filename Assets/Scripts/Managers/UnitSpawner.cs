using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TDEV.Core;

/// <summary>
/// Single Responsibility: UnitSpawner ONLY handles spawning mechanics.
/// AI drafting logic lives in WaveDirector.
/// UI text updates are broadcast through GameEvents — no direct UI references here.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance { get; private set; }

    [Header("Enemy Positioning")]
    // The center point where the enemy formation will be centered (e.g., X=5, Y=0)
    public Vector3 enemyCenterPos = new Vector3(5f, 0f, 0f);

    // Spread radius used by WaveDirector when placing AI units
    public float aiSpawnSpread = 2f;

    [Header("Campaign Data")]
    public LevelDataSO currentLevelData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // -------------------------------------------------------------------------
    // Campaign
    // -------------------------------------------------------------------------

    // Called by LevelManager to set up the Campaign board
    public void PrepareLevel()
    {
        if (currentLevelData != null && currentLevelData.enemyFormation != null)
        {
            SpawnEnemyFormation(currentLevelData.enemyFormation);
            Debug.Log($"Campaign Level: {currentLevelData.name} deployed.");
        }
    }

    private void SpawnEnemyFormation(EnemyFormationSO formation)
    {
        foreach (var placement in formation.units)
        {
            Vector3 spawnPos = enemyCenterPos + new Vector3(placement.offset.x, placement.offset.y, 0);
            UnitFactory.Instance.CreateUnit(placement.unitData, spawnPos, Team.Enemy);
        }
    }

    // -------------------------------------------------------------------------
    // Battle start — entry point for the "WAR!" button
    // -------------------------------------------------------------------------

    // Called by the "WAR!" button in Campaign & Wave mode
    public void StartBattle()
    {
        if (LevelManager.Instance != null && LevelManager.Instance.isWaveMode)
        {
            StartCoroutine(AiThinkingRoutine());
        }
        else
        {
            DrawingManager.Instance.canDraw = false;
            if (BattleManager.Instance != null) BattleManager.Instance.StartBattle();
        }
    }

    // -------------------------------------------------------------------------
    // Wave mode coroutine — orchestrates timing only; delegates AI work to
    // WaveDirector and UI updates to GameEvents.
    // -------------------------------------------------------------------------

    private IEnumerator AiThinkingRoutine()
    {
        DrawingManager.Instance.canDraw = false;

        // Notify UI via the event bus — no direct statusText reference needed
        GameEvents.SetStatusText("AI is thinking...");

        yield return new WaitForSeconds(1.5f);

        // Delegate all AI drafting + spawning to WaveDirector
        if (WaveDirector.Instance != null)
            WaveDirector.Instance.ExecuteAiDrafting(currentLevelData, enemyCenterPos, aiSpawnSpread);
        else
            Debug.LogError("WaveDirector is missing in the scene! Can't draft AI army.");

        yield return new WaitForSeconds(2.5f);

        // Clear the status message before battle begins
        GameEvents.SetStatusText("");

        if (BattleManager.Instance != null) BattleManager.Instance.StartBattle();
    }

    // -------------------------------------------------------------------------
    // Player path spawning
    // -------------------------------------------------------------------------

    public void SpawnUnitsOnPath(BaseUnitDataSO data, List<Vector3> points, int unitCount)
    {
        if (points == null || points.Count < 2 || data == null || unitCount <= 0) return;

        float totalPathLength = PathUtils.GetTotalLength(points);
        float actualSpacing = unitCount > 1 ? totalPathLength / (unitCount - 1) : 0;

        for (int i = 0; i < unitCount; i++)
        {
            float currentDist = i * actualSpacing;
            Vector3 spawnPos = unitCount > 1 ? PathUtils.GetPointAtDistance(points, currentDist) : points[0];
            UnitFactory.Instance.CreateUnit(data, spawnPos, Team.Player);
        }
    }
}
