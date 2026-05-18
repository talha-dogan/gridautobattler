using UnityEngine;
using TDEV.Core;

/// <summary>
/// Single Responsibility: UnitSpawner ONLY handles spawning mechanics.
///
/// Side-Scroller Layout (Lateral View):
/// ┌──────────────────────────────────────────────────────┐
/// │  X=0 (Player)              X=6  X=7 (Enemy)          │
/// │  Y=7  [P]  ──────────────  [E2] [E1]  Y=7            │
/// │  Y=6  [P]  ──────────────  [E2] [E1]  Y=6            │
/// │  ...                        ...  ...                  │
/// │  Y=0  [P]  ──────────────  [E2] [E1]  Y=0            │
/// └──────────────────────────────────────────────────────┘
///
/// Player units  → Column X=0 (far left),  rows Y=0..7  (max  8 units).
/// Enemy  units  → Column X=7 (far right), rows Y=0..7  (first 8 units).
///                 Overflow spills to X=6, rows Y=0..7  (next  8 units).
///                 Total enemy capacity: 16 units across two columns.
///
/// A "lane" is a horizontal strip defined by a shared Grid Y value.
/// Units in the same lane face each other directly across the X axis.
///
/// Each occupied node is marked IsOccupied = true so other systems can
/// query the grid for free cells without iterating the unit lists.
/// </summary>
public class UnitSpawner : MonoBehaviour
{
    public static UnitSpawner Instance { get; private set; }

    // ── Column constants ──────────────────────────────────────────────────────
    // Player units occupy the leftmost column.
    private const int PlayerSpawnColumn = 0;

    // Enemy primary column: the rightmost column (X=7).
    private const int EnemyPrimaryColumn = GridManager.Columns - 1;   // 7

    // Enemy overflow column: one step inward from the right edge (X=6).
    // Used when the primary column is full (more than 8 enemies in the formation).
    private const int EnemyOverflowColumn = GridManager.Columns - 2;  // 6

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
    /// Spawns enemy units from the formation asset using a two-column strategy:
    ///
    ///   Pass 1 — Primary column (X=7):
    ///     Fill rows Y=0..7 sequentially. Handles up to 8 enemies.
    ///
    ///   Pass 2 — Overflow column (X=6):
    ///     If the formation contains more than 8 units, continue filling
    ///     rows Y=0..7 on the adjacent inward column. Handles enemies 9–16.
    ///
    /// Any formation with more than 16 units will have the excess skipped
    /// with a warning, as the two-column capacity is the hard limit.
    /// </summary>
    private void SpawnEnemyFormation(EnemyFormationSO formation)
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("[UnitSpawner] GridManager.Instance is null — cannot spawn enemies.");
            return;
        }

        int unitIndex = 0;          // Tracks position within the formation list.
        int totalUnits = formation.units.Count;

        // ── Pass 1: Fill the primary enemy column (X=7) ───────────────────────
        for (int row = 0; row < GridManager.Rows && unitIndex < totalUnits; row++)
        {
            GridNode node = GridManager.Instance.GetNode(EnemyPrimaryColumn, row);
            if (node == null) continue;

            // Defensive guard: skip a lane that is already occupied.
            if (node.IsOccupied) continue;

            // Place the unit at the exact world-space center of this grid node.
            UnitFactory.Instance.CreateUnit(
                formation.units[unitIndex].unitData,
                node.WorldPosition,
                Team.Enemy);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            unitIndex++;
        }

        // ── Pass 2: Spill overflow enemies into the adjacent column (X=6) ─────
        // Only entered when the formation has more than 8 units.
        for (int row = 0; row < GridManager.Rows && unitIndex < totalUnits; row++)
        {
            GridNode node = GridManager.Instance.GetNode(EnemyOverflowColumn, row);
            if (node == null) continue;

            // Defensive guard: skip a lane that is already occupied.
            if (node.IsOccupied) continue;

            // Place the overflow unit at the exact world-space center of this node.
            UnitFactory.Instance.CreateUnit(
                formation.units[unitIndex].unitData,
                node.WorldPosition,
                Team.Enemy);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            unitIndex++;
        }

        // ── Overflow guard ────────────────────────────────────────────────────
        // If the formation still has units left after filling both columns (>16),
        // log a warning so designers know the formation exceeds the hard limit.
        if (unitIndex < totalUnits)
        {
            int skipped = totalUnits - unitIndex;
            Debug.LogWarning($"[UnitSpawner] Enemy formation has {skipped} unit(s) beyond the " +
                             $"16-unit two-column capacity. Extra units skipped.");
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
    /// Spawns all player units (melee first, then ranged) onto Column X=0 (far left),
    /// filling rows Y=0..7 sequentially from the bottom lane upward.
    ///
    /// Total units spawned = meleeLimit + rangedLimit.
    /// If the combined count exceeds 8 (the number of rows), extra units are skipped.
    /// </summary>
    private void SpawnPlayerUnits(LevelDataSO levelData)
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("[UnitSpawner] GridManager.Instance is null — cannot spawn player units.");
            return;
        }

        // Tracks the next available row on the player column.
        int row = 0;

        // ── Spawn Melee Units ─────────────────────────────────────────────────
        // Melee units fill the lower lanes first so they form the front line.
        for (int i = 0; i < levelData.meleeLimit; i++)
        {
            if (row >= GridManager.Rows)
            {
                Debug.LogWarning("[UnitSpawner] Player column is full. Remaining melee units skipped.");
                break;
            }

            GridNode node = GridManager.Instance.GetNode(PlayerSpawnColumn, row);
            if (node == null) { row++; continue; }

            // Defensive guard: skip a lane that is already occupied.
            if (node.IsOccupied) { row++; continue; }

            // Place the unit at the exact world-space center of this grid node.
            UnitFactory.Instance.CreateUnit(levelData.meleeData, node.WorldPosition, Team.Player);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            row++;
        }

        // ── Spawn Ranged Units ────────────────────────────────────────────────
        // Ranged units fill the remaining lanes directly above the melee line.
        for (int i = 0; i < levelData.rangedLimit; i++)
        {
            if (row >= GridManager.Rows)
            {
                Debug.LogWarning("[UnitSpawner] Player column is full. Remaining ranged units skipped.");
                break;
            }

            GridNode node = GridManager.Instance.GetNode(PlayerSpawnColumn, row);
            if (node == null) { row++; continue; }

            // Defensive guard: skip a lane that is already occupied.
            if (node.IsOccupied) { row++; continue; }

            // Place the unit at the exact world-space center of this grid node.
            UnitFactory.Instance.CreateUnit(levelData.rangedData, node.WorldPosition, Team.Player);

            // Mark the node so the grid reflects the current board state.
            node.IsOccupied = true;

            row++;
        }
    }
}
