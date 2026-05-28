using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Loot Box system logic.
/// Handles gold deduction safely (via LevelManager or PlayerPrefs),
/// instantiates UI prefabs correctly within the Canvas, and
/// manages box visual states (Open/Closed).
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

    [Header("Loot Box Visuals")]
    [Tooltip("The Image component on the Loot Box Button.")]
    [SerializeField] private Image _boxImage;

    [Tooltip("Sprite for the closed state of the box.")]
    [SerializeField] private Sprite _closedSprite;

    [Tooltip("Sprite for the open state of the box.")]
    [SerializeField] private Sprite _openSprite;

    [Tooltip("How long the box stays open before closing again (in seconds).")]
    [SerializeField] private float _openDuration = 1.0f;

    [Header("Loot Box Settings")]
    [Tooltip("List of UI item prefabs that can drop from the box.")]
    [SerializeField] private List<GameObject> _lootPrefabs = new List<GameObject>();

    [Tooltip("The UI Transform (e.g., a Panel inside the Canvas) where items will spawn.")]
    [SerializeField] private RectTransform _spawnPoint;

    // -------------------------------------------------------------------------
    // Private Fields
    // -------------------------------------------------------------------------

    // Prevents opening multiple boxes if the animation is still playing
    private bool _isOpening = false;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called via UI Button to open a box.
    /// Checks gold, deducts it, and triggers the visual sequence.
    /// </summary>
  public void TryOpenBox()
    {
        // Block interaction if the box is already in the opening process
        if (_isOpening) return;

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

        // --- ECONOMY CHECK (UPDATED) ---
        // LevelManager varsa oradan, yoksa GameSaveService'ten oku. 
        // PlayerPrefs kullanımı tamamen kaldırıldı.
        LevelManager levelManager = LevelManager.Instance;
        int currentGold = levelManager != null 
            ? levelManager.currentGold 
            : (GameSaveService.Instance != null ? GameSaveService.Instance.GetGold() : 0);

        if (currentGold < BoxCost)
        {
            Debug.Log($"[LootBoxManager] Yetersiz altın! Gereken: {BoxCost}, Mevcut: {currentGold}");
            return;
        }

        // --- DEDUCT GOLD (UPDATED) ---
        if (levelManager != null)
        {
            levelManager.SpendGold(BoxCost);
        }
        else if (GameSaveService.Instance != null)
        {
            int newGold = currentGold - BoxCost;
            GameSaveService.Instance.SetGold(newGold);
            GameEvents.SetGold(newGold); // Tells the UI to update
        }

        // Start the visual change and spawning process
        StartCoroutine(OpenBoxRoutine());
    }

    // -------------------------------------------------------------------------
    // Private Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles the visual sprite swapping, waiting, and resetting.
    /// </summary>
    private IEnumerator OpenBoxRoutine()
    {
        _isOpening = true;

        // Change the button image to the open state
        if (_boxImage != null && _openSprite != null)
        {
            _boxImage.sprite = _openSprite;
        }

        SpawnRandomItem();

        // Wait for the specified duration before resetting
        yield return new WaitForSeconds(_openDuration);

        // Change the button image back to the closed state
        if (_boxImage != null && _closedSprite != null)
        {
            _boxImage.sprite = _closedSprite;
        }

        _isOpening = false;
    }

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