using UnityEngine;

/// <summary>
/// A player unit whose attack behaviour is determined at runtime by the
/// WeaponBehaviourSO assigned to its equipped weapon.
///
/// Replaces the old MeleeUnit. The prefab no longer needs to know whether
/// the unit is melee or ranged — the equipped WeaponBehaviourSO decides.
///
/// SOLID:
///   OCP — New weapon types require only a new SO asset, no code changes here.
///   DIP — Depends on the WeaponBehaviourSO abstraction, not concrete classes.
///   SRP — This class only orchestrates; attack logic lives in the SO.
/// </summary>
public class AdaptiveUnit : BaseUnit
{
    // -------------------------------------------------------------------------
    // Inspector references (set in prefab)
    // -------------------------------------------------------------------------

    [Header("Weapon Visuals")]
    [Tooltip("MeleeWeaponVisuals component on the Weapon_Renderer child. " +
             "Used by MeleeWeaponBehaviourSO for swing animations.")]
    [SerializeField] private MeleeWeaponVisuals _weaponVisuals;

    [Tooltip("WeaponAimVisuals component used by ranged weapons to rotate toward the target.")]
    [SerializeField] private WeaponAimVisuals _weaponAim;

    [Tooltip("Fire-point Transform used by ranged weapons as the projectile spawn origin.")]
    [SerializeField] private Transform _firePoint;

    // -------------------------------------------------------------------------
    // Public accessors — read by WeaponBehaviourSO subclasses
    // -------------------------------------------------------------------------

    /// <summary>Melee swing visual component. May be null if not assigned.</summary>
    public MeleeWeaponVisuals WeaponVisuals => _weaponVisuals;

    /// <summary>Aim visual component for ranged weapons. May be null if not assigned.</summary>
    public WeaponAimVisuals WeaponAim => _weaponAim;

    /// <summary>Projectile spawn point for ranged weapons. Falls back to unit root if null.</summary>
    public Transform FirePoint => _firePoint;

    // -------------------------------------------------------------------------
    // Active weapon behaviour (swapped at runtime by UnitEquipmentManager)
    // -------------------------------------------------------------------------

    private WeaponBehaviourSO _currentWeaponBehaviour;

    /// <summary>
    /// The currently active weapon strategy.
    /// Read by the Update loop to drive aim visuals for ranged weapons.
    /// </summary>
    public WeaponBehaviourSO CurrentWeaponBehaviour => _currentWeaponBehaviour;

    // -------------------------------------------------------------------------
    // Weapon swap API — called by UnitEquipmentManager
    // -------------------------------------------------------------------------

    /// <summary>
    /// Replaces the active weapon behaviour with a new one.
    /// Calls OnUnequip on the old behaviour and OnEquip on the new one.
    /// Passing null removes the weapon (unit will not attack until re-armed).
    /// Also overrides attackRange if the new behaviour specifies a non-zero range.
    /// </summary>
    public void SetWeaponBehaviour(WeaponBehaviourSO newBehaviour)
    {
        // Notify the outgoing behaviour so it can clean up (hide aim visuals, etc.)
        _currentWeaponBehaviour?.OnUnequip(this);

        _currentWeaponBehaviour = newBehaviour;

        // Notify the incoming behaviour so it can set up (show aim visuals, etc.)
        _currentWeaponBehaviour?.OnEquip(this);

        // Override attack range if the weapon specifies one.
        if (newBehaviour != null && newBehaviour.attackRange > 0f)
            attackRange = newBehaviour.attackRange;
    }

    // -------------------------------------------------------------------------
    // BaseUnit overrides
    // -------------------------------------------------------------------------

    protected override void Update()
    {
        base.Update();

        // Rotate weapon aim visuals toward the current target while attacking
        // (only relevant for ranged weapons that have a WeaponAimVisuals component).
        if (currentState == UnitState.Attacking &&
            currentTarget != null &&
            _weaponAim != null &&
            _currentWeaponBehaviour is RangedWeaponBehaviourSO)
        {
            _weaponAim.UpdateAimVisuals(currentTarget.transform.position);
        }
    }

    /// <summary>
    /// Delegates the attack to the active WeaponBehaviourSO.
    /// If no weapon is equipped the unit silently skips the attack.
    /// </summary>
    public override void Attack(IDamageable target)
    {
        if (_currentWeaponBehaviour == null)
        {
            // No weapon equipped — fall back to a bare-fist melee hit so the
            // unit is never completely helpless.
            BaseUnit targetUnit = target as BaseUnit;
            targetUnit?.TakeDamage(attackDamage);
            return;
        }

        _currentWeaponBehaviour.Execute(this, target, attackDamage);
    }
}
