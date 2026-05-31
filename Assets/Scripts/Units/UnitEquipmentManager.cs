using UnityEngine;

/// <summary>
/// Bridges the equipment data layer with the unit's runtime stats and visuals.
///
/// Responsibilities (Single Responsibility Principle):
///   1. Read the ArmySlot assigned to this unit from PlayerArmyDataSO.
///   2. Aggregate all equipment stat bonuses and apply them on top of the
///      base stats that BaseUnit.Initialize() already set.
///   3. Delegate every visual update to CharacterEquipmentVisuals.
///   4. When a Weapon slot item is equipped, push its WeaponBehaviourSO to
///      AdaptiveUnit so the unit's attack strategy updates at runtime.
///
/// Attach this component to the same GameObject as AdaptiveUnit (or BaseUnit subclass).
/// Call ApplyEquipment(armySlot) after BaseUnit.Initialize() has run.
/// </summary>
[RequireComponent(typeof(BaseUnit))]
public class UnitEquipmentManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector references
    // -------------------------------------------------------------------------

    [Header("Visual Layer")]
    [Tooltip("Handles async sprite loading for each equipment slot.")]
    [SerializeField] private CharacterEquipmentVisuals equipmentVisuals;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    private BaseUnit _unit;

    // Cached AdaptiveUnit reference — null if the unit is not an AdaptiveUnit.
    private AdaptiveUnit _adaptiveUnit;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _unit         = GetComponent<BaseUnit>();
        _adaptiveUnit = GetComponent<AdaptiveUnit>();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads all equipment from the given ArmySlot, applies stat bonuses,
    /// triggers async sprite loads, and pushes the weapon behaviour to
    /// AdaptiveUnit (if present).
    ///
    /// Must be called AFTER BaseUnit.Initialize().
    /// </summary>
    public void ApplyEquipment(PlayerArmyDataSO.ArmySlot armySlot)
    {
        if (armySlot == null)
        {
            Debug.LogWarning($"[UnitEquipmentManager] ApplyEquipment called with a null ArmySlot on '{gameObject.name}'.");
            return;
        }

        ResetEquipmentBonuses();

        foreach (EquipmentDataSO equipment in armySlot.GetAllEquipment())
        {
            ApplyStatBonus(equipment);
            TriggerVisualUpdate(equipment);
            TryApplyWeaponBehaviour(equipment);
        }
    }

    /// <summary>
    /// Equips a single item at runtime (e.g. drag-and-drop in the Upgrade Scene).
    /// </summary>
    public void EquipSingleItem(EquipmentDataSO equipment)
    {
        if (equipment == null) return;

        ApplyStatBonus(equipment);
        TriggerVisualUpdate(equipment);
        TryApplyWeaponBehaviour(equipment);
    }

    /// <summary>
    /// Removes all equipment bonuses, clears all visual slots, and strips the
    /// weapon behaviour from AdaptiveUnit.
    /// </summary>
    public void StripAllEquipment()
    {
        ResetEquipmentBonuses();

        if (equipmentVisuals != null)
            equipmentVisuals.ClearAllSlots();

        // Remove the active weapon behaviour so the unit doesn't keep attacking
        // with a weapon that has been unequipped.
        if (_adaptiveUnit != null)
            _adaptiveUnit.SetWeaponBehaviour(null);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void ApplyStatBonus(EquipmentDataSO equipment)
    {
        if (_unit == null || equipment == null) return;
        _unit.ApplyEquipmentBonus(equipment.bonusHealth, equipment.bonusDamage, equipment.bonusAttackSpeed);
    }

    private void TriggerVisualUpdate(EquipmentDataSO equipment)
    {
        if (equipmentVisuals == null)
        {
            Debug.LogWarning($"[UnitEquipmentManager] No CharacterEquipmentVisuals assigned on '{gameObject.name}'. " +
                             "Visual update skipped.");
            return;
        }

        equipmentVisuals.EquipItem(equipment);
    }

    /// <summary>
    /// If the equipment occupies the Weapon slot and carries a WeaponBehaviourSO,
    /// push it to AdaptiveUnit so the attack strategy updates immediately.
    /// Non-weapon slots and null behaviours are silently ignored.
    /// </summary>
    private void TryApplyWeaponBehaviour(EquipmentDataSO equipment)
    {
        if (equipment == null) return;
        if (equipment.slot != EquipmentSlot.Weapon) return;
        if (_adaptiveUnit == null) return;

        // weaponBehaviour may be null (e.g. a cosmetic-only weapon skin).
        // SetWeaponBehaviour handles null gracefully — it just clears the behaviour.
        _adaptiveUnit.SetWeaponBehaviour(equipment.weaponBehaviour);
    }

    private void ResetEquipmentBonuses()
    {
        if (_unit == null) return;
        _unit.ResetEquipmentBonuses();
    }
}
