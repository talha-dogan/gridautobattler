/// <summary>
/// MVP pattern — View arayüzü.
///
/// GamePresenter bu arayüz üzerinden View'a erişir; somut UI sınıfına
/// (GameUIManager) doğrudan bağımlı değildir. Bu sayede View kolayca
/// test edilebilir veya farklı bir implementasyonla değiştirilebilir.
/// </summary>
public interface IGameView
{
    /// <summary>Durum mesajını günceller (savaş sonucu, talimatlar vb.).</summary>
    void ShowStatusText(string message);

    /// <summary>Level etiketini günceller.</summary>
    void ShowLevelIndex(int displayIndex);

    /// <summary>Altın etiketini günceller.</summary>
    void ShowGold(int amount);
}
