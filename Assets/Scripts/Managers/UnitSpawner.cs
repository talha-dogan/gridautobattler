using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TDEV.Core;

/// <summary>
/// Single Responsibility: UnitSpawner ONLY handles spawning mechanics.
///
/// Grid-based placement rules:
///   - Player units  → Row Y=0 (bottom row), filling columns left-to-right from X=0.
///   - Enemy units   → Row Y=7 (top row),    filling columns left-to-right from X=0.
///
/// Each occupied node is marked IsOccupied = true so other systems can query
/// the grid for free cells without iterating the unit lists.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance { get; private set; }

    // ── Row constants ─────────────────────────────────────────────────────────
    // Player units always spawn on the bottom row of the 8x8 grid.
    private const int PlayerSpawnRow = 0;

    // Enemy units always spawn on the top row of the 8x8 grid.
    private const int EnemySpawnRow = 7;

    [Header("Campaign Data")]
    public LevelDataSO currentLevelData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // ── Campaign ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LevelManager to set up the Campaign board.
    /// Spawns the enemy formation defined in the current level's ScriptableObject.
    /// </summary>
    public void PrepareLevel()
    {
        if (currentLevelData != null && currentLevelData.enemyFormation != null)
        {
            SpawnEnemyFormation(currentLevelData.enemyFormation);
            Debug.Log($"[UnitSpawner] Campaign Level '{currentLevelData.name}' deployed.");
        }
    }

    /// <summary>
    /// Spawns enemy units from the formation asset onto Row Y=7, left-to-right.
    /// Each unit in the formation list occupies the next available column.
    /// If the formation contains more units than columns (8), extra units are skipped.
    /// </summary>
    private void SpawnEnemyFormation(EnemyFormationSO formation)
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("[UnitSpawner] GridManager.Instance is null — cannot spawn enemies.");
            return;
        }

        int column = 0; // Start filling from the leftmost column.

        foreach (var placement in formation.units)
        {
            // Stop if we have run out of columns on the enemy row.
            if (column >= GridManager.Columns)
            {
                Debug.LogWarning("[UnitSpawner] Enemy formation has more units than grid columns. Extra units skipped.");
                break;
            }

            GridNode node = GridManager.Instance.GetNode(column, EnemySpawnRow);
            if (node == null) { column++; continue; }

            // Skip columns that are already occupied (defensive guard).
            if (node.IsOccupied) { column++; continue; }

            // Spawn the unit at the exact world-space center of this grid node.
            UnitFactory.Instance.CreateUnit(placement.unitData, node.WorldPosition, Team.Enemy);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            column++;
        }
    }

    // ── Battle start — entry point for the "WAR!" button ─────────────────────

    /// <summary>
    /// Called by the "WAR!" button in Campaign and Wave mode.
    /// Spawns player units onto the grid, then signals BattleManager to begin.
    /// </summary>
    public void StartBattle()
    {
        if (currentLevelData != null)
        {
            SpawnPlayerUnits(currentLevelData);
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartBattle();
        }
    }

    // ── Player grid spawning ──────────────────────────────────────────────────

    /// <summary>
    /// Spawns all player units (melee first, then ranged) onto Row Y=0,
    /// filling columns left-to-right starting at X=0.
    ///
    /// Total units spawned = meleeLimit + rangedLimit.
    /// If the combined count exceeds 8 (the number of columns), extra units are skipped.
    /// </summary>
    private void SpawnPlayerUnits(LevelDataSO levelData)
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("[UnitSpawner] GridManager.Instance is null — cannot spawn player units.");
            return;
        }

        int column = 0; // Tracks the next available column on the player row.

        // ── Spawn Melee Units ─────────────────────────────────────────────────
        for (int i = 0; i < levelData.meleeLimit; i++)
        {
            if (column >= GridManager.Columns)
            {
                Debug.LogWarning("[UnitSpawner] Player row is full. Remaining melee units skipped.");
                break;
            }

            GridNode node = GridManager.Instance.GetNode(column, PlayerSpawnRow);
            if (node == null) { column++; continue; }

            // Skip columns that are already occupied (defensive guard).
            if (node.IsOccupied) { column++; continue; }

            // Spawn the unit at the exact world-space center of this grid node.
            UnitFactory.Instance.CreateUnit(levelData.meleeData, node.WorldPosition, Team.Player);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            column++;
        }

        // ── Spawn Ranged Units ────────────────────────────────────────────────
        for (int i = 0; i < levelData.rangedLimit; i++)
        {
            if (column >= GridManager.Columns)
            {
                Debug.LogWarning("[UnitSpawner] Player row is full. Remaining ranged units skipped.");
                break;
            }

            GridNode node = GridManager.Instance.GetNode(column, PlayerSpawnRow);
            if (node == null) { column++; continue; }

            // Skip columns that are already occupied (defensive guard).
            if (node.IsOccupied) { column++; continue; }

            // Spawn the unit at the exact world-space center of this grid node.
            UnitFactory.Instance.CreateUnit(levelData.rangedData, node.WorldPosition, Team.Player);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            column++;
        }
    }
}
