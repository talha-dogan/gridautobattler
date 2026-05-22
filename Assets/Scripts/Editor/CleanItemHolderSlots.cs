using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// ItemHolder parent'ının altındaki boş placeholder child objelerini siler.
/// UpgradeManager runtime'da kendi item prefab'larını spawn edecek.
/// </summary>
public class CleanItemHolderSlots
{
    [MenuItem("TDEV/Clean ItemHolder Placeholder Slots")]
    public static void Execute()
    {
        // ItemHolder parent'ını bul
        Transform itemHolderParent = FindTransformByPath("--- UI CANVAS ---/Canvas/ItemsPanel/ItemHolder");
        if (itemHolderParent == null)
        {
            Debug.LogError("[CleanItemHolderSlots] 'Canvas/ItemsPanel/ItemHolder' bulunamadı!");
            return;
        }

        int removed = 0;
        // Tüm child'ları topla (reverse order ile sil)
        for (int i = itemHolderParent.childCount - 1; i >= 0; i--)
        {
            Transform child = itemHolderParent.GetChild(i);
            // Sadece UpgradeDragItemUI veya Image bileşeni olmayan boş slot'ları sil
            // (yani sadece Image + RectTransform olan placeholder'ları)
            UpgradeDragItemUI dragItem = child.GetComponent<UpgradeDragItemUI>();
            if (dragItem == null)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
                removed++;
            }
        }

        EditorSceneManager.MarkSceneDirty(itemHolderParent.gameObject.scene);
        EditorSceneManager.SaveScene(itemHolderParent.gameObject.scene);

        Debug.Log($"[CleanItemHolderSlots] {removed} adet boş placeholder slot silindi. Sahne kaydedildi.");
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
