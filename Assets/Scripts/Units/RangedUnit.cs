using UnityEngine;

/// <summary>
/// A unit that attacks at range by firing projectiles.
///
/// POOL REFACTOR: RangedUnit no longer owns or manages an ObjectPool<Projectile>.
/// All projectile pooling is delegated to the centralised ProjectileFactory singleton,
/// which maintains one shared pool per unique projectile prefab. This means 20 ranged
/// units firing the same prefab share a single pool instead of each holding their own.
/// </summary>
public class RangedUnit : BaseUnit
{
    [Header("Ranged Setup")]
    [SerializeField] private RangedUnitDataSO rangedData;
    [SerializeField] private Transform firePoint;
    [SerializeField] private WeaponAimVisuals weaponAim;

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    public override void Initialize(BaseUnitDataSO data, Team team)
    {
        // 1. Cast to the specific data type first so rangedData is available
        //    before base.Initialize() runs (base reads shared stats from data).
        rangedData = data as RangedUnitDataSO;

        if (rangedData == null)
        {
            Debug.LogError(
                $"{gameObject.name}: Expected RangedUnitDataSO but received a different type. " +
                "Check the Inspector assignment.");
        }

        // 2. Base initialise — fills health, speed, attack stats, boots the FSM.
        base.Initialize(data, team);

        // No pool setup needed here — ProjectileFactory handles it lazily on first Get().
    }

    // -------------------------------------------------------------------------
    // Update
    // -------------------------------------------------------------------------

    protected override void Update()
    {
        base.Update();

        // Rotate weapon to face the current target while in the Attacking state.
        if (currentState == UnitState.Attacking && currentTarget != null && weaponAim != null)
        {
            weaponAim.UpdateAimVisuals(currentTarget.transform.position);
        }
    }

    // -------------------------------------------------------------------------
    // Attack
    // -------------------------------------------------------------------------

    public override void Attack(IDamageable target)
    {
        if (target == null || !(target is BaseUnit targetUnit)) return;
        if (rangedData == null || rangedData.projectilePrefab == null)  return;

        if (ProjectileFactory.Instance == null)
        {
            Debug.LogWarning(
                $"{gameObject.name}: ProjectileFactory not found in scene. Cannot fire.");
            return;
        }

        // Request a pooled projectile from the centralised factory.
        Projectile projectile = ProjectileFactory.Instance.Get(rangedData.projectilePrefab);
        if (projectile == null) return;

        // Position the projectile at the barrel tip.
        if (firePoint != null)
        {
            projectile.transform.position = firePoint.position;
            projectile.transform.rotation = firePoint.rotation;
        }

        // Use a safe fallback speed if the data value was left at zero.
        float pSpeed = (rangedData.projectileSpeed > 0f) ? rangedData.projectileSpeed : 20f;

        // Silah sesi — ateşli silah
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySoundAtPosition(SoundType.WeaponShoot, transform.position);

        // Launch — the projectile will return itself to the factory pool when done.
        projectile.Launch(targetUnit, attackDamage, pSpeed, unitTeam);
    }
}
