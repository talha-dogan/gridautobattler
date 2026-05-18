using UnityEngine;

namespace TDEV.Core
{
    /// <summary>
    /// Defines all data for a single campaign level:
    /// player army limits, unit data references, the enemy formation, and the gold reward.
    /// The old AI cost-based wave drafting fields (availableEnemies / EnemyCostData)
    /// have been removed — the project now uses static EnemyFormationSO assets exclusively.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "TDEV/Level Data")]
    public class LevelDataSO : ScriptableObject
    {
        [Header("Player Army Limits")]
        public int meleeLimit;
        public int rangedLimit;
        public int totalUnitCapacity;

        [Header("Unit Data References")]
        public BaseUnitDataSO meleeData;
        public BaseUnitDataSO rangedData;

        [Header("Enemy Configuration")]
        public EnemyFormationSO enemyFormation;

        [Header("Level Rewards")]
        public int goldReward;
    }
}
