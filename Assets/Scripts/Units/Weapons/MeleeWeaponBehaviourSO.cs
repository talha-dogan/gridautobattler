using UnityEngine;

/// <summary>
/// Concrete melee weapon strategy.
/// Deals direct damage and triggers a swing animation on the weapon visual.
///
/// Create via: Assets → Create → TDEV/Weapon Behaviour/Melee
/// </summary>
[CreateAssetMenu(
    fileName = "New MeleeWeaponBehaviour",
    menuName  = "TDEV/Weapon Behaviour/Melee")]
public class MeleeWeaponBehaviourSO : WeaponBehaviourSO
{
    [Header("Swing Settings")]
    [Tooltip("Rotation angle of the weapon swing arc (degrees).")]
    public float swingAngle    = 60f;

    [Tooltip("Total duration of the swing animation (seconds).")]
    public float swingDuration = 0.2f;

    // -------------------------------------------------------------------------

    public override void Execute(AdaptiveUnit owner, IDamageable target, float damage)
    {
        BaseUnit targetUnit = target as BaseUnit;
        if (targetUnit == null) return;

        // Trigger swing animation if a weapon visual is present.
        MeleeWeaponVisuals visuals = owner.WeaponVisuals;
        if (visuals != null)
            visuals.SwingWeapon(swingAngle, swingDuration);

        // Melee swing sound.
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySoundAtPosition(SoundType.WeaponMeleeSwing, owner.transform.position);

        targetUnit.TakeDamage(damage);
    }
}
