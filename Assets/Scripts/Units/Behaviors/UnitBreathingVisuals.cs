using UnityEngine;

public class UnitBreathingVisuals : MonoBehaviour
{
    private Vector3 _originalScale;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;

    // Player (Mathf) için sürekli artan faz
    private float _mathPhase = 0f;
    // Enemy (Curve) için 0-1 arası döngüsel faz
    private float _curvePhase = 0f;

    [Header("Animation Mode")]
    public bool isEnemy = false;

    [Header("Curves (Enemy only)")]
    public AnimationCurve breathingCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, -2f, 0f)
    );
    public AnimationCurve walkSwayCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.25f, 1f, 0f, 0f),
        new Keyframe(0.5f, 0f, 0f, -4f),
        new Keyframe(0.75f, -1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, 4f)
    );
    public AnimationCurve victoryBounceCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 4f),
        new Keyframe(0.5f, 1f, 0f, 0f),
        new Keyframe(1f, 0f, 0f, -4f)
    );

    private void Awake()
    {
        _originalScale = transform.localScale;
        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;
    }

    public void UpdateBreathing(float speed, float amplitude)
    {
        if (isEnemy)
        {
            AdvanceCurvePhase(speed);
            float scaleOffset = breathingCurve.Evaluate(_curvePhase) * amplitude;
            transform.localScale = new Vector3(_originalScale.x - (scaleOffset * 0.5f), _originalScale.y + scaleOffset, _originalScale.z);
        }
        else
        {
            // --- Player original math logic ---
            _mathPhase += Time.deltaTime * speed;
            float scaleOffset = Mathf.Sin(_mathPhase) * amplitude;

            float newScaleX = _originalScale.x - (scaleOffset * 0.5f);
            float newScaleY = _originalScale.y + scaleOffset;

            transform.localScale = new Vector3(newScaleX, newScaleY, _originalScale.z);
        }
    }

    public void UpdateWalkingSway(float speed, float maxAngle, float squashAmount = 0.1f)
    {
        if (isEnemy)
        {
            AdvanceCurvePhase(speed);
            float swayValue = walkSwayCurve.Evaluate(_curvePhase);
            float rotationOffset = swayValue * maxAngle;
            
            // Curve tabanlı basit squash
            float squashFactor = Mathf.Abs(swayValue) * squashAmount;
            ApplyTransform(rotationOffset, squashFactor, Mathf.Abs(swayValue) * (squashAmount * 0.2f));
        }
        else
        {
            // --- Player original math logic ---
            _mathPhase += Time.deltaTime * speed;

            float sinWave = Mathf.Sin(_mathPhase);
            float rotationOffset = sinWave * maxAngle;

            // Sin^2 logic for organic feel
            float squashFactor = (sinWave * sinWave) * squashAmount;
            float subtleBob = Mathf.Abs(sinWave) * (squashAmount * 0.2f);

            ApplyTransform(rotationOffset, squashFactor, subtleBob);
        }
    }

    public void UpdateVictoryBounce(float speed, float height)
    {
        if (isEnemy)
        {
            AdvanceCurvePhase(speed);
            float bounceOffset = victoryBounceCurve.Evaluate(_curvePhase) * height;
            transform.localPosition = new Vector3(_originalLocalPosition.x, _originalLocalPosition.y + bounceOffset, _originalLocalPosition.z);
            transform.localScale = _originalScale;
        }
        else
        {
            // --- Player original math logic ---
            _mathPhase += Time.deltaTime * speed;
            float bounceOffset = Mathf.Abs(Mathf.Sin(_mathPhase)) * height;
            
            transform.localPosition = new Vector3(_originalLocalPosition.x, _originalLocalPosition.y + bounceOffset, _originalLocalPosition.z);
            transform.localScale = _originalScale;
        }
    }

    public void ResetVisuals()
    {
        transform.localScale = _originalScale;
        transform.localPosition = _originalLocalPosition;
        transform.localRotation = _originalLocalRotation;
        _mathPhase = 0f;
        _curvePhase = 0f;
    }

    // Helper to keep logic clean for common transform settings
    private void ApplyTransform(float rotationOffset, float squashFactor, float bobOffset)
    {
        float newScaleX = _originalScale.x + (squashFactor * 1.2f);
        float newScaleY = _originalScale.y - squashFactor;

        transform.localRotation = _originalLocalRotation * Quaternion.Euler(0f, 0f, rotationOffset);
        transform.localScale = new Vector3(newScaleX, newScaleY, _originalScale.z);
        transform.localPosition = new Vector3(_originalLocalPosition.x, _originalLocalPosition.y - bobOffset, _originalLocalPosition.z);
    }

    private void AdvanceCurvePhase(float speed)
    {
        _curvePhase += Time.deltaTime * speed;
        _curvePhase %= 1f; // Clamp to 0-1 for animation curves
    }
}