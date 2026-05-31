using UnityEngine;

/// <summary>
/// Concrete ranged weapon strategy.
/// Fires a pooled projectile from the unit's fire-point toward the target.
///
/// Create via: Assets → Create → TDEV/Weapon Behaviour/Ranged
/// </summary>
[CreateAssetMenu(
    fileName = "New RangedWeaponBehaviour",
    menuName  = "TDEV/Weapon Behaviour/Ranged")]
public class RangedWeaponBehaviourSO : WeaponBehaviourSO
{
    [Header("Projectile Settings")]
    [Tooltip("Projectile prefab that carries a Projectile component.")]
    public GameObject projectilePrefab;

    [Tooltip("Travel speed of the fired projectile (units/second).")]
    public float projectileSpeed = 20f;

    // -------------------------------------------------------------------------

    public override void Execute(AdaptiveUnit owner, IDamageable target, float damage)
    {
        BaseUnit targetUnit = target as BaseUnit;
        if (targetUnit == null) return;

        if (projectilePrefab == null)
        {
            Debug.LogWarning($"[RangedWeaponBehaviourSO] '{name}' has no projectilePrefab assigned.");
            return;
        }

        if (ProjectileFactory.Instance == null)
        {
            Debug.LogWarning($"[RangedWeaponBehaviourSO] ProjectileFactory not found in scene.");
            return;
        }

        Projectile projectile = ProjectileFactory.Instance.Get(projectilePrefab);
        if (projectile == null) return;

        // Position at the unit's fire-point (or unit root if none assigned).
        Transform firePoint = owner.FirePoint;
        if (firePoint != null)
        {
            projectile.transform.position = firePoint.position;
            projectile.transform.rotation = firePoint.rotation;
        }
        else
        {
            projectile.transform.position = owner.transform.position;
            projectile.transform.rotation = Quaternion.identity;
        }

        float speed = projectileSpeed > 0f ? projectileSpeed : 20f;

        // Ranged shoot sound.
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySoundAtPosition(SoundType.WeaponShoot, owner.transform.position);

        projectile.Launch(targetUnit, damage, speed, owner.unitTeam);
    }

    public override void OnEquip(AdaptiveUnit owner)
    {
        // Show aim visuals when a ranged weapon is equipped.
        if (owner.WeaponAim != null)
            owner.WeaponAim.gameObject.SetActive(true);
    }

    public override void OnUnequip(AdaptiveUnit owner)
    {
        // Hide aim visuals when the ranged weapon is removed.
        if (owner.WeaponAim != null)
            owner.WeaponAim.gameObject.SetActive(false);
    }
}
