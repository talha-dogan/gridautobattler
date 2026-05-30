using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor yardımcısı — CoreScene'i otomatik oluşturur ve Build Settings'i yapılandırır.
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
            "Bu araç additive sahne mimarisini kurar:\n\n" +
            "• CoreScene — DontDestroyOnLoad singleton'ları (SceneLoader, GameBootstrap, vb.)\n" +
            "• UpgradeScene — Additive yüklenir/unload edilir\n" +
            "• GridScene — Additive yüklenir/unload edilir\n\n" +
            "CoreScene Build Settings'te İLK sırada olmalıdır.",
            MessageType.Info);

        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Adım 1: CoreScene Oluştur", EditorStyles.boldLabel);
        if (GUILayout.Button("📁 CoreScene Oluştur ve Kaydet", GUILayout.Height(32)))
        {
            CreateCoreScene();
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Adım 2: Build Settings'i Yapılandır", EditorStyles.boldLabel);
        if (GUILayout.Button("⚙ Build Settings'e Ekle (CoreScene İlk Sıraya)", GUILayout.Height(32)))
        {
            ConfigureBuildSettings();
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Adım 3: Mevcut Durumu Kontrol Et", EditorStyles.boldLabel);
        if (GUILayout.Button("🔍 Build Settings'i Göster", GUILayout.Height(28)))
        {
            EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
        }

        EditorGUILayout.Space(8);
        DrawBuildSettingsStatus();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Mimari Notları", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "DontDestroyOnLoad Singleton'lar (CoreScene'de yaşar):\n" +
            "  ✅ GameBootstrap\n" +
            "  ✅ GameInputHandler\n" +
            "  ✅ SoundManager\n" +
            "  ✅ SceneLoader (yeni)\n" +
            "  ✅ CoreSceneBootstrapper (yeni)\n\n" +
            "Sahneye Özgü Manager'lar (additive sahnelerde yaşar):\n" +
            "  📦 UpgradeScene: UpgradeManager, PawnShopManager, LootBoxManager\n" +
            "  📦 GridScene: BattleManager, LevelManager, UnitFactory, UnitSpawner,\n" +
            "                GridManager, VFXManager, GameUIManager",
            MessageType.None);
    }

    private void DrawBuildSettingsStatus()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        EditorGUILayout.LabelField("Mevcut Build Settings:", EditorStyles.boldLabel);

        if (scenes.Length == 0)
        {
            EditorGUILayout.HelpBox("Build Settings boş.", MessageType.Warning);
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
        // CoreScene'in kaydedileceği yol
        const string coreSavePath = "Assets/Scenes/CoreScene.unity";

        // Zaten varsa sor
        if (System.IO.File.Exists(coreSavePath))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "CoreScene Zaten Var",
                $"'{coreSavePath}' zaten mevcut. Üzerine yazılsın mı?",
                "Evet, Üzerine Yaz",
                "İptal");

            if (!overwrite) return;
        }

        // Yeni boş sahne oluştur
        UnityEngine.SceneManagement.Scene coreScene =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // CoreSceneBootstrapper GameObject'i oluştur
        GameObject bootstrapperGO = new GameObject("[CoreBootstrapper]");
        EditorSceneManager.MoveGameObjectToScene(bootstrapperGO, coreScene);
        bootstrapperGO.AddComponent<CoreSceneBootstrapper>();

        // SceneLoader GameObject'i oluştur
        GameObject sceneLoaderGO = new GameObject("[SceneLoader]");
        EditorSceneManager.MoveGameObjectToScene(sceneLoaderGO, coreScene);
        sceneLoaderGO.AddComponent<SceneLoader>();

        // Sahneyi kaydet
        bool saved = EditorSceneManager.SaveScene(coreScene, coreSavePath);

        if (saved)
        {
            Debug.Log($"[CoreSceneSetup] CoreScene oluşturuldu: '{coreSavePath}'");
            EditorUtility.DisplayDialog(
                "CoreScene Oluşturuldu",
                $"CoreScene başarıyla oluşturuldu:\n{coreSavePath}\n\n" +
                "Şimdi 'Build Settings'e Ekle' butonuna tıklayın.",
                "Tamam");
        }
        else
        {
            Debug.LogError("[CoreSceneSetup] CoreScene kaydedilemedi!");
        }

        // Additive olarak açılan sahneyi kapat
        EditorSceneManager.CloseScene(coreScene, false);
        AssetDatabase.Refresh();
    }

    private static void ConfigureBuildSettings()
    {
        const string corePath    = "Assets/Scenes/CoreScene.unity";
        const string startPath   = "Assets/Scenes/StartScene.unity";
        const string upgradePath = "Assets/Scenes/UpgradeScene.unity";
        const string gridPath    = "Assets/Scenes/GridScene.unity";

        // CoreScene yoksa uyar
        if (!System.IO.File.Exists(corePath))
        {
            EditorUtility.DisplayDialog(
                "CoreScene Bulunamadı",
                $"'{corePath}' bulunamadı.\nÖnce 'CoreScene Oluştur' butonuna tıklayın.",
                "Tamam");
            return;
        }

        // Mevcut sahneleri al
        var existingScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
            EditorBuildSettings.scenes);

        // Eklenecek sahneler (sırayla)
        string[] desiredOrder = { corePath, startPath, upgradePath, gridPath };

        var newScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // Önce istenen sırayı ekle
        foreach (string path in desiredOrder)
        {
            if (System.IO.File.Exists(path))
            {
                newScenes.Add(new EditorBuildSettingsScene(path, true));
            }
            else
            {
                Debug.LogWarning($"[CoreSceneSetup] Sahne bulunamadı, atlandı: '{path}'");
            }
        }

        // Mevcut sahnelerden listede olmayanları sona ekle
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

        Debug.Log("[CoreSceneSetup] Build Settings güncellendi. CoreScene ilk sıraya alındı.");
        EditorUtility.DisplayDialog(
            "Build Settings Güncellendi",
            "Sahne sırası:\n" +
            "  0: CoreScene (ilk yüklenen)\n" +
            "  1: StartScene\n" +
            "  2: UpgradeScene\n" +
            "  3: GridScene\n\n" +
            "CoreSceneBootstrapper'ın 'Initial Scene' alanını 'StartScene' veya\n" +
            "'UpgradeScene' olarak ayarlamayı unutmayın.",
            "Tamam");
    }
}
