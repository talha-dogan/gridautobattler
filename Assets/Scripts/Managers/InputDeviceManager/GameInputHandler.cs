using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Singleton input yöneticisi.
/// InputSystem_Actions wrapper'ını okur ve oyunun geri kalanına
/// temiz C# event'leri olarak iletir.
///
/// Sistemin geri kalanı InputSystem'a doğrudan bağımlı değildir —
/// sadece bu sınıfın event'lerini dinler. Bu sayede binding'ler
/// değişse bile sadece bu dosya güncellenir.
///
/// Kullanım:
///   GameInputHandler.Instance.OnConfirm += HandleConfirm;
///   GameInputHandler.Instance.OnSpeedCycle += HandleSpeedCycle;
/// </summary>
public class GameInputHandler : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GameInputHandler Instance { get; private set; }

    // ── Generated wrapper ─────────────────────────────────────────────────────
    // InputSystem_Actions, .inputactions asset'inden Unity tarafından
    // otomatik üretilen C# sınıfıdır. "Generate C# Class" seçeneği
    // Inspector'da aktif olmalıdır.
    private InputSystem_Actions _actions;

    // ── Public Events ─────────────────────────────────────────────────────────

    /// <summary>A / Space / Sol tık — seçim, sürükleme başlat, UI onayla.</summary>
    public event Action OnConfirm;

    /// <summary>B / Escape / Sağ tık — iptal, geri.</summary>
    public event Action OnCancel;

    /// <summary>RB / Tab — savaş hızını döngüle.</summary>
    public event Action OnSpeedCycle;

    /// <summary>Start / Enter — READY butonunu tetikle.</summary>
    public event Action OnReady;

    /// <summary>
    /// Sol stick / D-pad / WASD — sanal cursor veya grid navigasyonu için
    /// normalize edilmiş 2D yön vektörü.
    /// </summary>
    public Vector2 NavigateValue { get; private set; }

    /// <summary>
    /// Sanal cursor için ham stick değeri (GamepadCursor tarafından okunur).
    /// </summary>
    public Vector2 CursorDelta { get; private set; }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _actions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _actions.Enable();

        // ── UI / Gameplay action map bağlantıları ─────────────────────────────
        // "UI" action map — Upgrade sahnesinde kullanılır
        _actions.UI.Click.performed          += _ => OnConfirm?.Invoke();
        _actions.UI.Cancel.performed         += _ => OnCancel?.Invoke();

        // "Player" action map — Grid sahnesinde kullanılır
        _actions.Player.Attack.performed     += _ => OnConfirm?.Invoke();
        _actions.Player.Interact.performed   += _ => OnReady?.Invoke();
        _actions.Player.SpeedCycle.performed += _ => OnSpeedCycle?.Invoke();
    }

    private void OnDisable()
    {
        _actions.Disable();

        _actions.UI.Click.performed          -= _ => OnConfirm?.Invoke();
        _actions.UI.Cancel.performed         -= _ => OnCancel?.Invoke();
        _actions.Player.Attack.performed     -= _ => OnConfirm?.Invoke();
        _actions.Player.Interact.performed   -= _ => OnReady?.Invoke();
        _actions.Player.SpeedCycle.performed -= _ => OnSpeedCycle?.Invoke();
    }

    private void Update()
    {
        // Navigate ve CursorDelta her frame okunur (polling)
        // çünkü bunlar sürekli değer taşıyan eksenlerdir.
        NavigateValue = _actions.Player.Move.ReadValue<Vector2>();
        CursorDelta   = _actions.UI.Point.ReadValue<Vector2>();
    }

    private void OnDestroy()
    {
        _actions?.Dispose();
    }

    // ── Action Map Switching ──────────────────────────────────────────────────

    /// <summary>
    /// Upgrade sahnesine geçişte UI action map'i aktive et.
    /// </summary>
    public void SwitchToUI()
    {
        _actions.Player.Disable();
        _actions.UI.Enable();
    }

    /// <summary>
    /// Grid / Battle sahnesine geçişte Player action map'i aktive et.
    /// </summary>
    public void SwitchToPlayer()
    {
        _actions.UI.Disable();
        _actions.Player.Enable();
    }
}
