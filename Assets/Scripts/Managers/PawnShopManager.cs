using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages Pawn purchasing business logic.
///
/// SRP: Only answers the question "can a pawn be bought?" and executes the purchase.
/// UI updates belong to PawnShopUI, coin tracking to LevelManager/PlayerPrefs, 
/// and pawn count to PlayerArmyDataSO.
///
/// Both the ShowcasePawn (world-space) and ShowCasePawnHolderSlot (canvas UI) 
/// for each index are toggled together — they are always synchronized.
///
/// Dependencies (DIP — Injected via Inspector):
///   • PlayerArmyDataSO      — reads/writes unlockedPawnCount
///   • LevelManager          — reads/writes currentGold (active economy system when present)
///   • _showcasePawns        — world-space pawn root objects (ordered 0-7)
///   • _holderSlots          — canvas UI slot objects (ordered 0-7, matches pawns)
/// </summary>
public class PawnShopManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    public const int PawnCost = 300;
    public const int MaxPawns = 8;

    // Defines the key used to load and save gold from PlayerPrefs as a fallback
    private const string GOLD_SAVE_KEY = "PlayerGold";

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Data")]
    [Tooltip("Army data — unlockedPawnCount is read/written here.")]
    [SerializeField] private PlayerArmyDataSO _armyData;

    [Header("Showcase Pawns (Ordered 0-7) — World Space")]
    [Tooltip("Drag the 8 ShowcasePawn root objects in the scene here in order.")]
    [SerializeField] private List<GameObject> _showcasePawns = new List<GameObject>();

    [Header("Holder Slots (Ordered 0-7) — Canvas UI")]
    [Tooltip("Drag the 8 ShowCasePawnHolderSlot objects in the canvas here in order. Index must match _showcasePawns.")]
    [SerializeField] private List<GameObject> _holderSlots = new List<GameObject>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Adjust pawn + slot visibilities based on saved unlockedPawnCount.
        ApplyVisibility();

        // Broadcast current pawn count to the UI.
        GameEvents.SetPawnCount(_armyData != null ? _armyData.unlockedPawnCount : 1);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by BuyPawnButton.OnClick().
    /// Unlocks a new pawn + slot if coins are sufficient and max hasn't been reached.
    /// </summary>
    public void TryBuyPawn()
    {
        if (_armyData == null)
        {
            Debug.LogError("[PawnShopManager] PlayerArmyDataSO not assigned!");
            return;
        }

        if (_armyData.unlockedPawnCount >= MaxPawns)
        {
            Debug.Log("[PawnShopManager] Maximum pawn count reached (8/8).");
            return;
        }

        // Check LevelManager first, fallback to PlayerPrefs if it's null (e.g., in UpgradeScene)
        LevelManager levelManager = LevelManager.Instance;
        int currentGold = levelManager != null ? levelManager.currentGold : PlayerPrefs.GetInt(GOLD_SAVE_KEY, 0);

        if (currentGold < PawnCost)
        {
            Debug.Log($"[PawnShopManager] Insufficient coins. Required: {PawnCost}, Current: {currentGold}");
            return;
        }

        // Deduct gold and update storage/events safely
        if (levelManager != null)
        {
            // If LevelManager exists, let it handle the spending and internal storage saving
            levelManager.SpendGold(PawnCost);
        }
        else
        {
            // If LevelManager does not exist, update PlayerPrefs directly and broadcast the change
            currentGold -= PawnCost;
            PlayerPrefs.SetInt(GOLD_SAVE_KEY, currentGold);
            PlayerPrefs.Save();

            // Notify UI components (like PawnShopUI) to refresh the coin text
            GameEvents.SetGold(currentGold);
        }

        // Unlock the new pawn + slot.
        _armyData.unlockedPawnCount++;
        ApplyVisibility();

        // Broadcast the updated pawn count (PawnShopUI listens to this).
        GameEvents.SetPawnCount(_armyData.unlockedPawnCount);

        Debug.Log($"[PawnShopManager] Pawn purchased! " +
                  $"New count: {_armyData.unlockedPawnCount}/{MaxPawns}, " +
                  $"Remaining coins: {(levelManager != null ? levelManager.currentGold : currentGold)}");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Toggles both world-space pawns and canvas holder slots based on unlockedPawnCount.
    /// Index 0 .. (unlockedPawnCount - 1) will be active, others inactive.
    /// </summary>
    private void ApplyVisibility()
    {
        if (_armyData == null) return;

        int unlocked = Mathf.Clamp(_armyData.unlockedPawnCount, 1, MaxPawns);

        for (int i = 0; i < MaxPawns; i++)
        {
            bool active = i < unlocked;

            // World-space pawn
            if (i < _showcasePawns.Count && _showcasePawns[i] != null)
                _showcasePawns[i].SetActive(active);

            // Canvas UI holder slot
            if (i < _holderSlots.Count && _holderSlots[i] != null)
                _holderSlots[i].SetActive(active);
        }
    }
}