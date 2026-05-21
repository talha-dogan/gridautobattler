using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class SaveActiveScene
{
    private const string AutoSaveOnPlayKey = "SaveActiveScene_AutoSaveOnPlay";
    private const string AutoSaveIntervalKey = "SaveActiveScene_AutoSaveInterval";
    private const string AutoSaveEnabledKey = "SaveActiveScene_AutoSaveEnabled";

    private static double _lastSaveTime;
    private static bool _isSaving;

    // ─── Defaults ───────────────────────────────────────────────────────────
    private static bool AutoSaveOnPlay
    {
        get => EditorPrefs.GetBool(AutoSaveOnPlayKey, true);
        set => EditorPrefs.SetBool(AutoSaveOnPlayKey, value);
    }

    private static bool AutoSaveEnabled
    {
        get => EditorPrefs.GetBool(AutoSaveEnabledKey, true);
        set => EditorPrefs.SetBool(AutoSaveEnabledKey, value);
    }

    /// <summary>Interval in minutes.</summary>
    private static float AutoSaveInterval
    {
        get => EditorPrefs.GetFloat(AutoSaveIntervalKey, 5f);
        set => EditorPrefs.SetFloat(AutoSaveIntervalKey, value);
    }

    // ─── Constructor (called on editor load via [InitializeOnLoad]) ──────────
    static SaveActiveScene()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.quitting += OnEditorQuitting;
        EditorApplication.update += OnEditorUpdate;
        _lastSaveTime = EditorApplication.timeSinceStartup;

        Debug.Log("[AutoSave] Initialized. Auto-save on play: " + AutoSaveOnPlay +
                  " | Interval save: " + AutoSaveEnabled +
                  " | Interval: " + AutoSaveInterval + " min");
    }

    // ─── Manual save (used by menu items & external callers) ────────────────
    public static void Execute()
    {
        SaveScenes("Manual");
    }

    // ─── Save helper ────────────────────────────────────────────────────────
    private static void SaveScenes(string reason)
    {
        if (_isSaving) return;
        _isSaving = true;

        bool saved = EditorSceneManager.SaveOpenScenes();
        _lastSaveTime = EditorApplication.timeSinceStartup;

        if (saved)
            Debug.Log($"[AutoSave] Scenes saved. Reason: {reason} | {System.DateTime.Now:HH:mm:ss}");
        else
            Debug.LogWarning("[AutoSave] Save attempted but nothing was saved (no dirty scenes?).");

        _isSaving = false;
    }

    // ─── Play mode hook ──────────────────────────────────────────────────────
    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode && AutoSaveOnPlay)
        {
            SaveScenes("Entering Play Mode");
        }
    }

    // ─── Editor quit hook ────────────────────────────────────────────────────
    private static void OnEditorQuitting()
    {
        SaveScenes("Editor Quitting");
    }

    // ─── Interval save ───────────────────────────────────────────────────────
    private static void OnEditorUpdate()
    {
        if (!AutoSaveEnabled) return;
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) return;

        double intervalSeconds = AutoSaveInterval * 60.0;
        if (EditorApplication.timeSinceStartup - _lastSaveTime >= intervalSeconds)
        {
            SaveScenes($"Auto-Interval ({AutoSaveInterval} min)");
        }
    }

    // ─── Menu Items ──────────────────────────────────────────────────────────

    [MenuItem("File/Save All Scenes %#s", priority = 170)]
    public static void MenuSaveAll()
    {
        SaveScenes("Menu: Save All Scenes");
    }

    [MenuItem("Tools/AutoSave/Save Now", priority = 0)]
    public static void MenuSaveNow()
    {
        SaveScenes("Menu: Save Now");
    }

    [MenuItem("Tools/AutoSave/Toggle Auto-Save on Play", priority = 10)]
    public static void ToggleAutoSaveOnPlay()
    {
        AutoSaveOnPlay = !AutoSaveOnPlay;
        Debug.Log("[AutoSave] Auto-save on play: " + (AutoSaveOnPlay ? "ENABLED" : "DISABLED"));
    }

    [MenuItem("Tools/AutoSave/Toggle Auto-Save on Play", true)]
    public static bool ToggleAutoSaveOnPlayValidate()
    {
        Menu.SetChecked("Tools/AutoSave/Toggle Auto-Save on Play", AutoSaveOnPlay);
        return true;
    }

    [MenuItem("Tools/AutoSave/Toggle Interval Auto-Save", priority = 11)]
    public static void ToggleIntervalAutoSave()
    {
        AutoSaveEnabled = !AutoSaveEnabled;
        Debug.Log("[AutoSave] Interval auto-save: " + (AutoSaveEnabled ? "ENABLED" : "DISABLED"));
    }

    [MenuItem("Tools/AutoSave/Toggle Interval Auto-Save", true)]
    public static bool ToggleIntervalAutoSaveValidate()
    {
        Menu.SetChecked("Tools/AutoSave/Toggle Interval Auto-Save", AutoSaveEnabled);
        return true;
    }

    [MenuItem("Tools/AutoSave/Set Interval: 1 min", priority = 20)]
    public static void SetInterval1() { AutoSaveInterval = 1f; Debug.Log("[AutoSave] Interval set to 1 min."); }

    [MenuItem("Tools/AutoSave/Set Interval: 3 min", priority = 21)]
    public static void SetInterval3() { AutoSaveInterval = 3f; Debug.Log("[AutoSave] Interval set to 3 min."); }

    [MenuItem("Tools/AutoSave/Set Interval: 5 min", priority = 22)]
    public static void SetInterval5() { AutoSaveInterval = 5f; Debug.Log("[AutoSave] Interval set to 5 min."); }

    [MenuItem("Tools/AutoSave/Set Interval: 10 min", priority = 23)]
    public static void SetInterval10() { AutoSaveInterval = 10f; Debug.Log("[AutoSave] Interval set to 10 min."); }

    [MenuItem("Tools/AutoSave/Set Interval: 15 min", priority = 24)]
    public static void SetInterval15() { AutoSaveInterval = 15f; Debug.Log("[AutoSave] Interval set to 15 min."); }
}
