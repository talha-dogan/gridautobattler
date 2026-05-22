using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// ItemHolder GridLayoutGroup'u 3x2 grid için yapılandırır.
/// </summary>
public class ConfigureGridLayout
{
    [MenuItem("TDEV/Configure ItemHolder GridLayout")]
    public static void Execute()
    {
        Transform itemHolderParent = FindTransformByPath("--- UI CANVAS ---/Canvas/ItemsPanel/ItemHolder");
        if (itemHolderParent == null)
        {
            Debug.LogError("[ConfigureGridLayout] ItemHolder bulunamadı!");
            return;
        }

        GridLayoutGroup grid = itemHolderParent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            Debug.LogError("[ConfigureGridLayout] GridLayoutGroup bileşeni bulunamadı!");
            return;
        }

        SerializedObject so = new SerializedObject(grid);

        // Padding: 10px her taraftan
        so.FindProperty("m_Padding.m_Left").intValue = 10;
        so.FindProperty("m_Padding.m_Right").intValue = 10;
        so.FindProperty("m_Padding.m_Top").intValue = 10;
        so.FindProperty("m_Padding.m_Bottom").intValue = 10;

        // Cell size: 240x230 (3 sütun x 2 satır, 777x501 içinde)
        so.FindProperty("m_CellSize.x").floatValue = 240f;
        so.FindProperty("m_CellSize.y").floatValue = 230f;

        // Spacing: 8x8
        so.FindProperty("m_Spacing.x").floatValue = 8f;
        so.FindProperty("m_Spacing.y").floatValue = 8f;

        // Constraint: Fixed Column Count = 3
        so.FindProperty("m_Constraint").intValue = 1; // FixedColumnCount
        so.FindProperty("m_ConstraintCount").intValue = 3;

        // Alignment: Middle Center
        so.FindProperty("m_ChildAlignment").intValue = 4; // MiddleCenter

        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(itemHolderParent.gameObject.scene);
        EditorSceneManager.SaveScene(itemHolderParent.gameObject.scene);

        Debug.Log("[ConfigureGridLayout] GridLayoutGroup 3x2 olarak yapılandırıldı. Sahne kaydedildi.");
    }

    private static Transform FindTransformByPath(string hierarchyPath)
    {
        string[] parts = hierarchyPath.Split('/');
        if (parts.Length == 0) return null;

        GameObject root = null;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
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
}
