using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Listens to GameEvents.OnLevelIndexChanged and swaps the SpriteRenderer sprite
/// on the BackGraund object whenever the level changes.
///
/// Inspector controls:
///   • changeBGOnLevelChange  – toggle to enable / disable the feature entirely.
///   • backgrounds            – ordered list of sprites; index 0 = level 1, index 1 = level 2, …
///                              If the level index exceeds the list, the last sprite is reused.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundChanger : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector fields
    // -------------------------------------------------------------------------

    [Header("Background Change Settings")]
    [Tooltip("Enable or disable automatic background changes on level transitions.")]
    public bool changeBGOnLevelChange = true;

    [Tooltip("Ordered list of background sprites. Index 0 is used for Level 1, index 1 for Level 2, etc. " +
             "If the current level exceeds the list size, the last sprite in the list is reused.")]
    public List<Sprite> backgrounds = new List<Sprite>();

    // -------------------------------------------------------------------------
    // Private references
    // -------------------------------------------------------------------------

    private SpriteRenderer _spriteRenderer;

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        GameEvents.OnLevelIndexChanged += HandleLevelIndexChanged;
    }

    private void OnDestroy()
    {
        GameEvents.OnLevelIndexChanged -= HandleLevelIndexChanged;
    }

    // -------------------------------------------------------------------------
    // Event handler
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by GameEvents whenever the level index changes.
    /// <paramref name="displayIndex"/> is 1-based (Level 1 = 1, Level 2 = 2, …).
    /// </summary>
    private void HandleLevelIndexChanged(int displayIndex)
    {
        if (!changeBGOnLevelChange) return;
        if (backgrounds == null || backgrounds.Count == 0) return;

        // Convert 1-based display index to 0-based list index, clamped to list bounds.
        int listIndex = Mathf.Clamp(displayIndex - 1, 0, backgrounds.Count - 1);

        Sprite target = backgrounds[listIndex];
        if (target == null) return;

        _spriteRenderer.sprite = target;
    }
}
