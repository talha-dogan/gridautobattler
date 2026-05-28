using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Handles the display and fade-out animation of localized equipment notifications.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI), typeof(CanvasGroup))]
public class EquipmentPopupUI : MonoBehaviour
{
    private TextMeshProUGUI _textComponent;
    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        _textComponent = GetComponent<TextMeshProUGUI>();
        _canvasGroup = GetComponent<CanvasGroup>();
        
        // Ensure it's invisible at start
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Displays the given text, waits for the duration, then fades out.
    /// </summary>
    public void ShowPopup(string message, float displayTime = 2f)
    {
        // Stop any running fade to prevent overlapping animations
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        
        _textComponent.text = message;
        _fadeCoroutine = StartCoroutine(DisplayAndFadeRoutine(displayTime));
    }

    private IEnumerator DisplayAndFadeRoutine(float displayTime)
    {
        // Instantly show the text
        _canvasGroup.alpha = 1f;

        // Wait for the specified time
        yield return new WaitForSeconds(displayTime);

        // Fade out smoothly over 0.5 seconds
        float fadeDuration = 0.5f;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}