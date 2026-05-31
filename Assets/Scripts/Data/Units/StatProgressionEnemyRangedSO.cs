using UnityEngine;

/// <summary>
/// Ranged enemy birimler için özelleştirilmiş stat progression.
/// StatProgressionSO'dan türer; sadece default değerler farklıdır.
/// 
/// TEMEL FARK: attackRangeCurve değerleri 8-12 arasında ayarlanmıştır.
/// Bu sayede ranged enemy'ler uzaktan ateş edebilir, melee gibi davranmaz.
/// 
/// Kullanım: E-Bot-Ranged asset'lerinin "Stat Progression" alanına bu asset'i bağla.
/// </summary>
[CreateAssetMenu(fileName = "StatProgressionEnemyRangedSO", menuName = "TDEV/Stat Progression (Enemy Ranged)")]
public class StatProgressionEnemyRangedSO : StatProgressionSO
{
    private void Reset()
    {
        // Health: Ranged birimler melee'den biraz daha az dayanıklı
        healthCurve = new AnimationCurve(
            new Keyframe(0f, 60f),
            new Keyframe(1f, 400f)
        );

        // Damage
        damageCurve = new AnimationCurve(
            new Keyframe(0f, 8f),
            new Keyframe(1f, 80f)
        );

        // Speed: Ranged birimler biraz daha yavaş hareket eder
        speedCurve = new AnimationCurve(
            new Keyframe(0f, 2f),
            new Keyframe(1f, 4f)
        );

        // Attack Cooldown
        attackCooldownCurve = new AnimationCurve(
            new Keyframe(0f, 1.5f),
            new Keyframe(1f, 0.4f)
        );

        // *** KRITIK: Ranged için geniş saldırı menzili ***
        // Level 1 → 8 birim, Max Level → 12 birim
        // (StatProgressionEnemy1SO'da bu değer 1-3 idi, bu yüzden melee gibi davranıyordu)
        attackRangeCurve = new AnimationCurve(
            new Keyframe(0f, 8f),
            new Keyframe(1f, 12f)
        );

        // Gold reward
        goldRewardCurve = new AnimationCurve(
            new Keyframe(0f, 15f),
            new Keyframe(1f, 250f)
        );

        // Spawn pacing
        spawnPacingCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 0.3f);

        // Movement curve
        movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }
}
