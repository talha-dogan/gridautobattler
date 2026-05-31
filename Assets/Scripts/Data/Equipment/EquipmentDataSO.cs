using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// Data container for a single piece of equipment.
/// Holds stat bonuses applied to the unit that equips it, and an Addressable
/// sprite reference so the visual asset is only loaded into memory on demand.
/// </summary>
[CreateAssetMenu(fileName = "New Equipment", menuName = "TDEV/Equipment/Equipment Data")]
public class EquipmentDataSO : ScriptableObject
{
    [Header("Identity")]
    public string equipmentName;

    [Tooltip("Which slot this item occupies on the unit.")]
    public EquipmentSlot slot;

    // -------------------------------------------------------------------------
    // Stat Bonuses
    // -------------------------------------------------------------------------

    [Header("Stat Bonuses")]
    [Tooltip("Flat bonus added to the unit's maximum health.")]
    public float bonusHealth;

    [Tooltip("Flat bonus added to the unit's attack damage.")]
    public float bonusDamage;

    [Tooltip("Flat bonus subtracted from the unit's attack cooldown (lower = faster).")]
    public float bonusAttackSpeed;

    // -------------------------------------------------------------------------
    // Weapon Behaviour (only relevant for Weapon slot)
    // -------------------------------------------------------------------------

    [Header("Weapon Behaviour (Weapon slot only)")]
    [Tooltip("Defines how this weapon attacks (melee swing, ranged projectile, etc.). " +
             "Leave null for non-weapon equipment (helmet, vest, pants, shield).")]
    public WeaponBehaviourSO weaponBehaviour;

    // -------------------------------------------------------------------------
    // Visual Reference (Addressable)
    // -------------------------------------------------------------------------

    [Header("Visual")]
    [Tooltip("Addressable reference to the sprite displayed in the matching equipment slot.")]
    public AssetReferenceT<Sprite> spriteReference;
}
