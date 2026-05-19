using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Manages the five equipment sprite layers on a single unit.
/// Each slot maps to a dedicated SpriteRenderer that is set via the Inspector.
///
/// Loading is asynchronous (Addressables.LoadAssetAsync) so the main thread
/// is never blocked. Every loaded handle is tracked and released in OnDestroy
/// to prevent memory leaks when the unit is returned to the pool or destroyed.
/// </summary>
public class CharacterEquipmentVisuals : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector references — one SpriteRenderer per equipment slot
    // -------------------------------------------------------------------------

    [Header("Equipment Sprite Renderers")]
    [SerializeField] private SpriteRenderer helmetRenderer;
    [SerializeField] private SpriteRenderer vestRenderer;
    [SerializeField] private SpriteRenderer pantsRenderer;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private SpriteRenderer shieldRenderer;

    // -------------------------------------------------------------------------
    // Internal state
    // -------------------------------------------------------------------------

    // Tracks one active Addressable handle per slot so we can release the
    // previous sprite before loading a new one, avoiding handle leaks.
    private readonly Dictionary<EquipmentSlot, AsyncOperationHandle<Sprite>> _activeHandles
        = new Dictionary<EquipmentSlot, AsyncOperationHandle<Sprite>>();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Equips a piece of gear onto this unit.
    /// Releases any previously loaded sprite for the same slot, then starts an
    /// asynchronous Addressable load. The SpriteRenderer is updated once the
    /// load completes; the unit remains fully functional during the load.
    /// Passing null clears the slot immediately without starting a load.
    /// </summary>
    public void EquipItem(EquipmentDataSO equipment)
    {
        if (equipment == null) return;

        EquipmentSlot slot = equipment.slot;

        // Release the old handle for this slot before starting a new load.
        ReleaseSlot(slot);

        // Guard: if the asset reference is not set, clear the renderer and bail.
        if (!equipment.spriteReference.RuntimeKeyIsValid())
        {
            SetRendererSprite(slot, null);
            return;
        }

        // Start the async load and cache the handle immediately so it can be
        // released even if the component is destroyed before the load finishes.
        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(equipment.spriteReference);
        _activeHandles[slot] = handle;

        // Capture slot in a local variable for the lambda closure.
        EquipmentSlot capturedSlot = slot;

        handle.Completed += (op) =>
        {
            // Guard: the component may have been destroyed while the load was in flight.
            if (this == null) return;

            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                SetRendererSprite(capturedSlot, op.Result);
            }
            else
            {
                Debug.LogWarning($"[CharacterEquipmentVisuals] Failed to load sprite for slot {capturedSlot} " +
                                 $"on '{gameObject.name}'. Status: {op.Status}");
            }
        };
    }

    /// <summary>
    /// Removes the visual for a specific slot and releases its Addressable handle.
    /// Call this when the player unequips an item without replacing it.
    /// </summary>
    public void UnequipSlot(EquipmentSlot slot)
    {
        ReleaseSlot(slot);
        SetRendererSprite(slot, null);
    }

    /// <summary>
    /// Clears all five slots and releases every loaded Addressable handle.
    /// Useful when resetting a unit before returning it to the pool.
    /// </summary>
    public void ClearAllSlots()
    {
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            ReleaseSlot(slot);
            SetRendererSprite(slot, null);
        }
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnDestroy()
    {
        // Release every tracked handle to prevent Addressables memory leaks.
        // This covers both the case where the unit is destroyed and where it is
        // returned to an object pool (pool should call ClearAllSlots first, but
        // OnDestroy acts as a final safety net).
        foreach (var kvp in _activeHandles)
        {
            if (kvp.Value.IsValid())
                Addressables.Release(kvp.Value);
        }

        _activeHandles.Clear();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Releases the Addressable handle for a slot if one exists, and removes it
    /// from the tracking dictionary. Safe to call even if no handle is cached.
    /// </summary>
    private void ReleaseSlot(EquipmentSlot slot)
    {
        if (_activeHandles.TryGetValue(slot, out AsyncOperationHandle<Sprite> existingHandle))
        {
            if (existingHandle.IsValid())
                Addressables.Release(existingHandle);

            _activeHandles.Remove(slot);
        }
    }

    /// <summary>
    /// Routes a sprite assignment to the correct SpriteRenderer for the given slot.
    /// Passing null clears the renderer (hides the layer).
    /// </summary>
    private void SetRendererSprite(EquipmentSlot slot, Sprite sprite)
    {
        SpriteRenderer target = GetRenderer(slot);
        if (target != null)
            target.sprite = sprite;
    }

    /// <summary>
    /// Returns the SpriteRenderer that corresponds to the given equipment slot.
    /// </summary>
    private SpriteRenderer GetRenderer(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Helmet => helmetRenderer,
            EquipmentSlot.Vest   => vestRenderer,
            EquipmentSlot.Pants  => pantsRenderer,
            EquipmentSlot.Weapon => weaponRenderer,
            EquipmentSlot.Shield => shieldRenderer,
            _                    => null
        };
    }
}
