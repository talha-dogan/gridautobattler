using UnityEngine;
using System.Collections;

public class MeleeWeaponVisuals : MonoBehaviour
{
    private Quaternion _originalRotation;
    private bool _isSwinging = false;

    private void Awake()
    {
        _originalRotation = transform.localRotation;
    }

    // Now accepts dynamic parameters for different weapons!
    public void SwingWeapon(float angle, float duration)
    {
        if (!_isSwinging)
        {
            StartCoroutine(SwingCoroutine(angle, duration));
        }
    }

    private IEnumerator SwingCoroutine(float angle, float duration)
    {
        _isSwinging = true;

        Quaternion startRot = transform.localRotation;
        Quaternion backRot = _originalRotation * Quaternion.Euler(0, 0, -angle * 0.3f);

        float elapsedTime = 0f;
        float pullBackDuration = duration * 0.2f;

        while (elapsedTime < pullBackDuration)
        {
            transform.localRotation = Quaternion.Slerp(startRot, backRot, elapsedTime / pullBackDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;
        Quaternion forwardRot = _originalRotation * Quaternion.Euler(0, 0, angle);
        float strikeDuration = duration * 0.5f;

        while (elapsedTime < strikeDuration)
        {
            transform.localRotation = Quaternion.Slerp(backRot, forwardRot, elapsedTime / strikeDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        elapsedTime = 0f;
        float recoveryDuration = duration * 0.3f;

        while (elapsedTime < recoveryDuration)
        {
            transform.localRotation = Quaternion.Slerp(forwardRot, _originalRotation, elapsedTime / recoveryDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.localRotation = _originalRotation;
        _isSwinging = false;
    }
}