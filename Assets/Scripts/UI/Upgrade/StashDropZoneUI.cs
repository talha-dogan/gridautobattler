using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

public class StashDropZoneUI : MonoBehaviour, IDropHandler
{
    // Holds the reference to the item currently stored in this slot
    private UpgradeDragItemUI _storedItem;

    public void OnDrop(PointerEventData eventData)
    {
        // Ensure there is a dragged object
        if (eventData.pointerDrag == null) return;

        UpgradeDragItemUI dragItem = eventData.pointerDrag.GetComponent<UpgradeDragItemUI>();
        if (dragItem == null) return;

        // Prevent dropping if the slot is already full
        if (_storedItem != null && _storedItem != dragItem)
        {
            Debug.Log("[StashDropZoneUI] This slot is already full.");
            return;
        }

        // Snap the dragged UI item to this slot
        dragItem.transform.SetParent(transform);
        dragItem.transform.localPosition = Vector3.zero;

        // Store the reference
        _storedItem = dragItem;

        // Inform the UpgradeManager that the item is now in the stash/inventory
        if (UpgradeManager.Instance != null && dragItem.EquipmentData != null)
        {
            // If it's not already in the inventory list, add it
            if (!UpgradeManager.Instance.GetInventory().Contains(dragItem.EquipmentData))
            {
                UpgradeManager.Instance.AddToInventory(dragItem.EquipmentData);
            }
        }
    }
}
