using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Attach this to every inventory item slot in the Upgrade Scene.
/// Handles the full drag lifecycle: spawning a ghost icon that follows the cursor,
/// carrying the EquipmentDataSO payload, and cleaning up after the drop.
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
    [SerializeField] private EquipmentDataSO _equipmentData;

    [Header("Görsel Ayarlar")]
    [SerializeField] private Image _itemIcon;
    [SerializeField] private Canvas _rootCanvas;

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    private GameObject _dragGhost;
    private RectTransform _ghostRect;
    private CanvasGroup _ghostCanvasGroup;

    // Addressable handle for the slot icon; released in OnDestroy.
    private AsyncOperationHandle<Sprite> _iconSpriteHandle;
    private bool _iconHandleActive = false;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    public EquipmentDataSO EquipmentData => _equipmentData;

    /// <summary>
    /// Sahne üzerindeki bu item nesnesini yok eder.
    /// Bir karaktere başarıyla takıldığında UpgradeCharacterDropZoneUI tarafından çağrılır.
    /// </summary>
    public void ConsumeItem()
    {
        // Ghost hâlâ varsa temizle.
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
        }

        Destroy(gameObject);
    }

    public void Initialize(EquipmentDataSO data, Canvas rootCanvas)
    {
        _equipmentData = data;
        _rootCanvas = rootCanvas;

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
        _dragGhost.transform.SetAsLastSibling();

        _ghostRect = _dragGhost.GetComponent<RectTransform>();
        _ghostRect.sizeDelta = GetComponent<RectTransform>().sizeDelta;

        _ghostCanvasGroup = _dragGhost.GetComponent<CanvasGroup>();
        _ghostCanvasGroup.blocksRaycasts = false;
        _ghostCanvasGroup.alpha = 0.80f;

        // FIX: Directly copy the already loaded sprite instead of loading it again asynchronously.
        Image ghostImage = _dragGhost.GetComponent<Image>();
        ghostImage.sprite = _itemIcon.sprite;

        UpdateGhostPosition(eventData, canvas);
    }

    // -------------------------------------------------------------------------
    // IDragHandler & IEndDragHandler
    // -------------------------------------------------------------------------

    public void OnDrag(PointerEventData eventData)
    {
        if (_dragGhost == null) return;
        UpdateGhostPosition(eventData, ResolveCanvas());
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_dragGhost != null)
        {
            Destroy(_dragGhost);
            _dragGhost = null;
            _ghostRect = null;
            _ghostCanvasGroup = null;
        }
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle & Private helpers
    // -------------------------------------------------------------------------

    private void OnDestroy()
    {
        if (_iconHandleActive && _iconSpriteHandle.IsValid())
            Addressables.Release(_iconSpriteHandle);
    }

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

    private void RefreshIcon()
    {
        if (_itemIcon == null || _equipmentData == null) return;
        if (!_equipmentData.spriteReference.RuntimeKeyIsValid()) return;

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

    private Canvas ResolveCanvas()
    {
        if (_rootCanvas != null) return _rootCanvas;

        Canvas found = GetComponentInParent<Canvas>();
        if (found == null)
            Debug.LogWarning("[UpgradeDragItemUI] Kök Canvas ataması yapılmamış ve parent'ta da bulunamadı.");

        return found;
    }
}