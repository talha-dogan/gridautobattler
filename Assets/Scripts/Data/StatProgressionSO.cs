using UnityEngine;

/// <summary>
/// AnimationCurve tabanlı stat ilerleme sistemi.
///
/// Kullanım:
///   Her stat için bir AnimationCurve tanımlanır.
///   X ekseni: normalised level (0 = level 1, 1 = max level)
///   Y ekseni: stat değeri (örn. 0-1000 HP)
///
///   float hp = statProgression.EvaluateHealth(currentLevel, maxLevel);
///
/// Bu sayede lineer formül yerine tasarımcı dostu eğriler kullanılabilir.
/// </summary>
[CreateAssetMenu(fileName = "NewStatProgression", menuName = "TDEV/Stat Progression")]
public class StatProgressionSO : ScriptableObject
{
    [Header("─── Stat Progression Curves ───────────────────────────────")]
    [Tooltip("X: normalised level (0-1), Y: max health değeri")]
    public AnimationCurve healthCurve = AnimationCurve.Linear(0f, 50f, 1f, 500f);

    [Tooltip("X: normalised level (0-1), Y: attack damage değeri")]
    public AnimationCurve damageCurve = AnimationCurve.Linear(0f, 10f, 1f, 100f);

    [Tooltip("X: normalised level (0-1), Y: move speed değeri")]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 2f, 1f, 5f);

    [Tooltip("X: normalised level (0-1), Y: attack cooldown (saniye, düşük = hızlı)")]
    public AnimationCurve attackCooldownCurve = AnimationCurve.Linear(0f, 2f, 1f, 0.5f);

    [Tooltip("X: normalised level (0-1), Y: attack range değeri")]
    public AnimationCurve attackRangeCurve = AnimationCurve.Linear(0f, 1f, 1f, 3f);

    [Header("─── Gold Reward Curve ────────────────────────────────────")]
    [Tooltip("X: normalised level (0-1), Y: gold ödülü")]
    public AnimationCurve goldRewardCurve = AnimationCurve.Linear(0f, 10f, 1f, 200f);

    [Header("─── Spawn Pacing Curve ──────────────────────────────────")]
    [Tooltip("X: normalised wave progress (0-1), Y: spawn aralığı (saniye). " +
             "Düşük değer = daha hızlı spawn.")]
    public AnimationCurve spawnPacingCurve = AnimationCurve.EaseInOut(0f, 2f, 1f, 0.3f);

    [Header("─── Movement Curve ──────────────────────────────────────")]
    [Tooltip("X: normalised hareket süresi (0-1), Y: hız çarpanı. " +
             "Başlangıçta yavaş, ortada hızlı, sonda yavaş gibi efektler için.")]
    public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Belirtilen level için max health değerini döner.
    /// </summary>
    public float EvaluateHealth(int currentLevel, int maxLevel)
        => healthCurve.Evaluate(Normalize(currentLevel, maxLevel));

    /// <summary>
    /// Belirtilen level için attack damage değerini döner.
    /// </summary>
    public float EvaluateDamage(int currentLevel, int maxLevel)
        => damageCurve.Evaluate(Normalize(currentLevel, maxLevel));

    /// <summary>
    /// Belirtilen level için move speed değerini döner.
    /// </summary>
    public float EvaluateSpeed(int currentLevel, int maxLevel)
        => speedCurve.Evaluate(Normalize(currentLevel, maxLevel));

    /// <summary>
    /// Belirtilen level için attack cooldown değerini döner.
    /// </summary>
    public float EvaluateAttackCooldown(int currentLevel, int maxLevel)
        => attackCooldownCurve.Evaluate(Normalize(currentLevel, maxLevel));

    /// <summary>
    /// Belirtilen level için attack range değerini döner.
    /// </summary>
    public float EvaluateAttackRange(int currentLevel, int maxLevel)
        => attackRangeCurve.Evaluate(Normalize(currentLevel, maxLevel));

    /// <summary>
    /// Belirtilen level için gold ödülünü döner.
    /// </summary>
    public int EvaluateGoldReward(int currentLevel, int maxLevel)
        => Mathf.RoundToInt(goldRewardCurve.Evaluate(Normalize(currentLevel, maxLevel)));

    /// <summary>
    /// Wave progress'e göre spawn aralığını döner (saniye).
    /// waveProgress: 0 = wave başlangıcı, 1 = wave sonu.
    /// </summary>
    public float EvaluateSpawnPacing(float waveProgress)
        => spawnPacingCurve.Evaluate(Mathf.Clamp01(waveProgress));

    /// <summary>
    /// Hareket süresi için eased t değerini döner.
    /// normalizedTime: 0-1 arası geçen süre.
    /// </summary>
    public float EvaluateMovement(float normalizedTime)
        => movementCurve.Evaluate(Mathf.Clamp01(normalizedTime));

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private float Normalize(int current, int max)
    {
        if (max <= 1) return 0f;
        return Mathf.Clamp01((float)(current - 1) / (max - 1));
    }
}
