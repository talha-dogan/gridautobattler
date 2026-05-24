using UnityEngine;

/// <summary>
/// Tüm easing fonksiyonlarını içeren statik utility sınıfı.
///
/// Kullanım:
///   float t = EasingLibrary.Apply(EaseType.EaseOutBounce, 0f, 1f, elapsed, duration);
///   float t = EasingLibrary.Evaluate(EaseType.EaseInQuad, normalizedT); // 0-1 arası
///
/// Referans: https://easings.net/
/// </summary>
public static class EasingLibrary
{
    // ─────────────────────────────────────────────────────────────────────────
    // Ana API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Normalised t (0-1) değerine easing uygular ve 0-1 arası sonuç döner.
    /// </summary>
    public static float Evaluate(EaseType ease, float t)
    {
        t = Mathf.Clamp01(t);
        return ease switch
        {
            EaseType.Linear         => t,
            EaseType.EaseInQuad     => EaseInQuad(t),
            EaseType.EaseOutQuad    => EaseOutQuad(t),
            EaseType.EaseInOutQuad  => EaseInOutQuad(t),
            EaseType.EaseInCubic    => EaseInCubic(t),
            EaseType.EaseOutCubic   => EaseOutCubic(t),
            EaseType.EaseInOutCubic => EaseInOutCubic(t),
            EaseType.EaseInQuart    => EaseInQuart(t),
            EaseType.EaseOutQuart   => EaseOutQuart(t),
            EaseType.EaseInOutQuart => EaseInOutQuart(t),
            EaseType.EaseInSine     => EaseInSine(t),
            EaseType.EaseOutSine    => EaseOutSine(t),
            EaseType.EaseInOutSine  => EaseInOutSine(t),
            EaseType.EaseInExpo     => EaseInExpo(t),
            EaseType.EaseOutExpo    => EaseOutExpo(t),
            EaseType.EaseInOutExpo  => EaseInOutExpo(t),
            EaseType.EaseInBack     => EaseInBack(t),
            EaseType.EaseOutBack    => EaseOutBack(t),
            EaseType.EaseInOutBack  => EaseInOutBack(t),
            EaseType.EaseInElastic  => EaseInElastic(t),
            EaseType.EaseOutElastic => EaseOutElastic(t),
            EaseType.EaseInBounce   => EaseInBounce(t),
            EaseType.EaseOutBounce  => EaseOutBounce(t),
            _                       => t,
        };
    }

    /// <summary>
    /// Başlangıç ve bitiş değerleri arasında easing uygular.
    /// elapsed: geçen süre, duration: toplam süre.
    /// </summary>
    public static float Apply(EaseType ease, float from, float to, float elapsed, float duration)
    {
        if (duration <= 0f) return to;
        float t = Mathf.Clamp01(elapsed / duration);
        return Mathf.LerpUnclamped(from, to, Evaluate(ease, t));
    }

    /// <summary>
    /// Vector3 için easing uygular.
    /// </summary>
    public static Vector3 Apply(EaseType ease, Vector3 from, Vector3 to, float elapsed, float duration)
    {
        if (duration <= 0f) return to;
        float t = Mathf.Clamp01(elapsed / duration);
        float easedT = Evaluate(ease, t);
        return Vector3.LerpUnclamped(from, to, easedT);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Quad
    // ─────────────────────────────────────────────────────────────────────────
    public static float EaseInQuad(float t)     => t * t;
    public static float EaseOutQuad(float t)    => 1f - (1f - t) * (1f - t);
    public static float EaseInOutQuad(float t)  => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

    // ─────────────────────────────────────────────────────────────────────────
    // Cubic
    // ─────────────────────────────────────────────────────────────────────────
    public static float EaseInCubic(float t)     => t * t * t;
    public static float EaseOutCubic(float t)    => 1f - Mathf.Pow(1f - t, 3f);
    public static float EaseInOutCubic(float t)  => t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

    // ─────────────────────────────────────────────────────────────────────────
    // Quart
    // ─────────────────────────────────────────────────────────────────────────
    public static float EaseInQuart(float t)     => t * t * t * t;
    public static float EaseOutQuart(float t)    => 1f - Mathf.Pow(1f - t, 4f);
    public static float EaseInOutQuart(float t)  => t < 0.5f ? 8f * t * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 4f) / 2f;

    // ─────────────────────────────────────────────────────────────────────────
    // Sine
    // ─────────────────────────────────────────────────────────────────────────
    public static float EaseInSine(float t)     => 1f - Mathf.Cos(t * Mathf.PI / 2f);
    public static float EaseOutSine(float t)    => Mathf.Sin(t * Mathf.PI / 2f);
    public static float EaseInOutSine(float t)  => -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;

    // ─────────────────────────────────────────────────────────────────────────
    // Expo
    // ─────────────────────────────────────────────────────────────────────────
    public static float EaseInExpo(float t)     => t == 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
    public static float EaseOutExpo(float t)    => t == 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    public static float EaseInOutExpo(float t)
    {
        if (t == 0f) return 0f;
        if (t == 1f) return 1f;
        return t < 0.5f
            ? Mathf.Pow(2f, 20f * t - 10f) / 2f
            : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Back (overshoot)
    // ─────────────────────────────────────────────────────────────────────────
    private const float BackC1 = 1.70158f;
    private const float BackC2 = BackC1 * 1.525f;
    private const float BackC3 = BackC1 + 1f;

    public static float EaseInBack(float t)    => BackC3 * t * t * t - BackC1 * t * t;
    public static float EaseOutBack(float t)   => 1f + BackC3 * Mathf.Pow(t - 1f, 3f) + BackC1 * Mathf.Pow(t - 1f, 2f);
    public static float EaseInOutBack(float t)
    {
        return t < 0.5f
            ? Mathf.Pow(2f * t, 2f) * ((BackC2 + 1f) * 2f * t - BackC2) / 2f
            : (Mathf.Pow(2f * t - 2f, 2f) * ((BackC2 + 1f) * (t * 2f - 2f) + BackC2) + 2f) / 2f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Elastic
    // ─────────────────────────────────────────────────────────────────────────
    private const float ElasticC4 = (2f * Mathf.PI) / 3f;
    private const float ElasticC5 = (2f * Mathf.PI) / 4.5f;

    public static float EaseInElastic(float t)
    {
        if (t == 0f) return 0f;
        if (t == 1f) return 1f;
        return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((t * 10f - 10.75f) * ElasticC4);
    }

    public static float EaseOutElastic(float t)
    {
        if (t == 0f) return 0f;
        if (t == 1f) return 1f;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * ElasticC4) + 1f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bounce
    // ─────────────────────────────────────────────────────────────────────────
    public static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)        return n1 * t * t;
        if (t < 2f / d1)        return n1 * (t -= 1.5f   / d1) * t + 0.75f;
        if (t < 2.5f / d1)      return n1 * (t -= 2.25f  / d1) * t + 0.9375f;
        return                         n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }

    public static float EaseInBounce(float t) => 1f - EaseOutBounce(1f - t);
}
