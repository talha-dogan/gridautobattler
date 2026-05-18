using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.Pool;

/// <summary>
/// Floating damage number that animates upward and fades out.
/// Instead of calling Destroy(), it returns itself to the DamageTextManager's pool
/// once the animation is complete.
/// </summary>
public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro _textMesh;

    // Pool reference injected by DamageTextManager after the instance is created.
    private IObjectPool<DamageText> _pool;

    // Coroutine handle so we can stop it if the object is returned early.
    private Coroutine _animationCoroutine;

    // -------------------------------------------------------------------------
    // Pool binding
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called once by DamageTextManager's createFunc to bind this instance to its pool.
    /// </summary>
    public void SetPool(IObjectPool<DamageText> pool)
    {
        _pool = pool;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resets visual state and starts the float-and-fade animation.
    /// Called by DamageTextManager every time this object is retrieved from the pool.
    /// </summary>
    public void Initialize(float damageAmount, DamageTextDataSO data)
    {
        // Stop any leftover animation from a previous use.
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }

        // Reset text and colour to a fully opaque state.
        _textMesh.text = $"-{damageAmount} HP";
        _textMesh.color = data.textColor;

        _animationCoroutine = StartCoroutine(AnimateText(data));
    }

    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    private IEnumerator AnimateText(DamageTextDataSO data)
    {
        float elapsedTime = 0f;
        Color startColor = _textMesh.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f); // Fully transparent

        while (elapsedTime < data.fadeDuration)
        {
            // Float upward.
            transform.position += Vector3.up * data.floatSpeed * Time.deltaTime;

            // Fade out gradually.
            _textMesh.color = Color.Lerp(startColor, endColor, elapsedTime / data.fadeDuration);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        _animationCoroutine = null;

        // Animation complete — return to pool instead of destroying.
        ReturnToPool();
    }

    // -------------------------------------------------------------------------
    // Pool helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns this instance to its pool. Falls back to Destroy if no pool is set.
    /// </summary>
    private void ReturnToPool()
    {
        if (_pool != null)
        {
            _pool.Release(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Resets all visual and runtime state so the object is clean for the next use.
    /// Called by DamageTextManager's ActionOnRelease.
    /// </summary>
    public void ResetState()
    {
        // Stop any in-progress animation to avoid ghost coroutines.
        if (_animationCoroutine != null)
        {
            StopCoroutine(_animationCoroutine);
            _animationCoroutine = null;
        }

        // Ensure the text mesh is fully transparent while sitting in the pool.
        if (_textMesh != null)
        {
            Color c = _textMesh.color;
            _textMesh.color = new Color(c.r, c.g, c.b, 0f);
        }
    }
}
