using UnityEngine;

/// <summary>
/// MVP pattern — Presenter katmanı.
///
/// Sorumluluklar:
///   • GameEvents event bus'ını dinler (Model tarafı).
///   • Gelen verileri IGameView arayüzü üzerinden View'a iletir.
///   • View'ın somut tipini bilmez — sadece IGameView arayüzünü kullanır.
///
/// Kullanım:
///   GameUIManager bu Presenter'ı Awake'te oluşturur ve kendisini
///   IGameView olarak geçirir. Presenter, GameEvents'e abone olur
///   ve tüm UI güncellemelerini View üzerinden yapar.
///
/// Bu yapı sayesinde:
///   • GameUIManager sadece UI bileşenlerini yönetir (View).
///   • İş mantığı (hangi event'te ne gösterilir) Presenter'da kalır.
///   • Unit testlerde IGameView mock'lanabilir.
/// </summary>
public class GamePresenter
{
    private readonly IGameView _view;

    // ─────────────────────────────────────────────────────────────────────────
    // Constructor
    // ─────────────────────────────────────────────────────────────────────────

    public GamePresenter(IGameView view)
    {
        _view = view;
        Subscribe();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// GameEvents'e abone olur.
    /// GameUIManager.Awake() içinde çağrılır (Presenter constructor'ı ile birlikte).
    /// </summary>
    private void Subscribe()
    {
        GameEvents.OnStatusTextChanged += HandleStatusText;
        GameEvents.OnLevelIndexChanged += HandleLevelIndex;
        GameEvents.OnGoldChanged       += HandleGold;
        GameEvents.OnBattleStarted     += HandleBattleStarted;
        GameEvents.OnLevelWin          += HandleLevelWin;
        GameEvents.OnLevelLose         += HandleLevelLose;
    }

    /// <summary>
    /// GameEvents aboneliklerini iptal eder.
    /// GameUIManager.OnDestroy() içinde çağrılmalıdır.
    /// </summary>
    public void Dispose()
    {
        GameEvents.OnStatusTextChanged -= HandleStatusText;
        GameEvents.OnLevelIndexChanged -= HandleLevelIndex;
        GameEvents.OnGoldChanged       -= HandleGold;
        GameEvents.OnBattleStarted     -= HandleBattleStarted;
        GameEvents.OnLevelWin          -= HandleLevelWin;
        GameEvents.OnLevelLose         -= HandleLevelLose;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Event Handlers — Model → View dönüşüm mantığı burada
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleStatusText(string message)
    {
        _view.ShowStatusText(message);
    }

    private void HandleLevelIndex(int displayIndex)
    {
        _view.ShowLevelIndex(displayIndex);
    }

    private void HandleGold(int amount)
    {
        _view.ShowGold(amount);
    }

    private void HandleBattleStarted()
    {
        _view.ShowStatusText("Savaş başladı!");
    }

    private void HandleLevelWin(string message)
    {
        // LevelManager zaten SetStatusText çağırıyor; burada ek işlem gerekmez.
        // Ancak Presenter'a özel UI animasyonu veya efekt tetiklenebilir.
        if (!string.IsNullOrEmpty(message))
            _view.ShowStatusText(message);
    }

    private void HandleLevelLose(string message)
    {
        if (!string.IsNullOrEmpty(message))
            _view.ShowStatusText(message);
    }
}
