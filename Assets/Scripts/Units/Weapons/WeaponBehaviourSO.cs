using UnityEngine;

/// <summary>
/// Abstract Strategy base for weapon behaviour.
/// Subclass this SO to define how a weapon attacks (melee swing, projectile, etc.).
///
/// SOLID:
///   OCP — New weapon types are new SO assets, no existing code changes.
///   DIP — AdaptiveUnit depends on this abstraction, not on concrete classes.
///   SRP — Each subclass owns exactly one attack behaviour.
/// </summary>
public abstract class WeaponBehaviourSO : ScriptableObject
{
    /// <summary>
    /// The attack range this weapon imposes on the unit that equips it.
    /// Overrides the unit's base attackRange while this weapon is active.
    /// </summary>
    [Header("Range Override")]
    [Tooltip("Attack range the unit will use while this weapon is equipped. " +
             "Set to 0 to keep the unit's base range.")]
    public float attackRange = 0f;

    /// <summary>
    /// Execute one attack tick.
    /// Called by AdaptiveUnit.Attack() every time the attack cooldown fires.
    /// </summary>
    /// <param name="owner">The unit performing the attack.</param>
    /// <param name="target">The damageable target.</param>
    /// <param name="damage">Final damage value (base + equipment bonuses already applied).</param>
    public abstract void Execute(AdaptiveUnit owner, IDamageable target, float damage);

    /// <summary>
    /// Called once when this weapon is equipped on a unit.
    /// Use for one-time setup (e.g. caching a fire-point reference).
    /// </summary>
    public virtual void OnEquip(AdaptiveUnit owner) { }

    /// <summary>
    /// Called once when this weapon is unequipped or replaced.
    /// Use for cleanup (e.g. stopping coroutines, hiding aim visuals).
    /// </summary>
    public virtual void OnUnequip(AdaptiveUnit owner) { }
}
