using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Central manager for the Upgrade Scene.
/// Owns the player's equipment inventory (the pool of unequipped items),
/// drives the inventory UI list, and handles the async transition to GridScene.
///
/// Responsibilities (SRP):
///   1. Maintain the runtime inventory list (add / remove).
///   2. Rebuild the inventory scroll list UI whenever the inventory changes.
///   3. Load GridScene asynchronously when the READY button is pressed.
///
/// All other systems (drop zones, drag items) communicate with this class
/// through its public API — no direct coupling to UI components outside this class.
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

    [Header("Envanter UI")]
    [Tooltip("Envanter eşyalarının spawn edileceği Content transform (ScrollRect içindeki).")]
    [SerializeField] private Transform _inventoryContent;

    [Tooltip("Envanter slotu prefab'ı — UpgradeDragItemUI bileşeni içermeli.")]
    [SerializeField] private GameObject _inventoryItemPrefab;

    [Tooltip("Envanter listesinin üzerinde bulunduğu kök Canvas (sürükleme hayaleti için).")]
    [SerializeField] private Canvas _rootCanvas;

    [Header("Sahne Geçişi")]
    [Tooltip("READY butonuna basıldığında yüklenecek sahnenin adı.")]
    [SerializeField] private string _gridSceneName = "GridScene";

    [Header("Başlangıç Envanteri (Test)")]
    [Tooltip("Sahne açıldığında envantere eklenecek başlangıç eşyaları.")]
    [SerializeField] private List<EquipmentDataSO> _startingInventory = new List<EquipmentDataSO>();

    // -------------------------------------------------------------------------
    // Runtime state
    // -------------------------------------------------------------------------

    // The player's current unequipped item pool.
    private readonly List<EquipmentDataSO> _inventory = new List<EquipmentDataSO>();

    // Cached references to spawned inventory slot GameObjects for efficient rebuild.
    private readonly List<GameObject> _spawnedSlots = new List<GameObject>();

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
        // Populate the inventory with the starting items defined in the Inspector.
        foreach (EquipmentDataSO item in _startingInventory)
        {
            if (item != null)
                _inventory.Add(item);
        }

        // Build the initial inventory UI.
        RebuildInventoryUI();

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
    /// Adds an item to the player's inventory and refreshes the UI list.
    /// Called by UpgradeCharacterDropZoneUI when a swap displaces an existing item.
    /// </summary>
    public void AddToInventory(EquipmentDataSO item)
    {
        if (item == null) return;

        _inventory.Add(item);
        RebuildInventoryUI();

        Debug.Log($"[UpgradeManager] Envantere eklendi: '{item.equipmentName}'");
    }

    /// <summary>
    /// Removes the first occurrence of the item from the player's inventory
    /// and refreshes the UI list.
    /// Called by UpgradeCharacterDropZoneUI when an item is equipped.
    /// </summary>
    public void RemoveFromInventory(EquipmentDataSO item)
    {
        if (item == null) return;

        bool removed = _inventory.Remove(item);
        if (removed)
        {
            RebuildInventoryUI();
            Debug.Log($"[UpgradeManager] Envanterden çıkarıldı: '{item.equipmentName}'");
        }
        else
        {
            Debug.LogWarning($"[UpgradeManager] Envanterden çıkarılmak istenen eşya bulunamadı: '{item.equipmentName}'");
        }
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
    /// Currently used as a hook for future systems (e.g. stat preview panels).
    /// </summary>
    private void HandleEquipmentChanged(int slotIndex, EquipmentDataSO equipment)
    {
        // Hook for future stat preview or validation logic.
        // The inventory UI is already updated by AddToInventory / RemoveFromInventory.
    }

    // -------------------------------------------------------------------------
    // Private helpers — UI
    // -------------------------------------------------------------------------

    /// <summary>
    /// Destroys all existing inventory slot GameObjects and re-spawns them
    /// from the current _inventory list. Called whenever the inventory changes.
    /// </summary>
    private void RebuildInventoryUI()
    {
        if (_inventoryContent == null || _inventoryItemPrefab == null)
        {
            Debug.LogWarning("[UpgradeManager] Envanter UI için Content veya Prefab atanmamış.");
            return;
        }

        // Destroy all previously spawned slots.
        foreach (GameObject slot in _spawnedSlots)
        {
            if (slot != null) Destroy(slot);
        }
        _spawnedSlots.Clear();

        // Spawn one slot per inventory item.
        foreach (EquipmentDataSO item in _inventory)
        {
            GameObject slotGO = Instantiate(_inventoryItemPrefab, _inventoryContent);
            _spawnedSlots.Add(slotGO);

            // Initialise the drag component with the item data and root canvas.
            UpgradeDragItemUI dragItem = slotGO.GetComponent<UpgradeDragItemUI>();
            if (dragItem != null)
            {
                dragItem.Initialize(item, _rootCanvas);
            }
            else
            {
                Debug.LogWarning($"[UpgradeManager] Envanter prefab'ında UpgradeDragItemUI bileşeni bulunamadı: '{_inventoryItemPrefab.name}'");
            }
        }
    }

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

    // -------------------------------------------------------------------------
    // Private helpers — Scene transition
    // -------------------------------------------------------------------------

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
