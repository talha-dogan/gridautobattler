using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Gamepad sol stick'ini fare imlecine dönüştürür.
///
/// Çalışma mantığı:
///   InputSystem'ın VirtualMouseInput bileşeni, sanal bir Mouse cihazı
///   oluşturur. Bu script o cihazı her frame güncelleyerek stick hareketini
///   ekran koordinatına çevirir. EventSystem bu sanal fareyi gerçek fare
///   gibi işler — dolayısıyla tüm mevcut drag & drop sistemi (UpgradeDragItemUI)
///   hiçbir değişiklik gerektirmeden gamepad ile çalışır.
///
/// Kurulum:
///   1. Bu script + VirtualMouseInput bileşeni aynı GameObject'e ekle.
///   2. VirtualMouseInput.CursorGraphic alanına cursor Image'ı ata.
///   3. VirtualMouseInput.CursorTransform alanına cursor RectTransform'u ata.
///   4. Bu GameObject'i Upgrade sahnesine ve Grid sahnesine koy
///      (ya da DontDestroyOnLoad ile taşı).
/// </summary>
[RequireComponent(typeof(VirtualMouseInput))]
public class GamepadCursor : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Cursor Ayarları")]
    [Tooltip("Stick'in cursor hızına çarpanı. Büyüttükçe cursor daha hızlı hareket eder.")]
    [SerializeField] private float _cursorSpeed = 1000f;

    [Tooltip("Cursor ikonunu gösteren Image. Gamepad bağlıysa görünür olur.")]
    [SerializeField] private Image _cursorImage;

    [Tooltip("Canvas — ekran sınırlarını hesaplamak için gerekli.")]
    [SerializeField] private RectTransform _canvasRect;

    // ── Private ───────────────────────────────────────────────────────────────

    private VirtualMouseInput _virtualMouse;
    private Mouse _currentMouse;
    private Camera _mainCamera;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _virtualMouse = GetComponent<VirtualMouseInput>();
        _mainCamera   = Camera.main;

        // Başlangıçta cursor'ı ekran ortasına yerleştir
        SetCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));

        // Gamepad bağlı değilse cursor'ı gizle
        RefreshCursorVisibility();
    }

    private void Update()
    {
        if (GameInputHandler.Instance == null) return;

        // Gamepad bağlı mı kontrol et
        bool gamepadActive = Gamepad.current != null;
        RefreshCursorVisibility(gamepadActive);

        if (!gamepadActive) return;

        // ── Cursor hareketi ────────────────────────────────────────────────────
        Vector2 stickInput = Gamepad.current.leftStick.ReadValue();

        // Küçük stick hareketlerini filtrele (dead zone)
        if (stickInput.magnitude < 0.1f) stickInput = Vector2.zero;

        Vector2 currentPos = _virtualMouse.virtualMouse?.position.ReadValue()
                             ?? new Vector2(Screen.width / 2f, Screen.height / 2f);

        Vector2 newPos = currentPos + stickInput * (_cursorSpeed * Time.unscaledDeltaTime);

        // Ekran sınırları içinde tut
        newPos.x = Mathf.Clamp(newPos.x, 0f, Screen.width);
        newPos.y = Mathf.Clamp(newPos.y, 0f, Screen.height);

        SetCursorPosition(newPos);

        // ── Sol tık — A butonu (Güney) ─────────────────────────────────────────
        // VirtualMouseInput'a tık durumunu bildir
        bool aPressed  = Gamepad.current.buttonSouth.isPressed;
        bool aReleased = Gamepad.current.buttonSouth.wasReleasedThisFrame;

        if (Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            InputSystem.QueueStateEvent(
                _virtualMouse.virtualMouse,
                new MouseState { buttons = 1 }); // sol tık aşağı
        }

        if (aReleased)
        {
            InputSystem.QueueStateEvent(
                _virtualMouse.virtualMouse,
                new MouseState { buttons = 0 }); // sol tık yukarı
        }
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    private void SetCursorPosition(Vector2 screenPos)
    {
        if (_virtualMouse?.virtualMouse == null) return;

        InputState.Change(_virtualMouse.virtualMouse.position, screenPos);
        InputSystem.QueueDeltaStateEvent(
            _virtualMouse.virtualMouse.position, screenPos);
    }

    private void RefreshCursorVisibility(bool? gamepadConnected = null)
    {
        if (_cursorImage == null) return;

        bool visible = gamepadConnected ?? Gamepad.current != null;
        _cursorImage.gameObject.SetActive(visible);

        // Gamepad aktifken sistem fare imlecini gizle, mouse'a dönünce göster
        Cursor.visible = !visible;
    }
}
