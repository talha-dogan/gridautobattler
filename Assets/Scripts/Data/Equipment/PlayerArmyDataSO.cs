using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent army roster that travels between the Upgrade Scene and the Grid Scene.
/// Stores the base unit type and the full equipment loadout for each of the 8 player slots.
/// Assign this asset to both scenes so they share the same runtime state.
/// </summary>
[CreateAssetMenu(fileName = "PlayerArmyData", menuName = "TDEV/Equipment/Player Army Data")]
public class PlayerArmyDataSO : ScriptableObject
{
    // -------------------------------------------------------------------------
    // Nested data structure
    // -------------------------------------------------------------------------

    /// <summary>
    /// Represents a single unit slot in the player's army.
    /// Bundles the unit's base stats with its five equipment pieces.
    /// </summary>
    [Serializable]
    public class ArmySlot
    {
        [Tooltip("Base stats and prefab reference for this unit.")]
        public BaseUnitDataSO baseUnitData;

        // One optional equipment reference per slot type.
        // Null means the slot is empty (no bonus, no visual).
        [Tooltip("Helmet equipped on this unit. Null = empty slot.")]
        public EquipmentDataSO helmet;

        [Tooltip("Vest equipped on this unit. Null = empty slot.")]
        public EquipmentDataSO vest;

        [Tooltip("Pants equipped on this unit. Null = empty slot.")]
        public EquipmentDataSO pants;

        [Tooltip("Weapon equipped on this unit. Null = empty slot.")]
        public EquipmentDataSO weapon;

        [Tooltip("Shield equipped on this unit. Null = empty slot.")]
        public EquipmentDataSO shield;

        // ── Convenience helpers ───────────────────────────────────────────────

        /// <summary>
        /// Returns the EquipmentDataSO assigned to the given slot, or null if empty.
        /// </summary>
        public EquipmentDataSO GetEquipment(EquipmentSlot slot)
        {
            return slot switch
            {
                EquipmentSlot.Helmet => helmet,
                EquipmentSlot.Vest   => vest,
                EquipmentSlot.Pants  => pants,
                EquipmentSlot.Weapon => weapon,
                EquipmentSlot.Shield => shield,
                _                    => null
            };
        }

        /// <summary>
        /// Assigns an equipment piece to the correct slot field by slot type.
        /// Pass null to clear a slot.
        /// </summary>
        public void SetEquipment(EquipmentSlot slot, EquipmentDataSO equipment)
        {
            switch (slot)
            {
                case EquipmentSlot.Helmet: helmet = equipment; break;
                case EquipmentSlot.Vest:   vest   = equipment; break;
                case EquipmentSlot.Pants:  pants  = equipment; break;
                case EquipmentSlot.Weapon: weapon = equipment; break;
                case EquipmentSlot.Shield: shield = equipment; break;
            }
        }

        /// <summary>
        /// Iterates all five slots and returns each non-null EquipmentDataSO.
        /// Useful for bulk stat aggregation without branching on every slot.
        /// </summary>
        public IEnumerable<EquipmentDataSO> GetAllEquipment()
        {
            if (helmet != null) yield return helmet;
            if (vest    != null) yield return vest;
            if (pants   != null) yield return pants;
            if (weapon  != null) yield return weapon;
            if (shield  != null) yield return shield;
        }
    }

    // -------------------------------------------------------------------------
    // Army roster — exactly 8 slots (one per grid row)
    // -------------------------------------------------------------------------

    [Header("Army Roster (8 Slots)")]
    [Tooltip("One entry per player unit slot. Index 0 = bottom lane, Index 7 = top lane.")]
    public List<ArmySlot> armySlots = new List<ArmySlot>(8);

    // -------------------------------------------------------------------------
    // Runtime helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the ArmySlot at the given index, or null if out of range.
    /// </summary>
    public ArmySlot GetSlot(int index)
    {
        if (index < 0 || index >= armySlots.Count) return null;
        return armySlots[index];
    }

    /// <summary>
    /// Ensures the list always contains exactly 8 entries.
    /// Call this from an Editor Reset() or an initialisation script.
    /// </summary>
    public void EnsureCapacity()
    {
        while (armySlots.Count < 8)
            armySlots.Add(new ArmySlot());

        if (armySlots.Count > 8)
            armySlots.RemoveRange(8, armySlots.Count - 8);
    }
}
