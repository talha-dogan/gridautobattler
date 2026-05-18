using UnityEngine;
using System.Collections.Generic;

namespace TDEV.Core // Kodu bu zırhın içine alıyoruz
{
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

        [Header("AI Draft Options")]
        public List<EnemyCostData> availableEnemies;
    }

    [System.Serializable]
    public class EnemyCostData
    {
        public BaseUnitDataSO enemyData;
        public int spawnCost;

        [Header("Tactical Tags")]
        public bool isGoodAgainstRanged;
        public bool isGoodAgainstMelee;

        // Tank flag — carried over from DynamicWaveDataSO to keep a single
        // unified definition. Used by WaveDirector for future tactical weighting.
        public bool isTank;
    }
}