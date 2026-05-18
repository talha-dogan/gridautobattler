using UnityEngine;
using System.Collections.Generic;
using TDEV.Core;

// EnemyCostData is defined once in TDEV.Core (LevelDataSO.cs).
// It was previously duplicated here as a global struct — that duplicate has
// been removed to eliminate the naming conflict and keep a single source of truth.

[CreateAssetMenu(fileName = "New Dynamic Wave", menuName = "AutoBattler/Dynamic Wave")]
public class DynamicWaveDataSO : ScriptableObject
{
    public string waveName;

    [Header("Pre-defined Formation (Optional)")]
    // Allow Wave system to accept campaign-style shapes.
    public EnemyFormationSO enemyFormation;

    [Header("Wave Economy (For AI)")]
    public int totalBudget;
    public List<EnemyCostData> availableEnemies;

    [Header("Player Drawing Limits")]
    public int playerMeleeLimit = 5;
    public int playerRangedLimit = 3;

    public BaseUnitDataSO playerMeleeData;
    public BaseUnitDataSO playerRangedData;
}
