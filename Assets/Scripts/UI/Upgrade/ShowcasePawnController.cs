using UnityEngine;

/// <summary>
/// Drives the idle breathing animation on a ShowcasePawn in the Upgrade Scene.
/// This is a lightweight controller — it has no combat logic, no Rigidbody,
/// and no FSM. It simply ticks UnitBreathingVisuals every frame so the
/// character looks alive on the army selection screen.
///
/// Attach this to the root of a ShowcasePawn prefab instance.
/// </summary>
public class ShowcasePawnController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector references
    // -------------------------------------------------------------------------

    [Header("Nefes Animasyonu")]
    [Tooltip("Body_Renderer child objesindeki UnitBreathingVisuals bileşeni.")]
    [SerializeField] private UnitBreathingVisuals _breathingVisuals;

    [Tooltip("Nefes animasyonunun hızı.")]
    [SerializeField] private float _breathingSpeed = 3f;

    [Tooltip("Nefes animasyonunun genliği (ne kadar şişip söneceği).")]
    [SerializeField] private float _breathingAmplitude = 0.04f;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Reset()
    {
        // Auto-find the UnitBreathingVisuals on the Body_Renderer child when
        // the component is first added, saving manual Inspector wiring.
        _breathingVisuals = GetComponentInChildren<UnitBreathingVisuals>();
    }

    private void Update()
    {
        if (_breathingVisuals == null) return;

        // Tick the idle breathing animation every frame.
        _breathingVisuals.UpdateBreathing(_breathingSpeed, _breathingAmplitude);
    }
}
