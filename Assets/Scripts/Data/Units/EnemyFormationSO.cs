using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct UnitPlacement
{
    public BaseUnitDataSO unitData; // Melee mi Ranged mi?
    public Vector2 offset;          // Merkez noktaya göre konumu
}

[CreateAssetMenu(fileName = "NewEnemyFormation", menuName = "TDEV/Enemy Formation")]
public class EnemyFormationSO : ScriptableObject
{
    public List<UnitPlacement> units;
}