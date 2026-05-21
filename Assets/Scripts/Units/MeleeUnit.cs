using UnityEngine;

public class MeleeUnit : BaseUnit
{
    [Header("Melee Setup")]
    [SerializeField] private MeleeUnitDataSO meleeData;
    [SerializeField] private MeleeWeaponVisuals weaponVisuals;

    public override void Initialize(BaseUnitDataSO data, Team team)
    {
        base.Initialize(data, team);
        meleeData = data as MeleeUnitDataSO; // Cast to get specific melee data
    }

    public override void Attack(IDamageable target)
    {
        BaseUnit targetUnit = target as BaseUnit;
        if (targetUnit == null || meleeData == null) return;

        // Pass the specific swing logic for THIS weapon from the Data SO
        if (weaponVisuals != null)
        {
            weaponVisuals.SwingWeapon(meleeData.swingAngle, meleeData.swingDuration);
        }

        // Silah sesi — yakın dövüş salınımı
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySoundAtPosition(SoundType.WeaponMeleeSwing, transform.position);

        targetUnit.TakeDamage(attackDamage);
    }
}