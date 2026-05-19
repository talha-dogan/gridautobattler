using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections.Generic;

/// <summary>
/// Attach this to each of the 8 character panels in the Upgrade Scene.
/// Handles the drop event, validates the incoming equipment, performs the
/// equip/swap logic against PlayerArmyDataSO, and refreshes the slot visuals.
///
/// Single Responsibility: drop validation + data mutation + visual refresh.
/// Inventory add/remove is delegated to UpgradeManager (SRP / DIP).
/// </summary>
[RequireComponent(typeof(Image))]
public class UpgradeCharacterDropZoneUI : MonoBehaviour, IDropHandler
{
    // -------------------------------------------------------------------------
    // Inspector references
    // -------------------------------------------------------------------------

    [Header("Ordu Verisi")]
    [Tooltip("Tüm 8 slotu içeren ordu veri varlığı.")]
    [SerializeField] private PlayerArmyDataSO _armyData;

    [Tooltip("Bu drop zone'un temsil ettiği slot indeksi (0-7).")]
    [SerializeField, Range(0, 7)] private int _armySlotIndex;

    [Header("Karakter Görseli (ShowcasePawn)")]
    [Tooltip("Bu slota bağlı ShowcasePawn'un CharacterEquipmentVisuals bileşeni. " +
             "Eşya düştüğünde SpriteRenderer katmanlarını async günceller.")]
    [SerializeField] private CharacterEquipmentVisuals _characterVisuals;

    [Header("Ekipman Görsel Slotları (Opsiyonel UI Image'ları)")]
    [Tooltip("Her ekipman slotu için Image bileşenlerini tutan sözlük. Inspector'dan doldurulur.")]
    [SerializeField] private List<EquipmentSlotImageEntry> _equipmentSlotImages = new List<EquipmentSlotImageEntry>();

    // -------------------------------------------------------------------------
    // Nested serializable type for Inspector slot-image mapping
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps an EquipmentSlot enum value to a UI Image component.
    /// Serializable so it can be configured in the Inspector without code changes.
    /// </summary>
    [System.Serializable]
    public class EquipmentSlotImageEntry
    {
        public EquipmentSlot slot;
        [Tooltip("Bu ekipman slotunun ikonunu gösteren Image bileşeni.")]
        public Image slotImage;
    }

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // Tracks one active Addressable handle per slot to prevent handle leaks.
    private readonly Dictionary<EquipmentSlot, AsyncOperationHandle<Sprite>> _activeHandles
        = new Dictionary<EquipmentSlot, AsyncOperationHandle<Sprite>>();

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// The army slot index this drop zone represents (0-7).
    /// </summary>
    public int ArmySlotIndex => _armySlotIndex;

    /// <summary>
    /// Forces a full visual refresh from the current ArmySlot data.
    /// Call this after loading a saved state or initialising the scene.
    /// </summary>
    public void RefreshAllVisuals()
    {
        if (_armyData == null) return;

        PlayerArmyDataSO.ArmySlot slot = _armyData.GetSlot(_armySlotIndex);
        if (slot == null) return;

        foreach (EquipmentSlot equipSlot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentDataSO equipped = slot.GetEquipment(equipSlot);

            // Push to the ShowcasePawn SpriteRenderer layers.
            if (_characterVisuals != null && equipped != null)
                _characterVisuals.EquipItem(equipped);

            // Also update optional UI Image slots.
            UpdateSlotVisual(equipSlot, equipped);
        }
    }

    // -------------------------------------------------------------------------
    // IDropHandler
    // -------------------------------------------------------------------------

    public void OnDrop(PointerEventData eventData)
    {
        // Retrieve the drag source from the pointer event.
        if (eventData.pointerDrag == null) return;

        UpgradeDragItemUI dragItem = eventData.pointerDrag.GetComponent<UpgradeDragItemUI>();
        if (dragItem == null) return;

        EquipmentDataSO incomingEquipment = dragItem.EquipmentData;
        if (incomingEquipment == null) return;

        // Validate that the army data and slot are properly configured.
        if (_armyData == null)
        {
            Debug.LogError($"[UpgradeCharacterDropZoneUI] Slot {_armySlotIndex}: PlayerArmyDataSO atanmamış!");
            return;
        }

        PlayerArmyDataSO.ArmySlot armySlot = _armyData.GetSlot(_armySlotIndex);
        if (armySlot == null)
        {
            Debug.LogWarning($"[UpgradeCharacterDropZoneUI] Slot {_armySlotIndex} geçersiz veya ordu listesi yeterince büyük değil.");
            return;
        }

        EquipmentSlot targetSlot = incomingEquipment.slot;

        // ── Swap logic ────────────────────────────────────────────────────────
        // If the character already has an item in this slot, return it to inventory.
        EquipmentDataSO existingEquipment = armySlot.GetEquipment(targetSlot);
        if (existingEquipment != null)
        {
            // Return the displaced item to the player's inventory via UpgradeManager.
            UpgradeManager upgradeManager = UpgradeManager.Instance;
            if (upgradeManager != null)
                upgradeManager.AddToInventory(existingEquipment);
            else
                Debug.LogWarning("[UpgradeCharacterDropZoneUI] UpgradeManager.Instance bulunamadı; eski eşya envantere iade edilemedi.");
        }

        // Remove the incoming item from the inventory (it is now equipped).
        UpgradeManager manager = UpgradeManager.Instance;
        if (manager != null)
            manager.RemoveFromInventory(incomingEquipment);

        // Write the new equipment into the army data.
        armySlot.SetEquipment(targetSlot, incomingEquipment);

        // Update the ShowcasePawn SpriteRenderer layers via CharacterEquipmentVisuals.
        if (_characterVisuals != null)
            _characterVisuals.EquipItem(incomingEquipment);

        // Also update any optional UI Image slots (if configured).
        UpdateSlotVisual(targetSlot, incomingEquipment);

        // Broadcast the equipment change so other systems can react.
        GameEvents.EquipmentChanged(_armySlotIndex, incomingEquipment);

        Debug.Log($"[UpgradeCharacterDropZoneUI] Slot {_armySlotIndex}: '{incomingEquipment.equipmentName}' ({targetSlot}) takıldı.");
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnDestroy()
    {
        // Release all tracked Addressable handles to prevent memory leaks.
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
    /// Asynchronously loads the sprite for the given equipment and assigns it
    /// to the corresponding slot Image. Releases any previously loaded handle
    /// for that slot before starting the new load.
    /// Passing null clears the slot image immediately.
    /// </summary>
    private void UpdateSlotVisual(EquipmentSlot slot, EquipmentDataSO equipment)
    {
        // Release the previous handle for this slot.
        if (_activeHandles.TryGetValue(slot, out AsyncOperationHandle<Sprite> existing))
        {
            if (existing.IsValid())
                Addressables.Release(existing);
            _activeHandles.Remove(slot);
        }

        Image targetImage = GetSlotImage(slot);

        // If no equipment, clear the image and bail.
        if (equipment == null)
        {
            if (targetImage != null) targetImage.sprite = null;
            return;
        }

        // Guard: no valid Addressable key — clear and bail.
        if (!equipment.spriteReference.RuntimeKeyIsValid())
        {
            if (targetImage != null) targetImage.sprite = null;
            return;
        }

        // Start the async load and track the handle.
        AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>(equipment.spriteReference);
        _activeHandles[slot] = handle;

        EquipmentSlot capturedSlot = slot;

        handle.Completed += (op) =>
        {
            if (this == null) return;

            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Image img = GetSlotImage(capturedSlot);
                if (img != null) img.sprite = op.Result;
            }
            else
            {
                Debug.LogWarning($"[UpgradeCharacterDropZoneUI] Slot {_armySlotIndex}/{capturedSlot} sprite yüklenemedi. Durum: {op.Status}");
            }
        };
    }

    /// <summary>
    /// Looks up the Image component mapped to the given EquipmentSlot.
    /// Returns null if no mapping exists for that slot.
    /// </summary>
    private Image GetSlotImage(EquipmentSlot slot)
    {
        foreach (EquipmentSlotImageEntry entry in _equipmentSlotImages)
        {
            if (entry.slot == slot)
                return entry.slotImage;
        }
        return null;
    }
}
