using UnityEngine;
using TDEV.Core;
using System.Collections.Generic;

/// <summary>
/// Single Responsibility: UnitSpawner ONLY handles spawning mechanics.
///
/// Side-Scroller Layout (Lateral View):
/// ┌──────────────────────────────────────────────────────┐
/// │  X=0 (Player)              X=6  X=7 (Enemy)          │
/// │  Y=7  [P]  ──────────────  [E2] [E1]  Y=7            │
/// │  Y=6  [P]  ──────────────  [E2] [E1]  Y=6            │
/// │  ...                       ...  ...                  │
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

    [Header("Player Army Data (From Upgrade Scene)")]
    [Tooltip("ScriptableObject carrying the equipment and character selections made in the Upgrade Scene. " +
             "The same asset must be assigned in both scenes.")]
    [SerializeField] public PlayerArmyDataSO playerArmyData;

    [Tooltip("The base prefab for spawning player units. " +
             "One is spawned per active slot in PlayerArmyDataSO.")]
    [SerializeField] public GameObject playerPawnPrefab;

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
    /// Spawns enemy units from the formation asset using a two-column strategy.
    /// Handles up to 16 enemies.
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
            if (node.IsOccupied) continue;

            UnitFactory.Instance.CreateUnit(
                formation.units[unitIndex].unitData,
                node.WorldPosition,
                Team.Enemy);

            node.IsOccupied = true;
            unitIndex++;
        }

        // ── Pass 2: Spill overflow enemies into the adjacent column (X=6) ─────
        for (int row = 0; row < GridManager.Rows && unitIndex < totalUnits; row++)
        {
            GridNode node = GridManager.Instance.GetNode(EnemyOverflowColumn, row);
            if (node == null) continue;
            if (node.IsOccupied) continue;

            UnitFactory.Instance.CreateUnit(
                formation.units[unitIndex].unitData,
                node.WorldPosition,
                Team.Enemy);

            node.IsOccupied = true;
            unitIndex++;
        }

        // ── Overflow guard ────────────────────────────────────────────────────
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
    /// Spawns player units directly from the Army Roster, then signals BattleManager to begin.
    /// </summary>
    public void StartBattle()
    {
        if (playerArmyData != null && playerPawnPrefab != null)
        {
            SpawnPlayerUnitsFromArmy();
        }
        else
        {
            Debug.LogError("[UnitSpawner] PlayerArmyDataSO or PlayerPawnPrefab is missing! Cannot spawn player units.");
        }

        if (BattleManager.Instance != null)
        {
            BattleManager.Instance.StartBattle();
        }
    }

    // ── Player grid spawning — Army Roster (Upgrade Scene) ───────────────────

    /// <summary>
    /// Spawns player units from the PlayerArmyDataSO army roster.
    ///
    /// Rules:
    ///  • Only slots within unlockedPawnCount are spawned.
    ///  • Each spawned unit gets its equipment applied via UnitEquipmentManager.
    ///  • Slots are filled from row 0 upward on Column X=0.
    /// </summary>
    private void SpawnPlayerUnitsFromArmy()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("[UnitSpawner] GridManager.Instance is null — cannot spawn player units from army.");
            return;
        }

        playerArmyData.EnsureCapacity();

        int row = 0;
        int unlockedCount = Mathf.Clamp(playerArmyData.unlockedPawnCount, 1, 8);

        for (int slotIndex = 0; slotIndex < unlockedCount; slotIndex++)
        {
            if (row >= GridManager.Rows)
            {
                Debug.LogWarning("[UnitSpawner] Player column is full. Remaining army slots skipped.");
                break;
            }

            PlayerArmyDataSO.ArmySlot armySlot = playerArmyData.GetSlot(slotIndex);
            if (armySlot == null) { row++; continue; }

            GridNode node = GridManager.Instance.GetNode(PlayerSpawnColumn, row);
            if (node == null || node.IsOccupied) { row++; continue; }

            // Spawn the pawn prefab at the grid node's world position.
            GameObject go = Instantiate(playerPawnPrefab, node.WorldPosition, Quaternion.identity);
            go.name = $"Player_Pawn_{slotIndex}";

            BaseUnit unit = go.GetComponent<BaseUnit>();
            if (unit == null)
            {
                Debug.LogWarning($"[UnitSpawner] playerPawnPrefab '{playerPawnPrefab.name}' has no BaseUnit component. Slot {slotIndex} skipped.");
                Destroy(go);
                row++;
                continue;
            }

            // Use the slot's baseUnitData if available; otherwise fall back to the default melee data as a safety net.
            BaseUnitDataSO dataToUse = armySlot.baseUnitData;
            if (dataToUse == null && currentLevelData != null)
                dataToUse = currentLevelData.meleeData;

            if (dataToUse == null)
            {
                Debug.LogWarning($"[UnitSpawner] No BaseUnitDataSO for army slot {slotIndex} and no fallback in LevelDataSO. Slot skipped.");
                Destroy(go);
                row++;
                continue;
            }

            // Initialise base stats.
            unit.Initialize(dataToUse, Team.Player);

            // Apply equipment bonuses + visuals from the army slot.
            UnitEquipmentManager equipManager = go.GetComponent<UnitEquipmentManager>();
            if (equipManager != null)
            {
                equipManager.ApplyEquipment(armySlot);
            }
            else
            {
                Debug.LogWarning($"[UnitSpawner] playerPawnPrefab '{playerPawnPrefab.name}' has no UnitEquipmentManager. Equipment skipped for slot {slotIndex}.");
            }

            // Register with BattleManager.
            if (BattleManager.Instance != null)
                BattleManager.Instance.RegisterUnit(unit);

            node.IsOccupied = true;
            row++;

            Debug.Log($"[UnitSpawner] Army slot {slotIndex} spawned at row {row - 1} with data '{dataToUse.unitName}'.");
        }
    }
}