using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages Pawn Shop UI updates.
///
/// SRP: Only updates UI components — business logic is in PawnShopManager.
/// Listens to the GameEvents bus to update the coin text and the interactable 
/// state of the buy button.
///
/// Dependencies (Injected via Inspector):
///  • CoinText (TMP)       — shows the current coin amount
///  • BuyPawnButton        — disabled when coins are insufficient or max pawns reached
///  • PlayerArmyDataSO     — to read the current pawn count
/// </summary>
public class PawnShopUI : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("UI References")]
    [Tooltip("TextMeshProUGUI component showing the coin amount (CoinText object).")]
    [SerializeField] private TextMeshProUGUI _coinText;

    [Tooltip("Pawn purchase button — disabled on insufficient coins or max pawns.")]
    [SerializeField] private Button _buyPawnButton;

    [Header("Data")]
    [Tooltip("Army data to read the current pawn count.")]
    [SerializeField] private PlayerArmyDataSO _armyData;

    [Header("Debug")]
    [Tooltip("Amount of coins to test via the Inspector context menu.")]
    [SerializeField] private int _debugCoinAmount = 100;

    // Defines the key used to load gold from PlayerPrefs as a fallback
    private const string GOLD_SAVE_KEY = "PlayerGold";

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        GameEvents.OnGoldChanged += HandleGoldChanged;
        GameEvents.OnPawnCountChanged += HandlePawnCountChanged;
    }

    private void OnDestroy()
    {
        GameEvents.OnGoldChanged -= HandleGoldChanged;
        GameEvents.OnPawnCountChanged -= HandlePawnCountChanged;
    }

    private void Start()
    {
        // Try to get current gold from LevelManager first. 
        // If LevelManager doesn't exist (e.g., in UpgradeScene), fallback to PlayerPrefs.
        LevelManager lm = LevelManager.Instance;
        int currentGold = lm != null ? lm.currentGold : PlayerPrefs.GetInt(GOLD_SAVE_KEY, 0);

        RefreshCoinText(currentGold);
        RefreshButtonState(currentGold, GetCurrentPawnCount());
    }

    // -------------------------------------------------------------------------
    // Debugging / Inspector Tools
    // -------------------------------------------------------------------------

    [ContextMenu("Test Coin Update")]
    private void TestCoinUpdateFromInspector()
    {
        // Simulates a gold change event using the debug value from the Inspector
        HandleGoldChanged(_debugCoinAmount);
        Debug.Log($"[PawnShopUI] Tested UI update with {_debugCoinAmount} coins.");
    }

    // -------------------------------------------------------------------------
    // GameEvents handlers
    // -------------------------------------------------------------------------

    private void HandleGoldChanged(int newTotal)
    {
        RefreshCoinText(newTotal);
        RefreshButtonState(newTotal, GetCurrentPawnCount());
    }

    private void HandlePawnCountChanged(int newCount)
    {
        LevelManager lm = LevelManager.Instance;
        // Fallback to PlayerPrefs if LevelManager is null
        int currentGold = lm != null ? lm.currentGold : PlayerPrefs.GetInt(GOLD_SAVE_KEY, 0);
        RefreshButtonState(currentGold, newCount);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void RefreshCoinText(int coins)
    {
        if (_coinText != null)
            _coinText.text = $"🪙 {coins}";
    }

    private void RefreshButtonState(int coins, int pawnCount)
    {
        if (_buyPawnButton == null) return;

        bool canBuy = coins >= PawnShopManager.PawnCost
                   && pawnCount < PawnShopManager.MaxPawns;

        _buyPawnButton.interactable = canBuy;
    }

    private int GetCurrentPawnCount()
    {
        if (_armyData == null) return 1;
        return _armyData.unlockedPawnCount;
    }
}