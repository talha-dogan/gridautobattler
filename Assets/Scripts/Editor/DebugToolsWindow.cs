using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using TDEV.Core;

/// <summary>
/// Debug Tools Window — Developer tools to accelerate the game development process.
///
/// Tools:
///   1. Memory Cleanup       — Clears all Unity memory caches and triggers GC.
///   2. Quick Stage Start    — Instantly starts the selected level (enters Play Mode).
///   3. Quick Inventory Test — Fills inventory slots with test data.
///   4. Quick Time Test      — Quickly adjusts Time.timeScale.
///   5. Stress Testing       — Spawns a high amount of units to test performance.
///   6. Startup Money        — Injects startup currency for new player testing.
///
/// Menu: Tools -> TDEV -> Debug Tools
/// </summary>
public class DebugToolsWindow : EditorWindow
{
    // -------------------------------------------------------------------------
    // Window state
    // -------------------------------------------------------------------------

    private Vector2 _scrollPos;

    // Section foldouts
    private bool _foldMemory    = true;
    private bool _foldStage     = true;
    private bool _foldInventory = true;
    private bool _foldTime      = true;
    private bool _foldStress    = true;
    private bool _foldStartup   = true;

    // Quick Stage Start
    private int _targetLevelIndex = 0;   // 0-based; displayed as Level N+1
    private bool _autoStartBattle = true;

    // Quick Time Test
    private float _timeScale = 1f;
    private readonly float[] _timePresets = { 0.25f, 0.5f, 1f, 2f, 4f, 8f };

    // Stress Testing
    private int _stressUnitCount = 16;
    private bool _stressEnemyOnly = false;
    private bool _stressBothTeams = true;

    // Startup Money
    private int _startupAmount = 500;

    // Memory report
    private string _memoryReport = string.Empty;

    // Styles (lazy-init)
    private GUIStyle _headerStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _statusOkStyle;
    private GUIStyle _statusWarnStyle;
    private GUIStyle _statusErrorStyle;
    private bool _stylesInitialized;

    // -------------------------------------------------------------------------
    // Open window
    // -------------------------------------------------------------------------

    [MenuItem("Tools/TDEV/Debug Tools %#d")]
    public static void OpenWindow()
    {
        DebugToolsWindow window = GetWindow<DebugToolsWindow>("🛠 Debug Tools");
        window.minSize = new Vector2(340f, 550f);
        window.Show();
    }

    // -------------------------------------------------------------------------
    // GUI
    // -------------------------------------------------------------------------

    private void OnGUI()
    {
        InitStyles();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
        EditorGUILayout.Space(4);

        DrawStartupMoney();
        EditorGUILayout.Space(4);

        DrawMemoryCleanup();
        EditorGUILayout.Space(4);

        DrawQuickStageStart();
        EditorGUILayout.Space(4);

        DrawQuickInventoryTest();
        EditorGUILayout.Space(4);

        DrawQuickTimeTest();
        EditorGUILayout.Space(4);

        DrawStressTesting();

        EditorGUILayout.EndScrollView();
    }

    // -------------------------------------------------------------------------
    // Header
    // -------------------------------------------------------------------------

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("TDEV — Debug Tools", _headerStyle);
        EditorGUILayout.LabelField("Developer tools. Works only in the Editor.", EditorStyles.miniLabel);
        DrawSeparator();
    }

    // -------------------------------------------------------------------------
    // 1. Startup Money
    // -------------------------------------------------------------------------

    private void DrawStartupMoney()
    {
        _foldStartup = DrawSectionHeader("💎  Startup Money", _foldStartup);
        if (!_foldStartup) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        EditorGUILayout.HelpBox(
            "Injects a starting amount of gold directly into the save file " +
            "to simulate a new player's first session.",
            MessageType.Info);

        _startupAmount = EditorGUILayout.IntField("Startup Amount", _startupAmount);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("💰 Inject Startup Money", GUILayout.Height(32)))
        {
            ApplyStartupMoney(_startupAmount);
        }

        if (GUILayout.Button("🔄 Reset 'First Time' Flag", GUILayout.Height(32)))
        {
            PlayerPrefs.SetInt("IsFirstTimePlaying", 1);
            PlayerPrefs.Save();
            Debug.Log("[DebugTools] 'First Time Playing' flag has been reset to true.");
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void ApplyStartupMoney(int amount)
    {
        // Update PlayerPrefs
        PlayerPrefs.SetInt("PlayerGold", amount);
        PlayerPrefs.SetInt("IsFirstTimePlaying", 0);
        PlayerPrefs.Save();

        // Update GameSaveService if it's running
        if (Application.isPlaying && GameSaveService.Instance != null)
        {
            GameSaveService.Instance.SetGold(amount);
            GameEvents.SetGold(amount);
        }

        Debug.Log($"[DebugTools] Injected {amount} startup gold into the save file.");
    }

    // -------------------------------------------------------------------------
    // 2. Memory Cleanup
    // -------------------------------------------------------------------------

    private void DrawMemoryCleanup()
    {
        _foldMemory = DrawSectionHeader("🧹  Memory Cleanup", _foldMemory);
        if (!_foldMemory) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        EditorGUILayout.HelpBox(
            "Refreshes the Asset Database, unloads unused assets, " +
            "calls GC.Collect(), and clears the Resources cache.",
            MessageType.Info);

        if (!string.IsNullOrEmpty(_memoryReport))
        {
            EditorGUILayout.LabelField(_memoryReport, EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🧹 Full Cleanup", GUILayout.Height(32)))
        {
            RunFullMemoryCleanup();
        }

        if (GUILayout.Button("📊 Memory Report", GUILayout.Height(32)))
        {
            PrintMemoryReport();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("♻ GC.Collect", GUILayout.Height(24)))
        {
            long before = System.GC.GetTotalMemory(false);
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            long after = System.GC.GetTotalMemory(true);
            long freed = before - after;
            _memoryReport = $"GC: {freed / 1024} KB freed.";
            Debug.Log($"[DebugTools] GC.Collect -> {freed / 1024} KB freed.");
        }

        if (GUILayout.Button("🔄 AssetDB Refresh", GUILayout.Height(24)))
        {
            AssetDatabase.Refresh();
            _memoryReport = "AssetDatabase refreshed.";
            Debug.Log("[DebugTools] AssetDatabase.Refresh() called.");
        }

        if (GUILayout.Button("📦 Unload Unused", GUILayout.Height(24)))
        {
            Resources.UnloadUnusedAssets();
            _memoryReport = "Unused assets unloaded.";
            Debug.Log("[DebugTools] Resources.UnloadUnusedAssets() called.");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void RunFullMemoryCleanup()
    {
        long before = System.GC.GetTotalMemory(false);

        // 1. GC
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        // 2. Unload unused assets
        Resources.UnloadUnusedAssets();

        // 3. Asset Database refresh
        AssetDatabase.Refresh();

        long after = System.GC.GetTotalMemory(true);
        long freed = before - after;

        _memoryReport = $"✅ Full cleanup complete.\n" +
                        $"GC: {freed / 1024} KB freed.\n" +
                        $"Total managed memory: {after / (1024 * 1024)} MB";

        Debug.Log($"[DebugTools] Full Memory Cleanup complete. Freed ~{freed / 1024} KB. " +
                  $"Managed heap: {after / (1024 * 1024)} MB");

        Repaint();
    }

    private void PrintMemoryReport()
    {
        long managed = System.GC.GetTotalMemory(false);
        long totalReserved = (long)UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
        long totalAllocated = (long)UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        long totalUnused = (long)UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong();

        _memoryReport =
            $"📊 Memory Report:\n" +
            $"  Managed (GC): {managed / (1024 * 1024)} MB\n" +
            $"  Unity Allocated: {totalAllocated / (1024 * 1024)} MB\n" +
            $"  Unity Reserved: {totalReserved / (1024 * 1024)} MB\n" +
            $"  Unused Reserved: {totalUnused / (1024 * 1024)} MB";

        Debug.Log($"[DebugTools] Memory Report:\n{_memoryReport}");
        Repaint();
    }

// -------------------------------------------------------------------------
    // 3. Quick Stage Start
    // -------------------------------------------------------------------------

    private void DrawQuickStageStart()
    {
        _foldStage = DrawSectionHeader("⚡  Quick Stage Start", _foldStage);
        if (!_foldStage) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        // Get the level list from LevelManager
        LevelManager levelManager = FindFirstObjectByType<LevelManager>();
        int maxLevel = 0;
        string[] levelNames = null;

        if (levelManager != null && levelManager.levels != null && levelManager.levels.Count > 0)
        {
            maxLevel = levelManager.levels.Count - 1;
            levelNames = levelManager.levels
                .Select((l, i) => $"Level {i + 1}: {(l != null ? l.name : "null")}")
                .ToArray();
        }

        if (levelManager == null)
        {
            EditorGUILayout.HelpBox(
                "LevelManager not found in the scene. Please open the GridScene.",
                MessageType.Warning);
        }
        else if (levelNames == null || levelNames.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "LevelManager's level list is empty.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"Total Levels: {levelNames.Length}", EditorStyles.miniLabel);

            // Convert 0-based index to 1-based level number for the UI
            int displayLevel = _targetLevelIndex + 1;
            displayLevel = EditorGUILayout.IntSlider("Target Level", displayLevel, 1, maxLevel + 1);
            
            // Convert back to 0-based index for backend logic
            _targetLevelIndex = displayLevel - 1;

            if (levelNames != null && _targetLevelIndex < levelNames.Length)
                EditorGUILayout.LabelField(levelNames[_targetLevelIndex], EditorStyles.centeredGreyMiniLabel);
        }

        _autoStartBattle = EditorGUILayout.Toggle("Auto Start Battle", _autoStartBattle);

        EditorGUILayout.BeginHorizontal();

        bool canStart = levelManager != null && levelNames != null && levelNames.Length > 0;

        GUI.enabled = canStart && !Application.isPlaying;
        if (GUILayout.Button("▶ Play + Load Level", GUILayout.Height(32)))
        {
            QuickStartLevel(_targetLevelIndex, _autoStartBattle);
        }

        GUI.enabled = canStart && Application.isPlaying;
        if (GUILayout.Button("🔄 Reload Level", GUILayout.Height(32)))
        {
            QuickReloadCurrentLevel();
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying && canStart)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⏮ Previous Level", GUILayout.Height(24)))
                JumpToLevel(Mathf.Max(0, _targetLevelIndex - 1));
            if (GUILayout.Button("⏭ Next Level", GUILayout.Height(24)))
                JumpToLevel(Mathf.Min(maxLevel, _targetLevelIndex + 1));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void QuickStartLevel(int levelIndex, bool autoStart)
    {
        // Save the target level index to PlayerPrefs; it will be read when the game starts
        PlayerPrefs.SetInt("DebugTools_StartLevelIndex", levelIndex);
        PlayerPrefs.SetInt("DebugTools_AutoStartBattle", autoStart ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[DebugTools] Quick Stage Start -> Level {levelIndex + 1}, AutoBattle={autoStart}");
        EditorApplication.isPlaying = true;
    }

    private void QuickReloadCurrentLevel()
    {
        if (!Application.isPlaying) return;

        LevelManager lm = LevelManager.Instance;
        if (lm == null) { Debug.LogWarning("[DebugTools] LevelManager.Instance not found."); return; }

        // Read private _currentLevelIndex via reflection
        var field = typeof(LevelManager).GetField("_currentLevelIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int currentIdx = field != null ? (int)field.GetValue(lm) : 0;

        // Call LoadLevel via reflection
        var method = typeof(LevelManager).GetMethod("LoadLevel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(lm, new object[] { currentIdx });

        Debug.Log($"[DebugTools] Level {currentIdx + 1} reloaded.");
    }

    private void JumpToLevel(int levelIndex)
    {
        if (!Application.isPlaying) return;

        LevelManager lm = LevelManager.Instance;
        if (lm == null) { Debug.LogWarning("[DebugTools] LevelManager.Instance not found."); return; }

        // Update _currentLevelIndex
        var indexField = typeof(LevelManager).GetField("_currentLevelIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        indexField?.SetValue(lm, levelIndex);

        // Call LoadLevel
        var loadMethod = typeof(LevelManager).GetMethod("LoadLevel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loadMethod?.Invoke(lm, new object[] { levelIndex });

        _targetLevelIndex = levelIndex;
        Debug.Log($"[DebugTools] Jumped to Level {levelIndex + 1}.");
    }
// -------------------------------------------------------------------------
    // 4. Quick Inventory Test
    // -------------------------------------------------------------------------

    private void DrawQuickInventoryTest()
    {
        _foldInventory = DrawSectionHeader("🎒  Quick Inventory Test", _foldInventory);
        if (!_foldInventory) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        // Find PlayerArmyDataSO
        PlayerArmyDataSO armyData = FindPlayerArmyData();

        if (armyData == null)
        {
            EditorGUILayout.HelpBox(
                "PlayerArmyDataSO asset not found.\n" +
                "Check the path: Assets/GameData/PlayerArmyData.asset",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField($"Asset: {armyData.name}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Unlocked Slots: {armyData.unlockedPawnCount} / 8", EditorStyles.miniLabel);

        // Show slot statuses
        armyData.EnsureCapacity();
        for (int i = 0; i < armyData.armySlots.Count; i++)
        {
            var slot = armyData.armySlots[i];
            bool hasUnit = slot.baseUnitData != null;
            bool hasAnyEquip = slot.helmet != null || slot.vest != null ||
                               slot.pants != null || slot.weapon != null || slot.shield != null;

            string slotLabel = $"Slot {i}: {(hasUnit ? slot.baseUnitData.unitName : "EMPTY")}";
            string equipLabel = hasAnyEquip ? " [Equipped]" : " [No Equip]";

            GUIStyle style = i < armyData.unlockedPawnCount
                ? (hasUnit ? _statusOkStyle : _statusWarnStyle)
                : _statusErrorStyle;

            EditorGUILayout.LabelField(slotLabel + equipLabel, style);
        }

        EditorGUILayout.Space(4);

EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔓 Unlock All Slots", GUILayout.Height(28)))
        {
            Undo.RecordObject(armyData, "Unlock All Slots");
            armyData.unlockedPawnCount = 8;
            EditorUtility.SetDirty(armyData);
            
            // --- save to save file ---
            if (Application.isPlaying && GameSaveService.Instance != null)
            {
                GameSaveService.Instance.SetUnlockedPawnCount(8);
            }
            
            // Force UI update for the Upgrade Scene
            RefreshPawnShopVisibility(8);
            RefreshAllDropZones();
            
            Debug.Log("[DebugTools] All 8 slots unlocked and saved.");
        }

        if (GUILayout.Button("🔒 Reset to 1 Slot", GUILayout.Height(28)))
        {
            Undo.RecordObject(armyData, "Reset Slot Count");
            armyData.unlockedPawnCount = 1;
            EditorUtility.SetDirty(armyData);
            
            // --- save to save file ---
            if (Application.isPlaying && GameSaveService.Instance != null)
            {
                GameSaveService.Instance.SetUnlockedPawnCount(1);
            }
            
            // Force UI update for the Upgrade Scene
            RefreshPawnShopVisibility(1);
            RefreshAllDropZones();
            
            Debug.Log("[DebugTools] Slot count reset to 1 and saved.");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🗑 Clear All Equipment", GUILayout.Height(28)))
        {
            Undo.RecordObject(armyData, "Clear All Equipment");
            foreach (var slot in armyData.armySlots)
            {
                slot.helmet = null;
                slot.vest   = null;
                slot.pants  = null;
                slot.weapon = null;
                slot.shield = null;
            }
            EditorUtility.SetDirty(armyData);
            
            // Force UI update to clear visuals
            RefreshAllDropZones();
            
            Debug.Log("[DebugTools] All equipment cleared.");
        }

        if (GUILayout.Button("🎲 Fill Random Equipment", GUILayout.Height(28)))
        {
            FillRandomEquipment(armyData);
        }

        EditorGUILayout.EndHorizontal();

        // Gold Test Tools
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── Gold Tools ──", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("💰 +1000 Gold", GUILayout.Height(24)))
        {
            if (Application.isPlaying)
            {
                if (LevelManager.Instance != null)
                {
                    // Add gold using LevelManager if it exists in the current scene
                    LevelManager.Instance.AddGold(1000);
                    Debug.Log("[DebugTools] Added +1000 Gold (runtime via LevelManager).");
                }
                else
                {
                    // Handle scenes without LevelManager (e.g. UpgradeScene)
                    int current = GameSaveService.Instance != null 
                        ? GameSaveService.Instance.GetGold() 
                        : PlayerPrefs.GetInt("PlayerGold", 0);
                        
                    int newTotal = current + 1000;
                    
                    PlayerPrefs.SetInt("PlayerGold", newTotal);
                    PlayerPrefs.Save();
                    
                    if (GameSaveService.Instance != null)
                        GameSaveService.Instance.SetGold(newTotal);
                        
                    // Broadcast the update so the UI catches it instantly
                    GameEvents.SetGold(newTotal);
                    Debug.Log($"[DebugTools] Added +1000 Gold (runtime via GameEvents). New total: {newTotal}");
                }
            }
            else
            {
                // Editor mode behavior when the game is not playing
                int current = PlayerPrefs.GetInt("PlayerGold", 0);
                PlayerPrefs.SetInt("PlayerGold", current + 1000);
                PlayerPrefs.Save();
                Debug.Log($"[DebugTools] +1000 Gold added to PlayerPrefs. New total: {current + 1000}");
            }
        }

        if (GUILayout.Button("💸 Reset Gold", GUILayout.Height(24)))
        {
            if (Application.isPlaying)
            {
                if (LevelManager.Instance != null)
                {
                    // Reset gold using LevelManager
                    LevelManager.Instance.SpendGold(LevelManager.Instance.currentGold);
                    Debug.Log("[DebugTools] Gold reset (runtime via LevelManager).");
                }
                else
                {
                    // Handle reset for scenes without LevelManager
                    PlayerPrefs.SetInt("PlayerGold", 0);
                    PlayerPrefs.Save();
                    
                    if (GameSaveService.Instance != null)
                        GameSaveService.Instance.SetGold(0);
                        
                    // Broadcast the reset so the UI updates
                    GameEvents.SetGold(0);
                    Debug.Log("[DebugTools] Gold reset (runtime via GameEvents).");
                }
            }
            else
            {
                // Editor mode behavior when the game is not playing
                PlayerPrefs.SetInt("PlayerGold", 0);
                PlayerPrefs.Save();
                Debug.Log("[DebugTools] Gold reset in PlayerPrefs.");
            }
        }

        if (GUILayout.Button("📋 Show Gold", GUILayout.Height(24)))
        {
            int gold = 0;
            if (Application.isPlaying)
            {
                if (LevelManager.Instance != null) 
                    gold = LevelManager.Instance.currentGold;
                else if (GameSaveService.Instance != null) 
                    gold = GameSaveService.Instance.GetGold();
                else 
                    gold = PlayerPrefs.GetInt("PlayerGold", 0);
            }
            else
            {
                gold = PlayerPrefs.GetInt("PlayerGold", 0);
            }
            Debug.Log($"[DebugTools] Current Gold: {gold}");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private PlayerArmyDataSO FindPlayerArmyData()
    {
        // First try to get it from UnitSpawner in the scene
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        if (spawner != null && spawner.playerArmyData != null)
            return spawner.playerArmyData;

        // Then search the asset database
        string[] guids = AssetDatabase.FindAssets("t:PlayerArmyDataSO");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<PlayerArmyDataSO>(path);
        }

        return null;
    }

    private void FillRandomEquipment(PlayerArmyDataSO armyData)
    {
        // Find all EquipmentDataSO assets
        string[] guids = AssetDatabase.FindAssets("t:EquipmentDataSO");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[DebugTools] No EquipmentDataSO assets found.");
            return;
        }

        List<EquipmentDataSO> allEquipment = guids
            .Select(g => AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(e => e != null)
            .ToList();

        Undo.RecordObject(armyData, "Fill Random Equipment");

        for (int i = 0; i < armyData.unlockedPawnCount && i < armyData.armySlots.Count; i++)
        {
            var slot = armyData.armySlots[i];
            // Assign random equipment to each slot type
            slot.helmet = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Helmet);
            slot.vest   = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Vest);
            slot.pants  = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Pants);
            slot.weapon = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Weapon);
            slot.shield = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Shield);
        }

        EditorUtility.SetDirty(armyData);
        
        // Force UI update
        RefreshAllDropZones();
        
        Debug.Log($"[DebugTools] Random equipment assigned to {armyData.unlockedPawnCount} slots.");
    }

    private EquipmentDataSO GetRandomEquipmentOfSlot(List<EquipmentDataSO> all, EquipmentSlot slot)
    {
        var filtered = all.Where(e => e.slot == slot).ToList();
        if (filtered.Count == 0) return null;
        return filtered[Random.Range(0, filtered.Count)];
    }

    // -------------------------------------------------------------------------
    // UI Refresh Helpers for Upgrade Scene
    // -------------------------------------------------------------------------

    private void RefreshAllDropZones()
    {
        if (!Application.isPlaying) return;

        // Find all drop zones in the scene and force them to refresh their visuals
        var dropZones = FindObjectsByType<UpgradeCharacterDropZoneUI>(FindObjectsInactive.Include);
        foreach (var dz in dropZones)
        {
            dz.RefreshAllVisuals();
        }
    }

    private void RefreshPawnShopVisibility(int newCount)
    {
        if (!Application.isPlaying) return;

        // Broadcast the pawn count change to update UI buttons
        GameEvents.SetPawnCount(newCount);

        // Update the 3D showcase pawns visibility via reflection (private method call)
        var pawnShop = FindFirstObjectByType<PawnShopManager>();
        if (pawnShop != null)
        {
            var applyVis = typeof(PawnShopManager).GetMethod("ApplyVisibility", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            applyVis?.Invoke(pawnShop, null);
        }
    }

    // -------------------------------------------------------------------------
    // 5. Quick Time Test
    // -------------------------------------------------------------------------

    private void DrawQuickTimeTest()
    {
        _foldTime = DrawSectionHeader("⏱  Quick Time Test", _foldTime);
        if (!_foldTime) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Time.timeScale can only be changed in Play Mode.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField($"Current timeScale: {Time.timeScale:F2}x", EditorStyles.boldLabel);
        }

        // Slider
        GUI.enabled = Application.isPlaying;
        float newScale = EditorGUILayout.Slider("Time Scale", _timeScale, 0f, 10f);
        if (!Mathf.Approximately(newScale, _timeScale))
        {
            _timeScale = newScale;
            if (Application.isPlaying)
                Time.timeScale = _timeScale;
        }

        // Preset buttons
        EditorGUILayout.LabelField("Quick Select:", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        foreach (float preset in _timePresets)
        {
            bool isCurrent = Application.isPlaying && Mathf.Approximately(Time.timeScale, preset);
            GUI.backgroundColor = isCurrent ? Color.green : Color.white;
            if (GUILayout.Button($"{preset}x", GUILayout.Height(28)))
            {
                _timeScale = preset;
                if (Application.isPlaying)
                    Time.timeScale = preset;
                Debug.Log($"[DebugTools] Time.timeScale = {preset}");
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("⏸ Pause (0x)", GUILayout.Height(28)))
        {
            _timeScale = 0f;
            if (Application.isPlaying) Time.timeScale = 0f;
            Debug.Log("[DebugTools] Game paused (timeScale=0).");
        }

        if (GUILayout.Button("▶ Normal (1x)", GUILayout.Height(28)))
        {
            _timeScale = 1f;
            if (Application.isPlaying) Time.timeScale = 1f;
            Debug.Log("[DebugTools] timeScale returned to normal (1x).");
        }

        EditorGUILayout.EndHorizontal();

        // Fixed Timestep info
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── Physics Settings ──", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField($"Fixed Timestep: {Time.fixedDeltaTime * 1000:F1} ms  " +
                                   $"({1f / Time.fixedDeltaTime:F0} Hz)", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Max Allowed Timestep: {Time.maximumDeltaTime * 1000:F0} ms",
                                   EditorStyles.miniLabel);

        GUI.enabled = true;
        EditorGUILayout.EndVertical();
    }


    // -------------------------------------------------------------------------
    // 6. Stress Testing
    // -------------------------------------------------------------------------

    private void DrawStressTesting()
    {
        _foldStress = DrawSectionHeader("💥  Stress Testing", _foldStress);
        if (!_foldStress) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        EditorGUILayout.HelpBox(
            "Works in Play Mode. Spawns a high amount of units into the scene " +
            "by duplicating the enemy formation of the current level.",
            MessageType.Info);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use Stress Testing.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        // Show current unit counts
        if (BattleManager.Instance != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"👤 Player Units: {BattleManager.Instance.playerUnits.Count}",
                _statusOkStyle);
            EditorGUILayout.LabelField($"👹 Enemy Units: {BattleManager.Instance.enemyUnits.Count}",
                _statusWarnStyle);
            EditorGUILayout.EndHorizontal();
        }

        _stressUnitCount = EditorGUILayout.IntSlider("Spawn Count", _stressUnitCount, 1, 64);

        EditorGUILayout.BeginHorizontal();
        _stressBothTeams = EditorGUILayout.ToggleLeft("Both Teams", _stressBothTeams, GUILayout.Width(120));
        _stressEnemyOnly = EditorGUILayout.ToggleLeft("Enemy Only", _stressEnemyOnly, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        // Mutual exclusion
        if (_stressBothTeams && _stressEnemyOnly) _stressEnemyOnly = false;

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("💥 Stress Spawn", GUILayout.Height(32)))
        {
            RunStressTest(_stressUnitCount, _stressBothTeams, _stressEnemyOnly);
        }

        if (GUILayout.Button("🗑 Clear All Units", GUILayout.Height(32)))
        {
            ClearAllUnits();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        // --- DEĞİŞTİRİLEN START BATTLE BUTONU ---
        if (GUILayout.Button("⚔ Start Battle", GUILayout.Height(28)))
        {
            if (UnitSpawner.Instance != null)
            {
                if (BattleManager.Instance != null && !BattleManager.Instance.isBattleStarted)
                {
                    // Doğrudan BattleManager'ı DEĞİL, önce oyuncu askerlerini spawn eden UnitSpawner'ı çağırıyoruz.
                    UnitSpawner.Instance.StartBattle();
                    Debug.Log("[DebugTools] Battle started via UnitSpawner.");
                }
                else
                {
                    Debug.LogWarning("[DebugTools] Battle already started or BattleManager missing.");
                }
            }
            else
            {
                Debug.LogWarning("[DebugTools] UnitSpawner not found! Cannot start battle.");
            }
        }

        if (GUILayout.Button("🔄 Reload Level", GUILayout.Height(28)))
        {
            QuickReloadCurrentLevel();
        }

        EditorGUILayout.EndHorizontal();

        // FPS Indicator
        EditorGUILayout.Space(4);
        float fps = 1f / Time.unscaledDeltaTime;
        string fpsColor = fps >= 30 ? "✅" : fps >= 15 ? "⚠️" : "❌";
        EditorGUILayout.LabelField($"{fpsColor} FPS: {fps:F1}  |  Frame: {Time.frameCount}",
            EditorStyles.miniLabel);

        EditorGUILayout.EndVertical();
    }

    private void RunStressTest(int count, bool bothTeams, bool enemyOnly)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[DebugTools] Stress Test only works in Play Mode.");
            return;
        }

        LevelManager lm = LevelManager.Instance;
        if (lm == null || lm.levels == null || lm.levels.Count == 0)
        {
            Debug.LogWarning("[DebugTools] LevelManager or level list not found.");
            return;
        }

        // Get current level index
        var indexField = typeof(LevelManager).GetField("_currentLevelIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int currentIdx = indexField != null ? (int)indexField.GetValue(lm) : 0;
        currentIdx = Mathf.Clamp(currentIdx, 0, lm.levels.Count - 1);

        LevelDataSO levelData = lm.levels[currentIdx];
        if (levelData == null)
        {
            Debug.LogWarning("[DebugTools] Current level data is null.");
            return;
        }

        // Get a unit data from enemy formation
        BaseUnitDataSO enemyData = null;
        if (levelData.enemyFormation != null && levelData.enemyFormation.units.Count > 0)
            enemyData = levelData.enemyFormation.units[0].unitData;

        // Player unit data
        BaseUnitDataSO playerData = levelData.meleeData;

        int spawnedCount = 0;

        // Spawn at random positions
        for (int i = 0; i < count; i++)
        {
            // Random grid position
            int col = Random.Range(0, GridManager.Columns);
            int row = Random.Range(0, GridManager.Rows);

            GridNode node = GridManager.Instance?.GetNode(col, row);
            if (node == null || node.IsOccupied) continue;

            if (!enemyOnly && bothTeams && i % 2 == 0)
            {
                // Player unit
                if (playerData != null && UnitFactory.Instance != null)
                {
                    UnitFactory.Instance.CreateUnit(playerData, node.WorldPosition, Team.Player);
                    node.IsOccupied = true;
                    spawnedCount++;
                }
            }
            else
            {
                // Enemy unit
                if (enemyData != null && UnitFactory.Instance != null)
                {
                    UnitFactory.Instance.CreateUnit(enemyData, node.WorldPosition, Team.Enemy);
                    node.IsOccupied = true;
                    spawnedCount++;
                }
            }
        }

        Debug.Log($"[DebugTools] Stress Test: {spawnedCount} units spawned. " +
                  $"Total: P={BattleManager.Instance?.playerUnits.Count} " +
                  $"E={BattleManager.Instance?.enemyUnits.Count}");
    }

    private void ClearAllUnits()
    {
        if (!Application.isPlaying) return;

        // Fetch all active BaseUnit instances in the scene without using the deprecated sort mode
        BaseUnit[] allUnits = FindObjectsByType<BaseUnit>(FindObjectsInactive.Exclude);
        int count = allUnits.Length;

        foreach (var unit in allUnits)
        {
            if (UnitFactory.Instance != null)
                UnitFactory.Instance.ReleaseUnit(unit);
            else
                Object.Destroy(unit.gameObject);
        }

        if (BattleManager.Instance != null)
            BattleManager.Instance.ResetBattleState();

        if (GridManager.Instance != null)
            GridManager.Instance.ResetGrid();

        Debug.Log($"[DebugTools] {count} units cleared.");
    }
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private bool DrawSectionHeader(string title, bool foldout)
    {
        EditorGUILayout.Space(2);
        bool result = EditorGUILayout.Foldout(foldout, title, true, _headerStyle);
        return result;
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(2);
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(2);
    }

    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _headerStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12
        };

        _sectionStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(8, 8, 6, 6),
            margin  = new RectOffset(4, 4, 2, 2)
        };

        _statusOkStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.2f, 0.8f, 0.2f) }
        };

        _statusWarnStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(1f, 0.8f, 0.1f) }
        };

        _statusErrorStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.5f, 0.5f, 0.5f) }
        };
    }

    // -------------------------------------------------------------------------
    // Auto-repaint in Play Mode for live FPS display
    // -------------------------------------------------------------------------

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }

   // -------------------------------------------------------------------------
    // Auto Start Battle & Quick Load Logic (Editor Only)
    // -------------------------------------------------------------------------

    private static int _autoStartAttemptCount = 0;

    // Register to Play Mode state changes automatically when the editor loads
    [InitializeOnLoadMethod]
    private static void RegisterPlayModeStateListener()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        // When Unity successfully enters Play Mode
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            // Check if we need to override the starting level or auto-start the battle
            if (PlayerPrefs.HasKey("DebugTools_StartLevelIndex") || PlayerPrefs.GetInt("DebugTools_AutoStartBattle", 0) == 1)
            {
                _autoStartAttemptCount = 0;
                EditorApplication.update += TryAutoStartBattle;
            }
        }
        // Make sure to clean up the update loop if we exit play mode early
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= TryAutoStartBattle;
        }
    }

    private static void TryAutoStartBattle()
    {
        _autoStartAttemptCount++;

        // Find managers safely
        LevelManager lm = Object.FindFirstObjectByType<LevelManager>();
        UnitSpawner us = Object.FindFirstObjectByType<UnitSpawner>();
        BattleManager bm = Object.FindFirstObjectByType<BattleManager>();

        // Wait until all necessary managers are ready in the scene
        if (lm != null && us != null && bm != null)
        {
            // Unsubscribe from the update loop so it only runs once
            EditorApplication.update -= TryAutoStartBattle;

            // 1. Force load the selected level first
            if (PlayerPrefs.HasKey("DebugTools_StartLevelIndex"))
            {
                int targetLevel = PlayerPrefs.GetInt("DebugTools_StartLevelIndex");
                
                // Remove the key so it only triggers on this specific play session
                PlayerPrefs.DeleteKey("DebugTools_StartLevelIndex");
                PlayerPrefs.Save();

                // Update _currentLevelIndex and call LoadLevel via reflection
                var indexField = typeof(LevelManager).GetField("_currentLevelIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (indexField != null)
                {
                    int currentIdx = (int)indexField.GetValue(lm);
                    if (currentIdx != targetLevel)
                    {
                        indexField.SetValue(lm, targetLevel);
                        var loadMethod = typeof(LevelManager).GetMethod("LoadLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        loadMethod?.Invoke(lm, new object[] { targetLevel });
                        Debug.Log($"[DebugTools] ⚡ Jumped to Level {targetLevel + 1} upon scene load.");
                    }
                }
            }

            // 2. Trigger the battle automatically
            if (PlayerPrefs.GetInt("DebugTools_AutoStartBattle", 0) == 1)
            {
                PlayerPrefs.SetInt("DebugTools_AutoStartBattle", 0);
                PlayerPrefs.Save();

                if (!bm.isBattleStarted)
                {
                    us.StartBattle();
                    Debug.Log("[DebugTools] ⚔ Auto-started battle successfully upon scene load.");
                }
            }
        }
        // Timeout safeguard
        else if (_autoStartAttemptCount > 500) 
        {
            EditorApplication.update -= TryAutoStartBattle;
            PlayerPrefs.DeleteKey("DebugTools_StartLevelIndex");
            PlayerPrefs.SetInt("DebugTools_AutoStartBattle", 0);
            PlayerPrefs.Save();
            Debug.LogWarning("[DebugTools] Auto-start/Level load timed out. Managers not found in the loaded scene.");
        }
    }
}