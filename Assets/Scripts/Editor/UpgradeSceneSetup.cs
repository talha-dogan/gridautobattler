using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

/// <summary>
/// One-shot editor utility that wires up the Upgrade Scene's ShowcasePawn system.
///
/// What it does:
///   1. Deletes the old Player_Pawn (0-3) objects.
///   2. Reads the screen-space centre of each UI Player drop zone (0-7) and converts it to World Space.
///   3. Instantiates a ShowcasePawn prefab at each proper world position.
///   4. Adds ShowcasePawnController to each instance for idle breathing.
///   5. Links the ShowcasePawn's CharacterEquipmentVisuals to the matching UI.
///   6. Makes the UI Player Image fully transparent so only the world-space pawn is visible.
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

    // Hierarchy paths of the 8 UI drop zone objects.
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

    // Scale modifier for spawned pawns.
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

        // Make sure we have a Main Camera to calculate world positions.
        if (Camera.main == null)
        {
            EditorUtility.DisplayDialog(
                "Hata",
                "Sahnede 'MainCamera' etiketli bir kamera bulunamadı. Dünya koordinatları hesaplanamıyor.",
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

            // Explicitly ensure the container itself is at the world origin.
            pawnsContainer.transform.position = Vector3.zero;
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

            // Verify it has the required UI script.
            UpgradeCharacterDropZoneUI dropZone = dropZoneGO.GetComponent<UpgradeCharacterDropZoneUI>();
            if (dropZone == null)
            {
                Debug.LogWarning($"[UpgradeSceneSetup] '{DROP_ZONE_PATHS[i]}' üzerinde UpgradeCharacterDropZoneUI yok — atlanıyor.");
                continue;
            }

            // Make the UI Image transparent.
            Image img = dropZoneGO.GetComponent<Image>();
            if (img != null)
            {
                Undo.RecordObject(img, "Make Drop Zone Transparent");
                Color c = img.color;
                c.a = 0f;
                img.color = c;
            }

            // Calculate ACTUAL World Space Position from UI.
            RectTransform rt = dropZoneGO.GetComponent<RectTransform>();
            Vector3 worldPos = rt != null
                ? GetWorldPositionFromUI(rt)
                : dropZoneGO.transform.position;

            // Remove existing duplicate pawn if any.
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

            // Assign parent and correct world position/scale.
            pawnInstance.transform.SetParent(pawnsContainer.transform, true);
            pawnInstance.transform.position = worldPos;
            pawnInstance.transform.localScale = Vector3.one * PAWN_SCALE;

            // Add controller.
            ShowcasePawnController controller = pawnInstance.GetComponent<ShowcasePawnController>();
            if (controller == null)
                controller = Undo.AddComponent<ShowcasePawnController>(pawnInstance);

            // Auto-wire breathing visual.
            UnitBreathingVisuals breathing = pawnInstance.GetComponentInChildren<UnitBreathingVisuals>();
            if (breathing != null)
            {
                SerializedObject so = new SerializedObject(controller);
                so.FindProperty("_breathingVisuals").objectReferenceValue = breathing;
                so.ApplyModifiedProperties();
            }

            // Auto-wire equipment visual.
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

    private static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        GameObject current = null;

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

        for (int i = 1; i < parts.Length; i++)
        {
            Transform child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }

        return current;
    }

    /// <summary>
    /// Accurately converts a UI RectTransform's position to actual 3D/2D World Space 
    /// using the Main Camera, preventing pawns from spawning in pixel-coordinate space.
    /// </summary>
    private static Vector3 GetWorldPositionFromUI(RectTransform rt)
    {
        Camera cam = Camera.main;
        if (cam == null) return rt.position;

        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Vector3 screenPos;

        // If the Canvas is Overlay, rt.position is already in pixel coordinates.
        // If it's Camera Space or World Space, we need to convert it to screen space first.
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            screenPos = rt.position;
        }
        else
        {
            screenPos = cam.WorldToScreenPoint(rt.position);
        }

        // Calculate a proper distance from the camera.
        // For Orthographic cameras (2D), distance doesn't affect scale, but we need them in front.
        // For Perspective cameras, this determines how far away they spawn.
        float distanceFromCamera = Mathf.Abs(cam.transform.position.z);

        // Convert the pixel coordinates to actual world scene coordinates.
        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceFromCamera));

        // Force Z to 0 (Standard for 2D sorting). Change this if you need them at a specific depth.
        worldPos.z = 0f;

        return worldPos;
    }
}