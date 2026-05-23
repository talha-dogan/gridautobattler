using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central manager for the Upgrade Scene.
/// Owns the player's equipment inventory (the pool of unequipped items)
/// as a pure data list, and handles the async transition to GridScene.
///
/// Responsibilities (SRP):
///   1. Maintain the runtime inventory list (add / remove) — data only, no UI.
///   2. Load GridScene asynchronously when the READY button is pressed.
///
/// UI management is fully delegated to StashDropZoneUI and the drag-drop system.
/// </summary>
public class UpgradeManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    public static UpgradeManager Instance { get; private set; }

    // -------------------------------------------------------------------------
    // Inspector references
    // -------------------------------------------------------------------------

    [Header("Ordu Verisi")]
    [Tooltip("Tüm 8 karakter slotunu içeren ordu veri varlığı.")]
    [SerializeField] private PlayerArmyDataSO _armyData;

    [Tooltip("Envanter listesinin üzerinde bulunduğu kök Canvas (sürükleme hayaleti için).")]
    [SerializeField] private Canvas _rootCanvas;

    [Header("Sahne Geçişi")]
    [Tooltip("READY butonuna basıldığında yüklenecek sahnenin adı.")]
    [SerializeField] private string _gridSceneName = "GridScene";

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // The player's current unequipped item pool — data only.
    private readonly List<EquipmentDataSO> _inventory = new List<EquipmentDataSO>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Singleton setup — only one UpgradeManager should exist per scene.
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Subscribe to equipment change events so the UI stays in sync.
        GameEvents.OnEquipmentChanged += HandleEquipmentChanged;
    }

    private void Start()
    {
        // Refresh all drop zone visuals to reflect any pre-existing equipment in armyData.
        RefreshAllDropZones();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        GameEvents.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    // -------------------------------------------------------------------------
    // Public API — Inventory management
    // -------------------------------------------------------------------------

    /// <summary>
    /// Adds an item to the player's inventory data list.
    /// Called by StashDropZoneUI or UpgradeCharacterDropZoneUI when a swap displaces an existing item.
    /// </summary>
    public void AddToInventory(EquipmentDataSO item)
    {
        if (item == null) return;

        _inventory.Add(item);
        Debug.Log($"[UpgradeManager] Envantere eklendi: '{item.equipmentName}'");
    }

    /// <summary>
    /// Removes the first occurrence of the item from the player's inventory data list.
    /// Called by UpgradeCharacterDropZoneUI when an item is equipped.
    /// </summary>
    public void RemoveFromInventory(EquipmentDataSO item)
    {
        if (item == null) return;

        bool removed = _inventory.Remove(item);
        if (removed)
            Debug.Log($"[UpgradeManager] Envanterden çıkarıldı: '{item.equipmentName}'");
        else
            Debug.LogWarning($"[UpgradeManager] Envanterden çıkarılmak istenen eşya bulunamadı: '{item.equipmentName}'");
    }

    /// <summary>
    /// Returns a read-only view of the current inventory.
    /// Use this for UI display or save-game serialisation.
    /// </summary>
    public IReadOnlyList<EquipmentDataSO> GetInventory() => _inventory.AsReadOnly();

    // -------------------------------------------------------------------------
    // Public API — Scene transition
    // -------------------------------------------------------------------------

    /// <summary>
    /// Assign this method to the READY button's OnClick event.
    /// Starts an asynchronous load of the GridScene.
    /// </summary>
    public void OnReadyButtonPressed()
    {
        StartCoroutine(LoadGridSceneAsync());
    }

    // -------------------------------------------------------------------------
    // GameEvents handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reacts to equipment changes broadcast by UpgradeCharacterDropZoneUI.
    /// Hook for future systems (e.g. stat preview panels).
    /// </summary>
    private void HandleEquipmentChanged(int slotIndex, EquipmentDataSO equipment)
    {
        // Hook for future stat preview or validation logic.
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds all UpgradeCharacterDropZoneUI components in the scene and tells
    /// each one to refresh its visuals from the current ArmyData.
    /// Called once on Start to sync the UI with any pre-existing equipment.
    /// </summary>
    private void RefreshAllDropZones()
    {
        UpgradeCharacterDropZoneUI[] dropZones = FindObjectsByType<UpgradeCharacterDropZoneUI>(FindObjectsInactive.Include);
        foreach (UpgradeCharacterDropZoneUI zone in dropZones)
        {
            zone.RefreshAllVisuals();
        }
    }

    /// <summary>
    /// Loads the GridScene asynchronously.
    /// Clears the GameEvents bus before the scene unloads to prevent stale subscribers.
    /// </summary>
    private IEnumerator LoadGridSceneAsync()
    {
        Debug.Log($"[UpgradeManager] GridScene yükleniyor: '{_gridSceneName}'...");

        // Clear stale event subscribers from the current scene before unloading.
        GameEvents.ClearAllEvents();

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(_gridSceneName);

        // Prevent the scene from activating until it is fully loaded.
        asyncLoad.allowSceneActivation = false;

        // Wait until the scene is 90% loaded (Unity's threshold before activation).
        while (asyncLoad.progress < 0.9f)
        {
            yield return null;
        }

        // Activate the scene.
        asyncLoad.allowSceneActivation = true;
    }
}
