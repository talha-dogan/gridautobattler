using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// GameBootstrap'ı GridScene'den CoreScene'e taşır.
/// Tek seferlik düzeltme scripti.
/// </summary>
public class FixGameBootstrapPlacement
{
    [MenuItem("Tools/TDEV/Fix GameBootstrap Placement")]
    public static void Execute()
    {
        // 1. CoreScene'i aç
        string coreScenePath = "Assets/Scenes/CoreScene.unity";
        var coreScene = EditorSceneManager.OpenScene(coreScenePath, OpenSceneMode.Single);

        // CoreScene'de zaten GameBootstrap var mı kontrol et
        bool alreadyExists = false;
        foreach (var go in coreScene.GetRootGameObjects())
        {
            if (go.GetComponent<GameBootstrap>() != null)
            {
                alreadyExists = true;
                Debug.Log("[FixGameBootstrap] CoreScene'de zaten GameBootstrap var.");
                break;
            }
        }

        if (!alreadyExists)
        {
            // CoreScene'e GameBootstrap ekle
            var bootstrapGO = new GameObject("[GameBootstrap]");
            bootstrapGO.AddComponent<GameBootstrap>();
            SceneManager.MoveGameObjectToScene(bootstrapGO, coreScene);
            Debug.Log("[FixGameBootstrap] CoreScene'e [GameBootstrap] eklendi.");
        }

        EditorSceneManager.SaveScene(coreScene);
        Debug.Log("[FixGameBootstrap] CoreScene kaydedildi.");

        // 2. GridScene'i aç ve GameBootstrap'ı kaldır
        string gridScenePath = "Assets/Scenes/GridScene.unity";
        var gridScene = EditorSceneManager.OpenScene(gridScenePath, OpenSceneMode.Single);

        int removedCount = 0;
        foreach (var go in gridScene.GetRootGameObjects())
        {
            if (go.name == "GameBootstrap" || go.GetComponent<GameBootstrap>() != null)
            {
                Object.DestroyImmediate(go);
                removedCount++;
                Debug.Log("[FixGameBootstrap] GridScene'den GameBootstrap kaldırıldı.");
            }
        }

        if (removedCount > 0)
        {
            EditorSceneManager.SaveScene(gridScene);
            Debug.Log("[FixGameBootstrap] GridScene kaydedildi.");
        }
        else
        {
            Debug.Log("[FixGameBootstrap] GridScene'de GameBootstrap bulunamadı (zaten temiz).");
        }

        // 3. CoreScene'i tekrar aç (son durum)
        EditorSceneManager.OpenScene(coreScenePath, OpenSceneMode.Single);
        Debug.Log("[FixGameBootstrap] İşlem tamamlandı. CoreScene aktif.");
    }
}
