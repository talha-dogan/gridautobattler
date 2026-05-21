using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the Loot Box system logic.
/// Handles gold deduction safely (via LevelManager or PlayerPrefs) and
/// instantiates UI prefabs correctly within the Canvas.
/// </summary>
public class LootBoxManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const int BoxCost = 200;
    private const string GOLD_SAVE_KEY = "PlayerGold";

    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Loot Box Settings")]
    [Tooltip("List of UI item prefabs that can drop from the box.")]
    [SerializeField] private List<GameObject> _lootPrefabs = new List<GameObject>();

    [Tooltip("The UI Transform (e.g., a Panel inside the Canvas) where items will spawn.")]
    [SerializeField] private RectTransform _spawnPoint;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called via UI Button to open a box.
    /// Checks gold, deducts it, and spawns a random UI item.
    /// </summary>
    public void TryOpenBox()
    {
        if (_lootPrefabs == null || _lootPrefabs.Count == 0)
        {
            Debug.LogError("[LootBoxManager] Kutu boş! Lütfen inspector üzerinden prefab ekle.");
            return;
        }

        if (_spawnPoint == null)
        {
            Debug.LogError("[LootBoxManager] Spawn Point atanmamış! Canvas içinden bir obje sürükle.");
            return;
        }

        // Check economy
        LevelManager levelManager = LevelManager.Instance;
        int currentGold = levelManager != null ? levelManager.currentGold : PlayerPrefs.GetInt(GOLD_SAVE_KEY, 0);

        if (currentGold < BoxCost)
        {
            Debug.Log($"[LootBoxManager] Yetersiz altın! Gereken: {BoxCost}, Mevcut: {currentGold}");
            return;
        }

        // Deduct gold safely
        if (levelManager != null)
        {
            levelManager.SpendGold(BoxCost);
        }
        else
        {
            currentGold -= BoxCost;
            PlayerPrefs.SetInt(GOLD_SAVE_KEY, currentGold);
            PlayerPrefs.Save();
            GameEvents.SetGold(currentGold);
        }

        SpawnRandomItem();
    }

    // -------------------------------------------------------------------------
    // Private Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Spawns a random UI prefab as a child of the defined spawn point.
    /// Ensures RectTransform scales properly within the Canvas.
    /// </summary>
    private void SpawnRandomItem()
    {
        // Pick a random prefab
        int randomIndex = Random.Range(0, _lootPrefabs.Count);
        GameObject prefabToSpawn = _lootPrefabs[randomIndex];

        // Instantiate as a child of the UI spawn point, keeping worldPositionStays false.
        // This is the CRITICAL part for UI elements so they don't break the Canvas scale.
        GameObject spawnedItem = Instantiate(prefabToSpawn, _spawnPoint, false);

        // Reset position and scale just to be completely safe
        RectTransform rect = spawnedItem.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero; // Centers it on the spawn point
            rect.localScale = Vector3.one;        // Resets any weird scaling
        }

        Debug.Log($"[LootBoxManager] Kutu açıldı! Çıkan eşya: {prefabToSpawn.name}");
    }
}