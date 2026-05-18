using UnityEngine;

// Inherits from BaseUnitDataSO, used for close-combat units.
[CreateAssetMenu(fileName = "New Melee Data", menuName = "Unit Data/Melee Unit")]
public class MeleeUnitDataSO : BaseUnitDataSO
{
    [Header("Close Fight Settings")]

    // The rotation angle for the weapon swing (e.g., 90 for broadsword, 45 for dagger)
    public float swingAngle = 60f;

    // How long the swing animation takes in seconds (e.g., 0.4 for heavy, 0.1 for fast)
    public float swingDuration = 0.2f;
}