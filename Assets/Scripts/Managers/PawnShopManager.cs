using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages Pawn purchasing business logic.
///
/// SRP: Only answers the question "can a pawn be bought?" and executes the purchase.
/// UI updates belong to PawnShopUI, coin tracking to LevelManager/GameSaveService,
/// and pawn count to PlayerArmyDataSO.
///
/// Save/Load: PlayerPrefs yerine GameSaveService kullanır.
/// </summary>
public class PawnShopManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    public const int PawnCost = 300;
    public const int MaxPawns = 8;

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Data")]
    [Tooltip("Army data — unlockedPawnCount is read/written here.")]
    [SerializeField] private PlayerArmyDataSO _armyData;

    [Header("Showcase Pawns (Ordered 0-7) — World Space")]
    [SerializeField] private List<GameObject> _showcasePawns = new List<GameObject>();

    [Header("Holder Slots (Ordered 0-7) — Canvas UI")]
    [SerializeField] private List<GameObject> _holderSlots = new List<GameObject>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // GameSaveService'ten pawn sayısını yükle
        if (_armyData != null && GameSaveService.Instance != null)
        {
            int saved = GameSaveService.Instance.GetUnlockedPawnCount();
            _armyData.unlockedPawnCount = Mathf.Clamp(saved, 1, MaxPawns);
        }

        ApplyVisibility();
        GameEvents.SetPawnCount(_armyData != null ? _armyData.unlockedPawnCount : 1);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

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

        // Altın kontrolü: önce LevelManager, yoksa GameSaveService
        LevelManager levelManager = LevelManager.Instance;
        int currentGold = levelManager != null
            ? levelManager.currentGold
            : (GameSaveService.Instance != null ? GameSaveService.Instance.GetGold() : 0);

        if (currentGold < PawnCost)
        {
            Debug.Log($"[PawnShopManager] Insufficient coins. Required: {PawnCost}, Current: {currentGold}");
            return;
        }

        // Altın harca
        if (levelManager != null)
        {
            levelManager.SpendGold(PawnCost);
        }
        else if (GameSaveService.Instance != null)
        {
            int newGold = currentGold - PawnCost;
            GameSaveService.Instance.SetGold(newGold);
            GameEvents.SetGold(newGold);
        }

        // Pawn kilidi aç
        _armyData.unlockedPawnCount++;
        GameSaveService.Instance?.SetUnlockedPawnCount(_armyData.unlockedPawnCount);

        ApplyVisibility();
        GameEvents.SetPawnCount(_armyData.unlockedPawnCount);

        Debug.Log($"[PawnShopManager] Pawn purchased! New count: {_armyData.unlockedPawnCount}/{MaxPawns}");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void ApplyVisibility()
    {
        if (_armyData == null) return;

        int unlocked = Mathf.Clamp(_armyData.unlockedPawnCount, 1, MaxPawns);

        for (int i = 0; i < MaxPawns; i++)
        {
            bool active = i < unlocked;

            if (i < _showcasePawns.Count && _showcasePawns[i] != null)
                _showcasePawns[i].SetActive(active);

            if (i < _holderSlots.Count && _holderSlots[i] != null)
                _holderSlots[i].SetActive(active);
        }
    }
}
