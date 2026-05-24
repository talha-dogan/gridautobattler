using UnityEngine;

// Inherits from BaseUnitDataSO, adding ONLY ranged specific data.
[CreateAssetMenu(fileName = "New Ranged Data", menuName = "Unit Data/Ranged Unit")]
public class RangedUnitDataSO : BaseUnitDataSO
{
    [Header("Ranged Specific")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 20f;
}