using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utility: UpgradeManager'ın _inventoryContent ve _startingInventory
/// alanlarını ItemHolder parent'ına ve 6 item data'sına bağlar.
/// </summary>
public class SetupUpgradeInventory
{
    [MenuItem("TDEV/Setup Upgrade Inventory")]
    public static void Execute()
    {
        // UpgradeManager'ı bul
        UpgradeManager upgradeManager = Object.FindAnyObjectByType<UpgradeManager>();
        if (upgradeManager == null)
        {
            Debug.LogError("[SetupUpgradeInventory] UpgradeManager sahnede bulunamadı!");
            return;
        }

        SerializedObject so = new SerializedObject(upgradeManager);

        // _inventoryContent → Canvas/ItemsPanel/ItemHolder (parent container)
        // Tam yolu ile bulmak için tüm Transform'ları tara
        Transform itemHolderParent = FindTransformByPath("--- UI CANVAS ---/Canvas/ItemsPanel/ItemHolder");
        if (itemHolderParent == null)
        {
            Debug.LogError("[SetupUpgradeInventory] 'Canvas/ItemsPanel/ItemHolder' parent objesi sahnede bulunamadı!");
            return;
        }

        SerializedProperty contentProp = so.FindProperty("_inventoryContent");
        contentProp.objectReferenceValue = itemHolderParent;

        // _startingInventory → 6 item data asset'i
        string[] assetPaths = new string[]
        {
            "Assets/Scripts/Data/Equipment/Sword_Weapon.asset",
            "Assets/Scripts/Data/Equipment/Halmet.asset",
            "Assets/Scripts/Data/Equipment/Vest.asset",
            "Assets/Scripts/Data/Equipment/Pants.asset",
            "Assets/Scripts/Data/Equipment/Shield_Weapon 1.asset",
            "Assets/Scripts/Data/Equipment/Gun_Weapon.asset",
        };

        SerializedProperty inventoryProp = so.FindProperty("_startingInventory");
        inventoryProp.ClearArray();
        inventoryProp.arraySize = assetPaths.Length;

        for (int i = 0; i < assetPaths.Length; i++)
        {
            EquipmentDataSO data = AssetDatabase.LoadAssetAtPath<EquipmentDataSO>(assetPaths[i]);
            if (data == null)
            {
                Debug.LogWarning($"[SetupUpgradeInventory] Asset yüklenemedi: {assetPaths[i]}");
                continue;
            }
            inventoryProp.GetArrayElementAtIndex(i).objectReferenceValue = data;
        }

        so.ApplyModifiedProperties();

        // Sahneyi kaydet
        EditorSceneManager.MarkSceneDirty(upgradeManager.gameObject.scene);
        EditorSceneManager.SaveScene(upgradeManager.gameObject.scene);

        Debug.Log($"[SetupUpgradeInventory] UpgradeManager güncellendi. _inventoryContent = '{itemHolderParent.name}' (path: {GetFullPath(itemHolderParent)}). Sahne kaydedildi.");
    }

    private static Transform FindTransformByPath(string hierarchyPath)
    {
        // Hiyerarşi yolunu '/' ile böl ve kök objeyi bul
        string[] parts = hierarchyPath.Split('/');
        if (parts.Length == 0) return null;

        // Kök objeyi bul (sahne kökündeki obje)
        GameObject root = null;
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject go in allObjects)
      {
         if (go.transform.parent == null && go.name == parts[0])
         {
        root = go;
        break;
         }
      }

        if (root == null) return null;

        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            Transform found = null;
            for (int c = 0; c < current.childCount; c++)
            {
                if (current.GetChild(c).name == parts[i])
                {
                    found = current.GetChild(c);
                    break;
                }
            }
            if (found == null) return null;
            current = found;
        }
        return current;
    }

    private static string GetFullPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
