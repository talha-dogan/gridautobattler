using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Attach this to every inventory item slot in the Upgrade Scene.
/// Handles the full drag lifecycle: spawning a ghost icon that follows the cursor,
/// carrying the EquipmentDataSO payload, and cleaning up after the drop.
///
/// Single Responsibility: visual drag transport only.
/// Data mutation is handled exclusively by UpgradeCharacterDropZoneUI on drop.
/// </summary>
[RequireComponent(typeof(Image))]
public class UpgradeDragItemUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    // -------------------------------------------------------------------------
    // Inspector references
    // -------------------------------------------------------------------------

    [Header("Ekipman Verisi")]
    [Tooltip("Bu envanter slotunun temsil ettiği ekipman.")]
    [SerializeField] private EquipmentDataSO _equipmentData;

    [Header("Görsel Ayarlar")]
    [Tooltip("Eşya ikonunu gösteren Image bileşeni (bu GameObject üzerinde).")]
    [SerializeField] private Image _itemIcon;

    [Tooltip("Sürükleme hayaletinin üstte render edilmesi için kök Canvas.")]
    [SerializeField] private Canvas _rootCanvas;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // The temporary ghost image that follows the cursor during a drag.
    private GameObject _dragGhost;

    // Cached RectTransform of the ghost for per-frame position updates.
    private RectTransform _ghostRect;

    // CanvasGroup on the ghost — blocksRaycasts disabled so drop zones receive events.
    private CanvasGroup _ghostCanvasGroup;

    // Addressable handle for the ghost sprite; released when drag ends.
    private AsyncOperationHandle<Sprite> _ghostSpriteHandle;
    private bool _ghostHandleActive = false;

    // Addressable handle for the slot icon; released in OnDestroy.
    private AsyncOperationHandle<Sprite> _iconSpriteHandle;
    private bool _iconHandleActive = false;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// The equipment data this drag item carries.
    /// Read by UpgradeCharacterDropZoneUI.OnDrop to identify the item.
    /// </summary>
    public EquipmentDataSO EquipmentData => _equipmentData;

    /// <summary>
    /// Populates this slot with the given equipment and refreshes the icon sprite.
    /// Call this when building the inventory list at runtime.
    /// </summary>
    public void Initialize(EquipmentDataSO data, Canvas rootCanvas)
    {
        _equipmentData = data;
        _rootCanvas    = rootCanvas;

        // Auto-grab the Image on this GameObject if not assigned in Inspector.
        if (_itemIcon == null)
            _itemIcon = GetComponent<Image>();

        RefreshIcon();
    }

    // -------------------------------------------------------------------------
    // IBeginDragHandler
    // -------------------------------------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_equipmentData == null) return;

        // Resolve the canvas to parent the ghost under.
        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[UpgradeDragItemUI] Kök Canvas bulunamadı, sürükleme iptal edildi.");
            return;
        }

        // Build the ghost GameObject with the required UI components.
        _dragGhost = new GameObject(
            "SürüklemeHayaleti",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup));

        _dragGhost.transform.SetParent(canvas.transform, false);

        // Render the ghost above all other UI elements.
        _dragGhost.transform.SetAsLastSibling();

        // Match the ghost size to this slot's RectTransform.
        _ghostRect = _dragGhost.GetComponent<RectTransform>();
        _ghostRect.sizeDelta = GetComponent<RectTransform>().sizeDelta;

        // Disable raycasting on the ghost so pointer events reach the drop zones.
        _ghostCanvasGroup = _dragGhost.GetComponent<CanvasGroup>();
        _ghostCanvasGroup.blocksRaycasts = false;
        _ghostCanvasGroup.alpha = 0.80f;

        // Load the ghost sprite asynchronously from Addressables.
        LoadGhostSprite(_dragGhost.GetComponent<Image>());

        // Snap the ghost to the current pointer position immediately.
        UpdateGhostPosition(eventData, canvas);
    }

    // -------------------------------------------------------------------------
    // IDragHandler
    // -------------------------------------------------------------------------

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhost == null) return;
        UpdateGhostPosition(eventData, ResolveCanvas());
    }

    // -------------------------------------------------------------------------
    // IEndDragHandler
    // -------------------------------------------------------------------------

    public void OnEndDrag(PointerEventData eventData)
    {
        // Destroy the ghost regardless of whether the drop was accepted.
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost        = null;
            _ghostRect        = null;
            _ghostCanvasGroup = null;
        }

        // Release the Addressable handle used for the ghost sprite.
        ReleaseGhostHandle();
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void OnDestroy()
    {
        // Release the icon sprite handle to prevent Addressables memory leaks.
        if (_iconHandleActive && _iconSpriteHandle.IsValid())
            Addressables.Release(_iconSpriteHandle);

        // Safety net: release ghost handle if OnEndDrag was never called.
        ReleaseGhostHandle();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Moves the ghost to the current pointer position in canvas-local space.
    /// Handles both Overlay and Camera-space canvas render modes correctly.
    /// </summary>
    private void UpdateGhostPosition(PointerEventData eventData, Canvas canvas)
    {
        if (_ghostRect == null || canvas == null) return;

        Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(),
            eventData.position,
            cam,
            out Vector2 localPoint);

        _ghostRect.localPosition = localPoint;
    }

    /// <summary>
    /// Starts an async Addressable load for the ghost icon.
    /// The handle is stored so it can be released when the drag ends.
    /// </summary>
    private void LoadGhostSprite(Image ghostImage)
    {
        if (_equipmentData == null || !_equipmentData.spriteReference.RuntimeKeyIsValid())
            return;

        _ghostHandleActive = false;
        _ghostSpriteHandle = Addressables.LoadAssetAsync<Sprite>(_equipmentData.spriteReference);
        _ghostHandleActive = true;

        _ghostSpriteHandle.Completed += (op) =>
        {
            // Guard: ghost may have been destroyed before the load completed.
            if (ghostImage == null) return;

            if (op.Status == AsyncOperationStatus.Succeeded)
                ghostImage.sprite = op.Result;
            else
                Debug.LogWarning($"[UpgradeDragItemUI] Hayalet sprite yüklenemedi: '{_equipmentData.equipmentName}'. Durum: {op.Status}");
        };
    }

    /// <summary>
    /// Releases the ghost Addressable handle if one is active.
    /// Safe to call multiple times.
    /// </summary>
    private void ReleaseGhostHandle()
    {
        if (_ghostHandleActive && _ghostSpriteHandle.IsValid())
        {
            Addressables.Release(_ghostSpriteHandle);
            _ghostHandleActive = false;
        }
    }

    /// <summary>
    /// Asynchronously loads the equipment sprite and assigns it to the slot icon.
    /// The handle is tracked for release in OnDestroy.
    /// </summary>
    private void RefreshIcon()
    {
        if (_itemIcon == null || _equipmentData == null) return;
        if (!_equipmentData.spriteReference.RuntimeKeyIsValid()) return;

        // Release any previously loaded icon handle before starting a new load.
        if (_iconHandleActive && _iconSpriteHandle.IsValid())
        {
            Addressables.Release(_iconSpriteHandle);
            _iconHandleActive = false;
        }

        _iconSpriteHandle = Addressables.LoadAssetAsync<Sprite>(_equipmentData.spriteReference);
        _iconHandleActive = true;

        _iconSpriteHandle.Completed += (op) =>
        {
            if (this == null || _itemIcon == null) return;

            if (op.Status == AsyncOperationStatus.Succeeded)
                _itemIcon.sprite = op.Result;
            else
                Debug.LogWarning($"[UpgradeDragItemUI] İkon sprite yüklenemedi: '{_equipmentData.equipmentName}'. Durum: {op.Status}");
        };
    }

    /// <summary>
    /// Returns the root Canvas, preferring the serialized field and falling back
    /// to a parent search. Logs a warning if neither is found.
    /// </summary>
    private Canvas ResolveCanvas()
    {
        if (_rootCanvas != null) return _rootCanvas;

        Canvas found = GetComponentInParent<Canvas>();
        if (found == null)
            Debug.LogWarning("[UpgradeDragItemUI] Kök Canvas ataması yapılmamış ve parent'ta da bulunamadı.");

        return found;
    }
}
