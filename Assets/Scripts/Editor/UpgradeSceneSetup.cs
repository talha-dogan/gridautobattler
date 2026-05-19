using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// One-shot editor utility that wires up the Upgrade Scene's ShowcasePawn system.
///
/// What it does:
///   1. Deletes the old Player_Pawn (0-3) objects (they carry combat components
///      that do not belong in the Upgrade Scene).
///   2. Reads the world-space centre of each UI Player drop zone (0-7).
///   3. Instantiates a ShowcasePawn prefab at each position.
///   4. Adds ShowcasePawnController to each instance for idle breathing.
///   5. Links the ShowcasePawn's CharacterEquipmentVisuals to the matching
///      UpgradeCharacterDropZoneUI so drops update the SpriteRenderer layers.
///   6. Makes the UI Player Image fully transparent (alpha = 0) so only the
///      world-space pawn is visible, while the RectTransform still acts as
///      the drop target.
/// </summary>
public class UpgradeSceneSetup : EditorWindow
{
    // Path to the ShowcasePawn prefab in the project.
    private const string SHOWCASE_PREFAB_PATH = "Assets/Prefabs/ShowcasePawn.prefab";

    // Names of the old combat-ready pawn objects to remove.
    private static readonly string[] OLD_PAWN_NAMES =
    {
        "Player_Pawn",
        "Player_Pawn (1)",
        "Player_Pawn (2)",
        "Player_Pawn (3)"
    };

    // Hierarchy paths of the 8 UI drop zone objects (left 4 + right 4).
    private static readonly string[] DROP_ZONE_PATHS =
    {
        "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/Player",
        "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/Player (1)",
        "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/Player (2)",
        "--- UI CANVAS ---/Canvas/Left_Army_Panel/Left_Army/Army_Left/Player (3)",
        "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/Player (4)",
        "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/Player (5)",
        "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/Player (6)",
        "--- UI CANVAS ---/Canvas/Right_Army_Panel/Right_Army/Army_Right/Player (7)"
    };

    // World-space Y spacing between pawns (matches the UI slot height in world units).
    // Canvas is ScreenSpaceOverlay at 1920x1080 with scaleFactor ~2.
    // Each UI slot is 500px tall at scale 0.5 => 250 canvas units => ~2.5 world units.
    private const float PAWN_SCALE = 1.5f;

    [MenuItem("TDEV/Upgrade Scene Setup")]
    public static void RunSetup()
    {
        // ── Step 1: Load the ShowcasePawn prefab ──────────────────────────────
        GameObject showcasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SHOWCASE_PREFAB_PATH);
        if (showcasePrefab == null)
        {
            EditorUtility.DisplayDialog(
                "Hata",
                $"ShowcasePawn prefabı bulunamadı:\n{SHOWCASE_PREFAB_PATH}",
                "Tamam");
            return;
        }

        // ── Step 2: Delete old combat pawns ───────────────────────────────────
        int deletedCount = 0;
        foreach (string name in OLD_PAWN_NAMES)
        {
            GameObject old = GameObject.Find(name);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old);
                deletedCount++;
            }
        }

        // ── Step 3: Find or create the Pawns container ────────────────────────
        GameObject pawnsContainer = GameObject.Find("--- SHOWCASE PAWNS ---");
        if (pawnsContainer == null)
        {
            pawnsContainer = new GameObject("--- SHOWCASE PAWNS ---");
            Undo.RegisterCreatedObjectUndo(pawnsContainer, "Create Pawns Container");
        }

        // ── Step 4: Spawn ShowcasePawns at each drop zone position ────────────
        int spawnedCount = 0;

        for (int i = 0; i < DROP_ZONE_PATHS.Length; i++)
        {
            // Find the UI drop zone object.
            GameObject dropZoneGO = FindByPath(DROP_ZONE_PATHS[i]);
            if (dropZoneGO == null)
            {
                Debug.LogWarning($"[UpgradeSceneSetup] Drop zone bulunamadı: '{DROP_ZONE_PATHS[i]}' — atlanıyor.");
                continue;
            }

            UpgradeCharacterDropZoneUI dropZone = dropZoneGO.GetComponent<UpgradeCharacterDropZoneUI>();
            if (dropZone == null)
            {
                Debug.LogWarning($"[UpgradeSceneSetup] '{DROP_ZONE_PATHS[i]}' üzerinde UpgradeCharacterDropZoneUI yok — atlanıyor.");
                continue;
            }

            // Make the UI Image transparent so only the world-space pawn is visible.
            // The RectTransform still acts as the IDropHandler target.
            Image img = dropZoneGO.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Make Drop Zone Transparent");
                Color c = img.color;
                c.a = 0f;
                img.color = c;
            }

            // Convert the UI RectTransform's centre to world space.
            RectTransform rt = dropZoneGO.GetComponent<RectTransform>();
            Vector3 worldPos = rt != null
                ? GetRectWorldCenter(rt)
                : dropZoneGO.transform.position;

            // Check if a ShowcasePawn already exists at this slot to avoid duplicates.
            string pawnName = $"ShowcasePawn_Slot{i}";
            GameObject existingPawn = GameObject.Find(pawnName);
            if (existingPawn != null)
            {
                Undo.DestroyObjectImmediate(existingPawn);
            }

            // Instantiate the prefab.
            GameObject pawnInstance = (GameObject)PrefabUtility.InstantiatePrefab(showcasePrefab);
            pawnInstance.name = pawnName;
            Undo.RegisterCreatedObjectUndo(pawnInstance, $"Spawn ShowcasePawn Slot {i}");

            // Position in world space at the drop zone centre.
            pawnInstance.transform.SetParent(pawnsContainer.transform);
            pawnInstance.transform.position = worldPos;
            pawnInstance.transform.localScale = Vector3.one * PAWN_SCALE;

            // Add ShowcasePawnController for idle breathing.
            ShowcasePawnController controller = pawnInstance.GetComponent<ShowcasePawnController>();
            if (controller == null)
                controller = Undo.AddComponent<ShowcasePawnController>(pawnInstance);

            // Auto-wire the UnitBreathingVisuals reference.
            UnitBreathingVisuals breathing = pawnInstance.GetComponentInChildren<UnitBreathingVisuals>();
            if (breathing != null)
            {
                SerializedObject so = new SerializedObject(controller);
                so.FindProperty("_breathingVisuals").objectReferenceValue = breathing;
                so.ApplyModifiedProperties();
            }

            // Link the ShowcasePawn's CharacterEquipmentVisuals to the drop zone.
            CharacterEquipmentVisuals equipVisuals = pawnInstance.GetComponent<CharacterEquipmentVisuals>();
            if (equipVisuals != null)
            {
                SerializedObject dzSO = new SerializedObject(dropZone);
                dzSO.FindProperty("_characterVisuals").objectReferenceValue = equipVisuals;
                dzSO.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning($"[UpgradeSceneSetup] ShowcasePawn_Slot{i} üzerinde CharacterEquipmentVisuals bulunamadı.");
            }

            spawnedCount++;
        }

        // ── Step 5: Save the scene ────────────────────────────────────────────
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Kurulum Tamamlandı",
            $"Silinen eski pawn: {deletedCount}\n" +
            $"Oluşturulan ShowcasePawn: {spawnedCount}\n\n" +
            "Sahneyi kaydetmeyi unutma! (Ctrl+S)",
            "Harika!");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Finds a GameObject in the active scene by its full hierarchy path.
    /// Traverses the path segments separated by '/'.
    /// </summary>
    private static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        GameObject current = null;

        // Find the root object.
        foreach (GameObject root in UnityEngine.SceneManagement.SceneManager
                     .GetActiveScene().GetRootGameObjects())
        {
            if (root.name == parts[0])
            {
                current = root;
                break;
            }
        }

        if (current == null) return null;

        // Traverse children.
        for (int i = 1; i < parts.Length; i++)
        {
            Transform child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }

        return current;
    }

    /// <summary>
    /// Returns the world-space centre of a RectTransform.
    /// Works for both Overlay and Camera-space canvases.
    /// </summary>
    private static Vector3 GetRectWorldCenter(RectTransform rt)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // Average of the four corners gives the centre.
        return (corners[0] + corners[1] + corners[2] + corners[3]) / 4f;
    }
}
