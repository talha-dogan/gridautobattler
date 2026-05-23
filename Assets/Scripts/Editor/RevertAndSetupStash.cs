using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 1. UpgradeManager _startingInventory temizle
/// 2. ItemHolder'dan GridLayoutGroup kaldir
/// 3. 6 adet ItemHolder child slot'u yeniden olustur
/// 4. Her birine StashDropZoneUI ekle
/// </summary>
public class RevertAndSetupStash
{
    [MenuItem("TDEV/Revert and Setup Stash Slots")]
    public static void Execute()
    {
        bool dirty = false;

        // ── 1. UpgradeManager _startingInventory temizle ─────────────────────
        UpgradeManager upgradeManager = Object.FindAnyObjectByType<UpgradeManager>();
        if (upgradeManager != null)
        {
            SerializedObject soUM = new SerializedObject(upgradeManager);
            SerializedProperty inventoryProp = soUM.FindProperty("_startingInventory");
            inventoryProp.ClearArray();
            soUM.ApplyModifiedProperties();
            dirty = true;
            Debug.Log("[RevertAndSetupStash] _startingInventory temizlendi.");
        }
        else
        {
            Debug.LogWarning("[RevertAndSetupStash] UpgradeManager bulunamadi.");
        }

        // ── 2. ItemHolder parent'ini bul ──────────────────────────────────────
        Transform itemHolderParent = FindTransformByPath("--- UI CANVAS ---/Canvas/ItemsPanel/ItemHolder");
        if (itemHolderParent == null)
        {
            Debug.LogError("[RevertAndSetupStash] 'Canvas/ItemsPanel/ItemHolder' bulunamadi!");
            return;
        }

        // ── 3. GridLayoutGroup kaldir ─────────────────────────────────────────
        GridLayoutGroup grid = itemHolderParent.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            Undo.DestroyObjectImmediate(grid);
            dirty = true;
            Debug.Log("[RevertAndSetupStash] GridLayoutGroup kaldirildi.");
        }
        else
        {
            Debug.Log("[RevertAndSetupStash] GridLayoutGroup zaten yok, atlanıyor.");
        }

        // ── 4. Mevcut child'lari temizle (onceki run'dan kalma olabilir) ──────
        for (int i = itemHolderParent.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(itemHolderParent.GetChild(i).gameObject);
        }

        // ── 5. 6 adet ItemHolder slot olustur ─────────────────────────────────
        // Orijinal slot boyutu: 160x160, ItemHolder parent: 777x501
        // 3 sutun x 2 satir, esit aralikli pozisyonlar
        Vector2 cellSize = new Vector2(160f, 160f);
        float spacingX = (777f - 3f * cellSize.x) / 4f;   // ~98.5
        float spacingY = (501f - 2f * cellSize.y) / 3f;   // ~60.3

        int index = 0;
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                string slotName = index == 0 ? "ItemHolder" : $"ItemHolder ({index})";

                GameObject slot = new GameObject(slotName);
                Undo.RegisterCreatedObjectUndo(slot, "Create ItemHolder Slot");
                slot.transform.SetParent(itemHolderParent, false);
                slot.layer = 5; // UI

                // RectTransform
                RectTransform rt = slot.AddComponent<RectTransform>();
                rt.sizeDelta = cellSize;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                // Pozisyon: sol ust koseden baslayarak hesapla
                float startX = -(777f / 2f) + spacingX + cellSize.x / 2f;
                float startY = (501f / 2f) - spacingY - cellSize.y / 2f;
                float posX = startX + col * (cellSize.x + spacingX);
                float posY = startY - row * (cellSize.y + spacingY);
                rt.anchoredPosition = new Vector2(posX, posY);

                // CanvasRenderer
                slot.AddComponent<CanvasRenderer>();

                // Image (slot arkaplan)
                Image img = slot.AddComponent<Image>();
                img.color = new Color(1f, 1f, 1f, 1f);
                img.raycastTarget = true;

                // StashDropZoneUI
                slot.AddComponent<StashDropZoneUI>();

                index++;
                dirty = true;
            }
        }

        Debug.Log("[RevertAndSetupStash] 6 adet ItemHolder slot olusturuldu ve StashDropZoneUI eklendi.");

        // ── 6. Sahneyi kaydet ─────────────────────────────────────────────────
        if (dirty)
        {
            EditorSceneManager.MarkSceneDirty(itemHolderParent.gameObject.scene);
            EditorSceneManager.SaveScene(itemHolderParent.gameObject.scene);
            Debug.Log("[RevertAndSetupStash] Sahne kaydedildi.");
        }
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
