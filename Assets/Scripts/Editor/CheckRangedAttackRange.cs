using UnityEngine;
using UnityEditor;

public class CheckRangedAttackRange
{
    [MenuItem("Debug/Check Ranged Attack Range")]
    public static void Execute()
    {
        // Load all RangedUnitDataSO assets
        string[] guids = AssetDatabase.FindAssets("t:RangedUnitDataSO");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RangedUnitDataSO data = AssetDatabase.LoadAssetAtPath<RangedUnitDataSO>(path);
            if (data != null)
            {
                Debug.Log($"[RangedData] {data.name} | attackRange(SO)={data.attackRange} | statProgression={(data.statProgression != null ? data.statProgression.name : "NULL")} | maxLevel={data.maxLevel}");
                if (data.statProgression != null)
                {
                    float rangeL1 = data.statProgression.EvaluateAttackRange(1, data.maxLevel);
                    float rangeMax = data.statProgression.EvaluateAttackRange(data.maxLevel, data.maxLevel);
                    Debug.Log($"  -> StatProgression attackRange: level1={rangeL1}, maxLevel={rangeMax}");
                }
            }
        }

        // Check the StatProgression asset directly
        StatProgressionSO prog = AssetDatabase.LoadAssetAtPath<StatProgressionSO>("Assets/Scripts/Data/Units/StatProgressionEnemy1SO.asset");
        if (prog != null)
        {
            int keyCount = prog.attackRangeCurve.keys.Length;
            Debug.Log($"[StatProgressionEnemy1SO] attackRangeCurve key count: {keyCount}");
            foreach (var key in prog.attackRangeCurve.keys)
            {
                Debug.Log($"  key -> time={key.time}, value={key.value}");
            }

            // Test evaluate
            float evalAt0 = prog.attackRangeCurve.Evaluate(0f);
            float evalAt1 = prog.attackRangeCurve.Evaluate(1f);
            Debug.Log($"  Evaluate(0)={evalAt0}, Evaluate(1)={evalAt1}");

            // Also check other curves
            Debug.Log($"  speedCurve keys: {prog.speedCurve.keys.Length}, Evaluate(0)={prog.speedCurve.Evaluate(0f)}, Evaluate(1)={prog.speedCurve.Evaluate(1f)}");
        }
        else
        {
            Debug.LogError("StatProgressionEnemy1SO not found!");
        }
    }
}
