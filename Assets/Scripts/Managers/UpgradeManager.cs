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
///   3. Persist inventory to GameSaveService on every change.
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

    private readonly List<EquipmentDataSO> _inventory = new List<EquipmentDataSO>();

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        GameEvents.OnEquipmentChanged += HandleEquipmentChanged;
    }

    private void Start()
    {
        RefreshAllDropZones();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameEvents.OnEquipmentChanged -= HandleEquipmentChanged;
    }

    // -------------------------------------------------------------------------
    // Public API — Inventory management
    // -------------------------------------------------------------------------

    public void AddToInventory(EquipmentDataSO item)
    {
        if (item == null) return;
        _inventory.Add(item);
        PersistInventory();
        Debug.Log($"[UpgradeManager] Envantere eklendi: '{item.equipmentName}'");
    }

    public void RemoveFromInventory(EquipmentDataSO item)
    {
        if (item == null) return;
        bool removed = _inventory.Remove(item);
        if (removed)
        {
            PersistInventory();
            Debug.Log($"[UpgradeManager] Envanterden çıkarıldı: '{item.equipmentName}'");
        }
        else
        {
            Debug.LogWarning($"[UpgradeManager] Envanterden çıkarılmak istenen eşya bulunamadı: '{item.equipmentName}'");
        }
    }

    public IReadOnlyList<EquipmentDataSO> GetInventory() => _inventory.AsReadOnly();

    // -------------------------------------------------------------------------
    // Public API — Army slot persistence
    // -------------------------------------------------------------------------

    /// <summary>
    /// Tüm army slotlarını GameSaveService'e yazar.
    /// UpgradeCharacterDropZoneUI ekipman değişikliğinde çağırır.
    /// </summary>
    public void PersistArmySlots()
    {
        if (_armyData == null || GameSaveService.Instance == null) return;

        var slotDataList = new List<ArmySlotSaveData>();

        for (int i = 0; i < _armyData.armySlots.Count; i++)
        {
            var slot = _armyData.armySlots[i];
            if (slot == null) continue;

            slotDataList.Add(new ArmySlotSaveData
            {
                slotIndex      = i,
                baseUnitDataName = slot.baseUnitData != null ? slot.baseUnitData.name : string.Empty,
                weaponAssetName  = slot.weapon  != null ? slot.weapon.name  : string.Empty,
                helmetAssetName  = slot.helmet  != null ? slot.helmet.name  : string.Empty,
                vestAssetName    = slot.vest    != null ? slot.vest.name    : string.Empty,
                pantsAssetName   = slot.pants   != null ? slot.pants.name   : string.Empty,
                shieldAssetName  = slot.shield  != null ? slot.shield.name  : string.Empty,
            });
        }

        GameSaveService.Instance.SetArmySlots(slotDataList);
    }

    // -------------------------------------------------------------------------
    // Public API — Scene transition
    // -------------------------------------------------------------------------

    public void OnReadyButtonPressed()
    {
        // Sahne geçişinden önce son kaydı yap
        PersistArmySlots();
        PersistInventory();
        StartCoroutine(LoadGridSceneAsync());
    }

    // -------------------------------------------------------------------------
    // GameEvents handlers
    // -------------------------------------------------------------------------

    private void HandleEquipmentChanged(int slotIndex, EquipmentDataSO equipment)
    {
        PersistArmySlots();
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private void RefreshAllDropZones()
    {
        UpgradeCharacterDropZoneUI[] dropZones = FindObjectsByType<UpgradeCharacterDropZoneUI>(FindObjectsInactive.Include);
        foreach (UpgradeCharacterDropZoneUI zone in dropZones)
            zone.RefreshAllVisuals();
    }

    private void PersistInventory()
    {
        if (GameSaveService.Instance == null) return;

        var names = new List<string>();
        foreach (var item in _inventory)
        {
            if (item != null) names.Add(item.name);
        }
        GameSaveService.Instance.SetInventory(names);
    }

    private System.Collections.IEnumerator LoadGridSceneAsync()
    {
        Debug.Log($"[UpgradeManager] GridScene geçişi başlıyor: '{_gridSceneName}'...");

        // SceneLoader varsa additive geçiş kullan
        if (SceneLoader.Instance != null)
        {
            // SceneLoader.TransitionTo: loads GridScene first, then unloads UpgradeScene.
            // Cleanup pipeline (Addressables release, pool clear, GC) runs automatically.
            // Use a hardcoded string instead of GetActiveScene().name to guarantee the correct
            // scene is unloaded even if the active scene changes during the transition.
            SceneLoader.Instance.TransitionTo(
                targetScene:   _gridSceneName,
                sceneToUnload: "UpgradeScene",
                onComplete:    () => Debug.Log($"[UpgradeManager] '{_gridSceneName}' geçişi tamamlandı.")
            );
        }
        else
        {
            // Fallback: SceneLoader yoksa eski yöntemi kullan
            Debug.LogWarning("[UpgradeManager] SceneLoader bulunamadı. Eski LoadScene yöntemi kullanılıyor.");
            GameEvents.ClearAllEvents();

            AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(_gridSceneName);
            asyncLoad.allowSceneActivation = false;

            while (asyncLoad.progress < 0.9f)
                yield return null;

            asyncLoad.allowSceneActivation = true;
        }

        yield break;
    }
}
