using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor helper — Automatically creates CoreScene and configures Build Settings.
///
/// Menu: Tools -> TDEV -> Setup Core Scene
/// </summary>
public class CoreSceneSetup : EditorWindow
{
    [MenuItem("Tools/TDEV/Setup Core Scene")]
    public static void OpenWindow()
    {
        CoreSceneSetup window = GetWindow<CoreSceneSetup>("Core Scene Setup");
        window.minSize = new Vector2(420f, 380f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Core Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool sets up the additive scene architecture:\n\n" +
            "• CoreScene — DontDestroyOnLoad singletons (SceneLoader, GameBootstrap, etc.)\n" +
            "• UpgradeScene — Loaded/unloaded additively\n" +
            "• GridScene — Loaded/unloaded additively\n\n" +
            "CoreScene MUST be the FIRST in Build Settings.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Step 1: Create CoreScene", EditorStyles.boldLabel);
        if (GUILayout.Button("📁 Create and Save CoreScene", GUILayout.Height(32)))
        {
            CreateCoreScene();
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 2: Configure Build Settings", EditorStyles.boldLabel);
        if (GUILayout.Button("⚙ Add to Build Settings (CoreScene First)", GUILayout.Height(32)))
        {
            ConfigureBuildSettings();
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Step 3: Check Current Status", EditorStyles.boldLabel);
        if (GUILayout.Button("🔍 Show Build Settings", GUILayout.Height(28)))
        {
            EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
        }

        EditorGUILayout.Space(8);
        DrawBuildSettingsStatus();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Architecture Notes", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "DontDestroyOnLoad Singletons (Lives in CoreScene):\n" +
            "  ✅ GameBootstrap\n" +
            "  ✅ GameInputHandler\n" +
            "  ✅ SoundManager\n" +
            "  ✅ SceneLoader (new)\n" +
            "  ✅ CoreSceneBootstrapper (new)\n\n" +
            "Scene-Specific Managers (Lives in additive scenes):\n" +
            "  📦 UpgradeScene: UpgradeManager, PawnShopManager, LootBoxManager\n" +
            "  📦 GridScene: BattleManager, LevelManager, UnitFactory, UnitSpawner,\n" +
            "                 GridManager, VFXManager, GameUIManager",
            MessageType.None);
    }

    private void DrawBuildSettingsStatus()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        EditorGUILayout.LabelField("Current Build Settings:", EditorStyles.boldLabel);

        if (scenes.Length == 0)
        {
            EditorGUILayout.HelpBox("Build Settings is empty.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < scenes.Length; i++)
        {
            string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenes[i].path);
            string icon = i == 0 ? "🏠" : "📄";
            string status = scenes[i].enabled ? "✅" : "❌";
            EditorGUILayout.LabelField($"  {i}: {icon} {status} {sceneName}", EditorStyles.miniLabel);
        }
    }

    private static void CreateCoreScene()
    {
        // The path where CoreScene will be saved
        const string coreSavePath = "Assets/Scenes/CoreScene.unity";

        // Ask if it already exists
        if (System.IO.File.Exists(coreSavePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "CoreScene Already Exists",
                $"'{coreSavePath}' already exists. Overwrite?",
                "Yes, Overwrite",
                "Cancel");

            if (!overwrite) return;
        }

        // Create a new empty scene
        UnityEngine.SceneManagement.Scene coreScene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // Create CoreSceneBootstrapper GameObject
        GameObject bootstrapperGO = new GameObject("[CoreBootstrapper]");
        EditorSceneManager.MoveGameObjectToScene(bootstrapperGO, coreScene);
        bootstrapperGO.AddComponent<CoreSceneBootstrapper>();

        // Create SceneLoader GameObject
        GameObject sceneLoaderGO = new GameObject("[SceneLoader]");
        EditorSceneManager.MoveGameObjectToScene(sceneLoaderGO, coreScene);
        sceneLoaderGO.AddComponent<SceneLoader>();

        // Save the scene
        bool saved = EditorSceneManager.SaveScene(coreScene, coreSavePath);

        if (saved)
        {
            Debug.Log($"[CoreSceneSetup] CoreScene created: '{coreSavePath}'");
            EditorUtility.DisplayDialog(
                "CoreScene Created",
                $"CoreScene successfully created:\n{coreSavePath}\n\n" +
                "Now click the 'Add to Build Settings' button.",
                "OK");
        }
        else
        {
            Debug.LogError("[CoreSceneSetup] Failed to save CoreScene!");
        }

        // Close the scene opened additively
        EditorSceneManager.CloseScene(coreScene, false);
        AssetDatabase.Refresh();
    }

    private static void ConfigureBuildSettings()
    {
        const string corePath    = "Assets/Scenes/CoreScene.unity";
        const string startPath   = "Assets/Scenes/StartScene.unity";
        const string upgradePath = "Assets/Scenes/UpgradeScene.unity";
        const string gridPath    = "Assets/Scenes/GridScene.unity";

        // Warn if CoreScene does not exist
        if (!System.IO.File.Exists(corePath))
        {
            EditorUtility.DisplayDialog(
                "CoreScene Not Found",
                $"'{corePath}' not found.\nClick the 'Create CoreScene' button first.",
                "OK");
            return;
        }

        // Get existing scenes
        var existingScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        // Scenes to add (in order)
        string[] desiredOrder = { corePath, startPath, upgradePath, gridPath };

        var newScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // Add the desired order first
        foreach (string path in desiredOrder)
        {
            if (System.IO.File.Exists(path))
            {
                newScenes.Add(new EditorBuildSettingsScene(path, true));
            }
            else
            {
                Debug.LogWarning($"[CoreSceneSetup] Scene not found, skipped: '{path}'");
            }
        }

        // Append existing scenes that are not in the list to the end
        foreach (var existing in existingScenes)
        {
            bool alreadyAdded = false;
            foreach (string path in desiredOrder)
            {
                if (existing.path == path) { alreadyAdded = true; break; }
            }
            if (!alreadyAdded)
                newScenes.Add(existing);
        }

        EditorBuildSettings.scenes = newScenes.ToArray();

        Debug.Log("[CoreSceneSetup] Build Settings updated. CoreScene is set to the first position.");
        EditorUtility.DisplayDialog(
            "Build Settings Updated",
            "Scene order:\n" +
            "  0: CoreScene (first loaded)\n" +
            "  1: StartScene\n" +
            "  2: UpgradeScene\n" +
            "  3: GridScene\n\n" +
            "Do not forget to set the 'Initial Scene' field of CoreSceneBootstrapper to 'StartScene' or\n" +
            "'UpgradeScene'.",
            "OK");
    }
}