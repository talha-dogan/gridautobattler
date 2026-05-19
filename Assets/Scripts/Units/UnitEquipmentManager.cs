using UnityEngine;

/// <summary>
/// Bridges the equipment data layer with the unit's runtime stats and visuals.
///
/// Responsibilities (Single Responsibility Principle):
///   1. Read the ArmySlot assigned to this unit from PlayerArmyDataSO.
///   2. Aggregate all equipment stat bonuses and apply them on top of the
///      base stats that BaseUnit.Initialize() already set.
///   3. Delegate every visual update to CharacterEquipmentVisuals — this
///      component never touches SpriteRenderers directly.
///
/// Attach this component to the same GameObject as BaseUnit (or a subclass).
/// Call ApplyEquipment(armySlot) after BaseUnit.Initialize() has run so that
/// the base stats are already cached before bonuses are added.
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

    // Cached reference to the owning unit — used to apply stat bonuses.
    private BaseUnit _unit;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _unit = GetComponent<BaseUnit>();
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads all equipment from the given ArmySlot, applies stat bonuses to the
    /// unit's cached fields, and triggers async sprite loads for each filled slot.
    ///
    /// Must be called AFTER BaseUnit.Initialize() so that base stats are already
    /// written to the unit's protected fields before bonuses are stacked on top.
    /// </summary>
    public void ApplyEquipment(PlayerArmyDataSO.ArmySlot armySlot)
    {
        if (armySlot == null)
        {
            Debug.LogWarning($"[UnitEquipmentManager] ApplyEquipment called with a null ArmySlot on '{gameObject.name}'.");
            return;
        }

        // Reset any previously applied bonuses before re-applying the new loadout.
        // This is important when a unit is recycled from the pool and re-initialized.
        ResetEquipmentBonuses();

        // Aggregate bonuses from every filled slot and push visuals simultaneously.
        foreach (EquipmentDataSO equipment in armySlot.GetAllEquipment())
        {
            ApplyStatBonus(equipment);
            TriggerVisualUpdate(equipment);
        }
    }

    /// <summary>
    /// Equips a single item at runtime (e.g. drag-and-drop in the Upgrade Scene).
    /// Applies the stat delta and updates the visual for the affected slot only.
    /// </summary>
    public void EquipSingleItem(EquipmentDataSO equipment)
    {
        if (equipment == null) return;

        ApplyStatBonus(equipment);
        TriggerVisualUpdate(equipment);
    }

    /// <summary>
    /// Removes all equipment bonuses and clears all visual slots.
    /// Call this before returning the unit to the pool.
    /// </summary>
    public void StripAllEquipment()
    {
        ResetEquipmentBonuses();

        if (equipmentVisuals != null)
            equipmentVisuals.ClearAllSlots();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds the equipment's stat bonuses directly to the unit's protected fields.
    /// BaseUnit exposes these fields as protected so subclasses and trusted
    /// components in the same assembly can modify them without a full re-init.
    /// </summary>
    private void ApplyStatBonus(EquipmentDataSO equipment)
    {
        if (_unit == null || equipment == null) return;

        // Apply bonuses through the public method on BaseUnit so the unit can
        // broadcast any relevant events (e.g. OnHealthChanged after a health buff).
        _unit.ApplyEquipmentBonus(equipment.bonusHealth, equipment.bonusDamage, equipment.bonusAttackSpeed);
    }

    /// <summary>
    /// Forwards the equipment data to CharacterEquipmentVisuals for async sprite loading.
    /// No-ops gracefully if the visual component is not assigned.
    /// </summary>
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
    /// Resets all equipment-derived stat bonuses on the unit back to zero.
    /// Called before re-applying a new loadout to avoid stacking bonuses across
    /// multiple Initialize/ApplyEquipment cycles (e.g. object pool reuse).
    /// </summary>
    private void ResetEquipmentBonuses()
    {
        if (_unit == null) return;

        // Passing all-zero values signals a full bonus reset.
        _unit.ResetEquipmentBonuses();
    }
}
