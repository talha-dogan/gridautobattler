using UnityEngine;

public class UnitBreathingVisuals : MonoBehaviour
{
    private Vector3 _originalScale;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private float _currentPhase = 0f;

    private void Awake()
    {
        _originalScale = transform.localScale;
        _originalLocalPosition = transform.localPosition;
        _originalLocalRotation = transform.localRotation;
    }

    public void UpdateBreathing(float speed, float amplitude)
    {
        _currentPhase += Time.deltaTime * speed;
        float scaleOffset = Mathf.Sin(_currentPhase) * amplitude;

        float newScaleX = _originalScale.x - (scaleOffset * 0.5f);
        float newScaleY = _originalScale.y + scaleOffset;

        transform.localScale = new Vector3(newScaleX, newScaleY, _originalScale.z);
    }

    // --- YENİ VE GELİŞTİRİLMİŞ YÜRÜME MANTIĞI ---
    public void UpdateWalkingSway(float speed, float maxAngle, float squashAmount = 0.1f)
    {
        _currentPhase += Time.deltaTime * speed;

        // 1. Sağa-Sola Sallanma (Rotation)
        // Sinüs dalgası: -1 (sol) ile +1 (sağ) arası gider gelir.
        float sinWave = Mathf.Sin(_currentPhase);
        float rotationOffset = sinWave * maxAngle;

        // 2. Organik Sıkışma-Bırakma (Squash & Stretch)
        // Sinüsün karesini alıyoruz (sin^2). Neden? 
        // Çünkü sinüs -1 veya 1 olduğunda (yani en sağda veya en solda), karesi her zaman 1 olur.
        // Böylece karakter hem en sağa hem de en sola yattığında "esnemiş" olur.
        float squashFactor = (sinWave * sinWave) * squashAmount;

        float newScaleX = _originalScale.x + (squashFactor * 1.2f); // Yanlara doğru genişleme
        float newScaleY = _originalScale.y - squashFactor;         // Boydan kısalma (yere basma hissi)

        // Uygulama
        transform.localRotation = _originalLocalRotation * Quaternion.Euler(0f, 0f, rotationOffset);
        transform.localScale = new Vector3(newScaleX, newScaleY, _originalScale.z);

        // Opsiyonel: Yere her bastığında çok hafif aşağı inmesi için (Bounce)
        float subtleBob = Mathf.Abs(sinWave) * (squashAmount * 0.2f);
        transform.localPosition = new Vector3(_originalLocalPosition.x, _originalLocalPosition.y - subtleBob, _originalLocalPosition.z);
    }

    public void UpdateVictoryBounce(float speed, float height)
    {
        _currentPhase += Time.deltaTime * speed;
        float bounceOffset = Mathf.Abs(Mathf.Sin(_currentPhase)) * height;
        transform.localPosition = new Vector3(_originalLocalPosition.x, _originalLocalPosition.y + bounceOffset, _originalLocalPosition.z);
        transform.localScale = _originalScale;
    }

    public void ResetVisuals()
    {
        transform.localScale = _originalScale;
        transform.localPosition = _originalLocalPosition;
        transform.localRotation = _originalLocalRotation;
        _currentPhase = 0f;
    }
}