using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor utility: UpgradeScene'e PawnShopManager ve PawnShopUI bileşenlerini
/// ekler, tüm referansları bağlar ve ShowcasePawn + HolderSlot görünürlüklerini ayarlar.
/// </summary>
public static class PawnShopSetup
{
    [MenuItem("TDEV/Setup/Setup Pawn Shop in UpgradeScene")]
    public static void Run()
    {
        // ── 1. UpgradeManager objesini bul ───────────────────────────────────
        UpgradeManager upgradeManager = Object.FindAnyObjectByType<UpgradeManager>();
        if (upgradeManager == null)
        {
            Debug.LogError("[PawnShopSetup] UpgradeManager bulunamadı. UpgradeScene açık mı?");
            return;
        }
        GameObject managerGO = upgradeManager.gameObject;

        // ── 2. PlayerArmyDataSO'yu yükle ─────────────────────────────────────
        PlayerArmyDataSO armyData = AssetDatabase.LoadAssetAtPath<PlayerArmyDataSO>(
            "Assets/GameData/PlayerArmyData.asset");
        if (armyData == null)
        {
            Debug.LogError("[PawnShopSetup] PlayerArmyData.asset bulunamadı.");
            return;
        }

        // ── 3. PawnShopManager ekle / bul ─────────────────────────────────────
        PawnShopManager shopManager = managerGO.GetComponent<PawnShopManager>();
        if (shopManager == null)
            shopManager = managerGO.AddComponent<PawnShopManager>();

        // ── 4. PawnShopUI ekle / bul ──────────────────────────────────────────
        PawnShopUI shopUI = managerGO.GetComponent<PawnShopUI>();
        if (shopUI == null)
            shopUI = managerGO.AddComponent<PawnShopUI>();

        // ── 5. ShowcasePawn listesini topla (sıralı 0-7) ─────────────────────
        GameObject showcaseParent = GameObject.Find("--- SHOWCASE PAWNS ---");
        if (showcaseParent == null)
        {
            Debug.LogError("[PawnShopSetup] '--- SHOWCASE PAWNS ---' objesi bulunamadı.");
            return;
        }

        // Sahnedeki isimlerle birebir eşleşen sıralı liste
        string[] pawnNames = new string[]
        {
            "ShowCasePawn",
            "ShowCasePawn1",
            "ShowCasePawn2 ",   // trailing space — sahnedeki isimle eşleşmeli
            "ShowCasePawn3",
            "ShowCasePawn4",
            "ShowCasePawn5",
            "ShowCasePawn6",
            "ShowCasePawn7"
        };

        List<GameObject> pawns = new List<GameObject>();
        foreach (string pawnName in pawnNames)
        {
            Transform t = showcaseParent.transform.Find(pawnName);
            if (t != null)
                pawns.Add(t.gameObject);
            else
                Debug.LogWarning($"[PawnShopSetup] Pawn bulunamadı: '{pawnName}'");
        }

        // ── 6. HolderSlot listesini topla (sıralı 0-7) ───────────────────────
        // Slot isimleri ve path'leri — UpgradeCharacterDropZoneUI._armySlotIndex ile eşleşir
        // Left panel: index 0,1,2,3 | Right panel: index 4,5,6,7
        string[] slotPaths = new string[]
        {
            "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/ShowCasePawnHolderSlot",
            "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/ShowCasePawnHolderSlot 1",
            "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/ShowCasePawnHolderSlot 2",
            "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/ShowCasePawnHolderSlot 3",
            "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/ShowCasePawnHolderSlot 4",
            "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/ShowCasePawnHolderSlot 5",
            "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/ShowCasePawnHolderSlot 6",
            "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/ShowCasePawnHolderSlot 7"
        };

        List<GameObject> slots = new List<GameObject>();
        foreach (string slotPath in slotPaths)
        {
            // Path'i parçalara bölerek hiyerarşide bul
            string[] parts = slotPath.Split('/');
            GameObject current = GameObject.Find(parts[0]);
            for (int i = 1; i < parts.Length && current != null; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                current = child != null ? child.gameObject : null;
            }

            if (current != null)
                slots.Add(current);
            else
                Debug.LogWarning($"[PawnShopSetup] HolderSlot bulunamadı: '{slotPath}'");
        }

        // ── 7. PawnShopManager'a referansları ata ────────────────────────────
        SerializedObject soManager = new SerializedObject(shopManager);
        soManager.FindProperty("_armyData").objectReferenceValue = armyData;

        SerializedProperty pawnsListProp = soManager.FindProperty("_showcasePawns");
        pawnsListProp.ClearArray();
        for (int i = 0; i < pawns.Count; i++)
        {
            pawnsListProp.InsertArrayElementAtIndex(i);
            pawnsListProp.GetArrayElementAtIndex(i).objectReferenceValue = pawns[i];
        }

        SerializedProperty slotsListProp = soManager.FindProperty("_holderSlots");
        slotsListProp.ClearArray();
        for (int i = 0; i < slots.Count; i++)
        {
            slotsListProp.InsertArrayElementAtIndex(i);
            slotsListProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
        }

        soManager.ApplyModifiedProperties();

        // ── 8. PawnShopUI'a referansları ata ─────────────────────────────────
        GameObject coinTextGO = GameObject.Find("CoinText");
        TextMeshProUGUI coinTMP = coinTextGO != null
            ? coinTextGO.GetComponent<TextMeshProUGUI>()
            : null;

        GameObject buyBtnGO = GameObject.Find("BuyPawnButton");
        Button buyBtn = buyBtnGO != null
            ? buyBtnGO.GetComponent<Button>()
            : null;

        SerializedObject soUI = new SerializedObject(shopUI);
        soUI.FindProperty("_coinText").objectReferenceValue     = coinTMP;
        soUI.FindProperty("_buyPawnButton").objectReferenceValue = buyBtn;
        soUI.FindProperty("_armyData").objectReferenceValue     = armyData;
        soUI.ApplyModifiedProperties();

        // ── 9. BuyPawnButton OnClick → PawnShopManager.TryBuyPawn() ──────────
        if (buyBtn != null)
        {
            SerializedObject soBtn = new SerializedObject(buyBtn);
            SerializedProperty onClickProp = soBtn.FindProperty(
                "m_OnClick.m_PersistentCalls.m_Calls");

            onClickProp.ClearArray();
            onClickProp.InsertArrayElementAtIndex(0);
            SerializedProperty call = onClickProp.GetArrayElementAtIndex(0);

            call.FindPropertyRelative("m_Target").objectReferenceValue = shopManager;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                typeof(PawnShopManager).AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = "TryBuyPawn";
            call.FindPropertyRelative("m_Mode").intValue  = 1; // Void / no args
            call.FindPropertyRelative("m_CallState").intValue = 2; // RuntimeOnly

            soBtn.ApplyModifiedProperties();
        }
        else
        {
            Debug.LogWarning("[PawnShopSetup] BuyPawnButton bulunamadı — OnClick bağlanamadı.");
        }

        // ── 10. unlockedPawnCount = 1, görünürlükleri uygula ─────────────────
        armyData.unlockedPawnCount = 1;
        EditorUtility.SetDirty(armyData);

        for (int i = 0; i < pawns.Count; i++)
        {
            bool active = i < armyData.unlockedPawnCount;
            if (pawns[i].activeSelf != active)
            {
                Undo.RecordObject(pawns[i], "Set Pawn Active State");
                pawns[i].SetActive(active);
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            bool active = i < armyData.unlockedPawnCount;
            if (slots[i].activeSelf != active)
            {
                Undo.RecordObject(slots[i], "Set Slot Active State");
                slots[i].SetActive(active);
            }
        }

        // ── 11. Sahneyi kirli işaretle ────────────────────────────────────────
        EditorUtility.SetDirty(managerGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(managerGO.scene);

        Debug.Log($"[PawnShopSetup] Tamamlandı! " +
                  $"{pawns.Count} pawn + {slots.Count} slot bağlandı. " +
                  $"Başlangıç: 1/{PawnShopManager.MaxPawns}. " +
                  $"Sahneyi kaydedin (Ctrl+S).");
    }
}
