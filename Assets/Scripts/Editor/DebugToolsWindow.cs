using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using TDEV.Core;

/// <summary>
/// Debug Tools Window — Oyun geliştirme sürecini hızlandıran araçlar.
///
/// Araçlar:
///   1. Memory Cleanup     — Tüm Unity bellek önbelleklerini ve GC'yi temizler.
///   2. Quick Stage Start  — Seçili level'ı anında başlatır (Play Mode'a girer).
///   3. Quick Inventory Test — Envanter slotlarını test verileriyle doldurur.
///   4. Quick Time Test    — Time.timeScale'i hızlıca ayarlar.
///   5. Stress Testing     — Sahneye yoğun birim spawn ederek performansı test eder.
///
/// Menü: Tools → TDEV → Debug Tools
/// </summary>
public class DebugToolsWindow : EditorWindow
{
    // ─────────────────────────────────────────────────────────────────────────
    // Window state
    // ─────────────────────────────────────────────────────────────────────────

    private Vector2 _scrollPos;

    // ── Section foldouts ──────────────────────────────────────────────────────
    private bool _foldMemory    = true;
    private bool _foldStage     = true;
    private bool _foldInventory = true;
    private bool _foldTime      = true;
    private bool _foldStress    = true;

    // ── Quick Stage Start ─────────────────────────────────────────────────────
    private int _targetLevelIndex = 0;   // 0-based; displayed as Level N+1
    private bool _autoStartBattle = true;

    // ── Quick Time Test ───────────────────────────────────────────────────────
    private float _timeScale = 1f;
    private readonly float[] _timePresets = { 0.25f, 0.5f, 1f, 2f, 4f, 8f };

    // ── Stress Testing ────────────────────────────────────────────────────────
    private int _stressUnitCount = 16;
    private bool _stressEnemyOnly = false;
    private bool _stressBothTeams = true;

    // ── Memory report ─────────────────────────────────────────────────────────
    private string _memoryReport = string.Empty;

    // ── Styles (lazy-init) ────────────────────────────────────────────────────
    private GUIStyle _headerStyle;
    private GUIStyle _sectionStyle;
    private GUIStyle _statusOkStyle;
    private GUIStyle _statusWarnStyle;
    private GUIStyle _statusErrorStyle;
    private bool _stylesInitialized;

    // ─────────────────────────────────────────────────────────────────────────
    // Open window
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/TDEV/Debug Tools %#d")]
    public static void OpenWindow()
    {
        DebugToolsWindow window = GetWindow<DebugToolsWindow>("🛠 Debug Tools");
        window.minSize = new Vector2(340f, 480f);
        window.Show();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        InitStyles();

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        DrawHeader();
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

    // ─────────────────────────────────────────────────────────────────────────
    // Header
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("TDEV — Debug Tools", _headerStyle);
        EditorGUILayout.LabelField("Geliştirici araçları. Sadece Editor'da çalışır.", EditorStyles.miniLabel);
        DrawSeparator();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Memory Cleanup
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawMemoryCleanup()
    {
        _foldMemory = DrawSectionHeader("🧹  Memory Cleanup", _foldMemory);
        if (!_foldMemory) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        EditorGUILayout.HelpBox(
            "Asset Database'i yeniler, kullanılmayan asset'leri boşaltır, " +
            "GC.Collect() çağırır ve Resources önbelleğini temizler.",
            MessageType.Info);

        if (!string.IsNullOrEmpty(_memoryReport))
        {
            EditorGUILayout.LabelField(_memoryReport, EditorStyles.wordWrappedMiniLabel);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🧹 Tam Temizlik", GUILayout.Height(32)))
        {
            RunFullMemoryCleanup();
        }

        if (GUILayout.Button("📊 Bellek Raporu", GUILayout.Height(32)))
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
            _memoryReport = $"GC: {freed / 1024} KB serbest bırakıldı.";
            Debug.Log($"[DebugTools] GC.Collect → {freed / 1024} KB freed.");
        }

        if (GUILayout.Button("🔄 AssetDB Refresh", GUILayout.Height(24)))
        {
            AssetDatabase.Refresh();
            _memoryReport = "AssetDatabase yenilendi.";
            Debug.Log("[DebugTools] AssetDatabase.Refresh() called.");
        }

        if (GUILayout.Button("📦 Unload Unused", GUILayout.Height(24)))
        {
            Resources.UnloadUnusedAssets();
            _memoryReport = "Kullanılmayan asset'ler boşaltıldı.";
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

        _memoryReport = $"✅ Tam temizlik tamamlandı.\n" +
                        $"GC: {freed / 1024} KB serbest bırakıldı.\n" +
                        $"Toplam yönetilen bellek: {after / (1024 * 1024)} MB";

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
            $"📊 Bellek Raporu:\n" +
            $"  Yönetilen (GC): {managed / (1024 * 1024)} MB\n" +
            $"  Unity Ayrılan:  {totalAllocated / (1024 * 1024)} MB\n" +
            $"  Unity Rezerve:  {totalReserved / (1024 * 1024)} MB\n" +
            $"  Kullanılmayan:  {totalUnused / (1024 * 1024)} MB";

        Debug.Log($"[DebugTools] Memory Report:\n{_memoryReport}");
        Repaint();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Quick Stage Start
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawQuickStageStart()
    {
        _foldStage = DrawSectionHeader("⚡  Quick Stage Start", _foldStage);
        if (!_foldStage) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        // LevelManager'dan level listesini al
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
                "LevelManager sahnede bulunamadı. GridScene'i açın.",
                MessageType.Warning);
        }
        else if (levelNames == null || levelNames.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "LevelManager'da level listesi boş.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField($"Toplam Level: {levelNames.Length}", EditorStyles.miniLabel);

            _targetLevelIndex = EditorGUILayout.IntSlider(
                "Hedef Level", _targetLevelIndex, 0, maxLevel);

            if (levelNames != null && _targetLevelIndex < levelNames.Length)
                EditorGUILayout.LabelField(levelNames[_targetLevelIndex], EditorStyles.centeredGreyMiniLabel);
        }

        _autoStartBattle = EditorGUILayout.Toggle("Savaşı Otomatik Başlat", _autoStartBattle);

        EditorGUILayout.BeginHorizontal();

        bool canStart = levelManager != null && levelNames != null && levelNames.Length > 0;

        GUI.enabled = canStart && !Application.isPlaying;
        if (GUILayout.Button("▶ Play + Level Yükle", GUILayout.Height(32)))
        {
            QuickStartLevel(_targetLevelIndex, _autoStartBattle);
        }

        GUI.enabled = canStart && Application.isPlaying;
        if (GUILayout.Button("🔄 Level Yeniden Yükle", GUILayout.Height(32)))
        {
            QuickReloadCurrentLevel();
        }

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (Application.isPlaying && canStart)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("⏮ Önceki Level", GUILayout.Height(24)))
                JumpToLevel(Mathf.Max(0, _targetLevelIndex - 1));
            if (GUILayout.Button("⏭ Sonraki Level", GUILayout.Height(24)))
                JumpToLevel(Mathf.Min(maxLevel, _targetLevelIndex + 1));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    private void QuickStartLevel(int levelIndex, bool autoStart)
    {
        // PlayerPrefs'e hedef level index'i kaydet; oyun başladığında okuyacak
        PlayerPrefs.SetInt("DebugTools_StartLevelIndex", levelIndex);
        PlayerPrefs.SetInt("DebugTools_AutoStartBattle", autoStart ? 1 : 0);
        PlayerPrefs.Save();

        Debug.Log($"[DebugTools] Quick Stage Start → Level {levelIndex + 1}, AutoBattle={autoStart}");
        EditorApplication.isPlaying = true;
    }

    private void QuickReloadCurrentLevel()
    {
        if (!Application.isPlaying) return;

        LevelManager lm = LevelManager.Instance;
        if (lm == null) { Debug.LogWarning("[DebugTools] LevelManager.Instance bulunamadı."); return; }

        // Reflection ile private _currentLevelIndex'i oku
        var field = typeof(LevelManager).GetField("_currentLevelIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int currentIdx = field != null ? (int)field.GetValue(lm) : 0;

        // LoadLevel metodunu reflection ile çağır
        var method = typeof(LevelManager).GetMethod("LoadLevel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method?.Invoke(lm, new object[] { currentIdx });

        Debug.Log($"[DebugTools] Level {currentIdx + 1} yeniden yüklendi.");
    }

    private void JumpToLevel(int levelIndex)
    {
        if (!Application.isPlaying) return;

        LevelManager lm = LevelManager.Instance;
        if (lm == null) { Debug.LogWarning("[DebugTools] LevelManager.Instance bulunamadı."); return; }

        // _currentLevelIndex'i güncelle
        var indexField = typeof(LevelManager).GetField("_currentLevelIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        indexField?.SetValue(lm, levelIndex);

        // LoadLevel'ı çağır
        var loadMethod = typeof(LevelManager).GetMethod("LoadLevel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        loadMethod?.Invoke(lm, new object[] { levelIndex });

        _targetLevelIndex = levelIndex;
        Debug.Log($"[DebugTools] Level {levelIndex + 1}'e atlandı.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Quick Inventory Test
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawQuickInventoryTest()
    {
        _foldInventory = DrawSectionHeader("🎒  Quick Inventory Test", _foldInventory);
        if (!_foldInventory) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        // PlayerArmyDataSO'yu bul
        PlayerArmyDataSO armyData = FindPlayerArmyData();

        if (armyData == null)
        {
            EditorGUILayout.HelpBox(
                "PlayerArmyDataSO asset'i bulunamadı.\n" +
                "Assets/GameData/PlayerArmyData.asset yolunu kontrol edin.",
                MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField($"Asset: {armyData.name}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Açık Slot: {armyData.unlockedPawnCount} / 8", EditorStyles.miniLabel);

        // Slot durumlarını göster
        armyData.EnsureCapacity();
        for (int i = 0; i < armyData.armySlots.Count; i++)
        {
            var slot = armyData.armySlots[i];
            bool hasUnit = slot.baseUnitData != null;
            bool hasAnyEquip = slot.helmet != null || slot.vest != null ||
                               slot.pants != null || slot.weapon != null || slot.shield != null;

            string slotLabel = $"Slot {i}: {(hasUnit ? slot.baseUnitData.unitName : "BOŞ")}";
            string equipLabel = hasAnyEquip ? " [Ekipman var]" : " [Ekipman yok]";

            GUIStyle style = i < armyData.unlockedPawnCount
                ? (hasUnit ? _statusOkStyle : _statusWarnStyle)
                : _statusErrorStyle;

            EditorGUILayout.LabelField(slotLabel + equipLabel, style);
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔓 Tüm Slotları Aç", GUILayout.Height(28)))
        {
            Undo.RecordObject(armyData, "Unlock All Slots");
            armyData.unlockedPawnCount = 8;
            EditorUtility.SetDirty(armyData);
            Debug.Log("[DebugTools] Tüm 8 slot açıldı.");
        }

        if (GUILayout.Button("🔒 1 Slota Sıfırla", GUILayout.Height(28)))
        {
            Undo.RecordObject(armyData, "Reset Slot Count");
            armyData.unlockedPawnCount = 1;
            EditorUtility.SetDirty(armyData);
            Debug.Log("[DebugTools] Slot sayısı 1'e sıfırlandı.");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🗑 Tüm Ekipmanı Temizle", GUILayout.Height(28)))
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
            Debug.Log("[DebugTools] Tüm ekipman temizlendi.");
        }

        if (GUILayout.Button("🎲 Rastgele Ekipman Doldur", GUILayout.Height(28)))
        {
            FillRandomEquipment(armyData);
        }

        EditorGUILayout.EndHorizontal();

        // Gold test araçları
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── Gold Araçları ──", EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("💰 +1000 Gold", GUILayout.Height(24)))
        {
            if (Application.isPlaying && LevelManager.Instance != null)
            {
                LevelManager.Instance.AddGold(1000);
                Debug.Log("[DebugTools] +1000 Gold eklendi (runtime).");
            }
            else
            {
                int current = PlayerPrefs.GetInt("PlayerGold", 0);
                PlayerPrefs.SetInt("PlayerGold", current + 1000);
                PlayerPrefs.Save();
                Debug.Log($"[DebugTools] +1000 Gold PlayerPrefs'e eklendi. Yeni toplam: {current + 1000}");
            }
        }

        if (GUILayout.Button("💸 Gold Sıfırla", GUILayout.Height(24)))
        {
            if (Application.isPlaying && LevelManager.Instance != null)
            {
                var field = typeof(LevelManager).GetField("currentGold",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                // SpendGold ile sıfırla
                LevelManager.Instance.SpendGold(LevelManager.Instance.currentGold);
                Debug.Log("[DebugTools] Gold sıfırlandı (runtime).");
            }
            else
            {
                PlayerPrefs.SetInt("PlayerGold", 0);
                PlayerPrefs.Save();
                Debug.Log("[DebugTools] Gold PlayerPrefs'te sıfırlandı.");
            }
        }

        if (GUILayout.Button("📋 Gold Göster", GUILayout.Height(24)))
        {
            int gold = Application.isPlaying && LevelManager.Instance != null
                ? LevelManager.Instance.currentGold
                : PlayerPrefs.GetInt("PlayerGold", 0);
            Debug.Log($"[DebugTools] Mevcut Gold: {gold}");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private PlayerArmyDataSO FindPlayerArmyData()
    {
        // Önce sahnedeki UnitSpawner'dan al
        UnitSpawner spawner = FindFirstObjectByType<UnitSpawner>();
        if (spawner != null && spawner.playerArmyData != null)
            return spawner.playerArmyData;

        // Sonra asset veritabanından ara
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
        // Tüm EquipmentDataSO asset'lerini bul
        string[] guids = AssetDatabase.FindAssets("t:EquipmentDataSO");
        if (guids.Length == 0)
        {
            Debug.LogWarning("[DebugTools] Hiç EquipmentDataSO asset'i bulunamadı.");
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
            // Her slot tipine rastgele ekipman ata
            slot.helmet = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Helmet);
            slot.vest   = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Vest);
            slot.pants  = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Pants);
            slot.weapon = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Weapon);
            slot.shield = GetRandomEquipmentOfSlot(allEquipment, EquipmentSlot.Shield);
        }

        EditorUtility.SetDirty(armyData);
        Debug.Log($"[DebugTools] {armyData.unlockedPawnCount} slota rastgele ekipman atandı.");
    }

    private EquipmentDataSO GetRandomEquipmentOfSlot(List<EquipmentDataSO> all, EquipmentSlot slot)
    {
        var filtered = all.Where(e => e.slot == slot).ToList();
        if (filtered.Count == 0) return null;
        return filtered[Random.Range(0, filtered.Count)];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Quick Time Test
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawQuickTimeTest()
    {
        _foldTime = DrawSectionHeader("⏱  Quick Time Test", _foldTime);
        if (!_foldTime) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Time.timeScale sadece Play Mode'da değiştirilebilir.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField($"Mevcut timeScale: {Time.timeScale:F2}x", EditorStyles.boldLabel);
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

        // Preset butonları
        EditorGUILayout.LabelField("Hızlı Seçim:", EditorStyles.miniLabel);
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

        if (GUILayout.Button("⏸ Duraklat (0x)", GUILayout.Height(28)))
        {
            _timeScale = 0f;
            if (Application.isPlaying) Time.timeScale = 0f;
            Debug.Log("[DebugTools] Oyun duraklatıldı (timeScale=0).");
        }

        if (GUILayout.Button("▶ Normal (1x)", GUILayout.Height(28)))
        {
            _timeScale = 1f;
            if (Application.isPlaying) Time.timeScale = 1f;
            Debug.Log("[DebugTools] timeScale normal hıza döndürüldü (1x).");
        }

        EditorGUILayout.EndHorizontal();

        // Fixed Timestep bilgisi
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("── Fizik Ayarları ──", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField($"Fixed Timestep: {Time.fixedDeltaTime * 1000:F1} ms  " +
                                   $"({1f / Time.fixedDeltaTime:F0} Hz)", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Max Allowed Timestep: {Time.maximumDeltaTime * 1000:F0} ms",
                                   EditorStyles.miniLabel);

        GUI.enabled = true;
        EditorGUILayout.EndVertical();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. Stress Testing
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawStressTesting()
    {
        _foldStress = DrawSectionHeader("💥  Stress Testing", _foldStress);
        if (!_foldStress) return;

        EditorGUILayout.BeginVertical(_sectionStyle);

        EditorGUILayout.HelpBox(
            "Play Mode'da çalışır. Mevcut level'ın düşman formasyonunu " +
            "çoğaltarak sahneye yoğun birim spawn eder.",
            MessageType.Info);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Stress Test için Play Mode'a girin.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        // Mevcut birim sayılarını göster
        if (BattleManager.Instance != null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"👤 Oyuncu Birimleri: {BattleManager.Instance.playerUnits.Count}",
                _statusOkStyle);
            EditorGUILayout.LabelField($"👹 Düşman Birimleri: {BattleManager.Instance.enemyUnits.Count}",
                _statusWarnStyle);
            EditorGUILayout.EndHorizontal();
        }

        _stressUnitCount = EditorGUILayout.IntSlider("Spawn Sayısı", _stressUnitCount, 1, 64);

        EditorGUILayout.BeginHorizontal();
        _stressBothTeams = EditorGUILayout.ToggleLeft("Her İki Takım", _stressBothTeams, GUILayout.Width(120));
        _stressEnemyOnly = EditorGUILayout.ToggleLeft("Sadece Düşman", _stressEnemyOnly, GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        // Mutual exclusion
        if (_stressBothTeams && _stressEnemyOnly) _stressEnemyOnly = false;

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("💥 Stress Spawn", GUILayout.Height(32)))
        {
            RunStressTest(_stressUnitCount, _stressBothTeams, _stressEnemyOnly);
        }

        if (GUILayout.Button("🗑 Tüm Birimleri Temizle", GUILayout.Height(32)))
        {
            ClearAllUnits();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("⚔ Savaşı Başlat", GUILayout.Height(28)))
        {
            if (BattleManager.Instance != null && !BattleManager.Instance.isBattleStarted)
            {
                BattleManager.Instance.StartBattle();
                Debug.Log("[DebugTools] Savaş başlatıldı.");
            }
            else
            {
                Debug.LogWarning("[DebugTools] BattleManager bulunamadı veya savaş zaten başladı.");
            }
        }

        if (GUILayout.Button("🔄 Level Sıfırla", GUILayout.Height(28)))
        {
            QuickReloadCurrentLevel();
        }

        EditorGUILayout.EndHorizontal();

        // FPS göstergesi
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
            Debug.LogWarning("[DebugTools] Stress Test sadece Play Mode'da çalışır.");
            return;
        }

        LevelManager lm = LevelManager.Instance;
        if (lm == null || lm.levels == null || lm.levels.Count == 0)
        {
            Debug.LogWarning("[DebugTools] LevelManager veya level listesi bulunamadı.");
            return;
        }

        // Mevcut level index'ini al
        var indexField = typeof(LevelManager).GetField("_currentLevelIndex",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int currentIdx = indexField != null ? (int)indexField.GetValue(lm) : 0;
        currentIdx = Mathf.Clamp(currentIdx, 0, lm.levels.Count - 1);

        LevelDataSO levelData = lm.levels[currentIdx];
        if (levelData == null)
        {
            Debug.LogWarning("[DebugTools] Mevcut level data null.");
            return;
        }

        // Düşman formasyonundan bir unit data al
        BaseUnitDataSO enemyData = null;
        if (levelData.enemyFormation != null && levelData.enemyFormation.units.Count > 0)
            enemyData = levelData.enemyFormation.units[0].unitData;

        // Oyuncu unit data
        BaseUnitDataSO playerData = levelData.meleeData;

        int spawnedCount = 0;

        // Rastgele pozisyonlarda spawn et
        for (int i = 0; i < count; i++)
        {
            // Rastgele grid pozisyonu
            int col = Random.Range(0, GridManager.Columns);
            int row = Random.Range(0, GridManager.Rows);

            GridNode node = GridManager.Instance?.GetNode(col, row);
            if (node == null || node.IsOccupied) continue;

            if (!enemyOnly && bothTeams && i % 2 == 0)
            {
                // Oyuncu birimi
                if (playerData != null && UnitFactory.Instance != null)
                {
                    UnitFactory.Instance.CreateUnit(playerData, node.WorldPosition, Team.Player);
                    node.IsOccupied = true;
                    spawnedCount++;
                }
            }
            else
            {
                // Düşman birimi
                if (enemyData != null && UnitFactory.Instance != null)
                {
                    UnitFactory.Instance.CreateUnit(enemyData, node.WorldPosition, Team.Enemy);
                    node.IsOccupied = true;
                    spawnedCount++;
                }
            }
        }

        Debug.Log($"[DebugTools] Stress Test: {spawnedCount} birim spawn edildi. " +
                  $"Toplam: P={BattleManager.Instance?.playerUnits.Count} " +
                  $"E={BattleManager.Instance?.enemyUnits.Count}");
    }

    private void ClearAllUnits()
    {
        if (!Application.isPlaying) return;

        BaseUnit[] allUnits = FindObjectsByType<BaseUnit>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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

        Debug.Log($"[DebugTools] {count} birim temizlendi.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

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

    // ─────────────────────────────────────────────────────────────────────────
    // Auto-repaint in Play Mode for live FPS display
    // ─────────────────────────────────────────────────────────────────────────

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying)
            Repaint();
    }
}
