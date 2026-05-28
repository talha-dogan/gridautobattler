using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TDEV.Core;

/// <summary>
/// Editor window that procedurally generates LevelDataSO + EnemyFormationSO assets
/// for the entire campaign.
///
/// Enemy difficulty scaling is driven by a designer-friendly AnimationCurve:
///
///   enemyCountCurve  – X: normalised level index (0-1)
///                      Y: enemy unit count (clamped to MAX_ENEMY_UNITS)
///
/// Player capacity uses a simple linear formula that scales predictably:
///   playerTotal = Mathf.Min(3 + (i / 15), MAX_PLAYER_UNITS)
///
/// The enemy curve is serialised on the window so it persists across editor
/// sessions and can be tweaked without touching code.
/// </summary>
public class GridLevelGenerator : EditorWindow
{
    // ── Enemy pool & player data ──────────────────────────────────────────────
    [SerializeField] private List<BaseUnitDataSO> enemyPool = new List<BaseUnitDataSO>();

    private BaseUnitDataSO playerMelee;
    private BaseUnitDataSO playerRanged;

    // ── Campaign settings ─────────────────────────────────────────────────────
    private int numberOfLevels = 100;
    private int baseGoldReward = 100;

    private const int MAX_PLAYER_UNITS = 8;
    private const int MAX_ENEMY_UNITS  = 16;
    private readonly string folderPath = "Assets/GameData/Levels";

    // ── Enemy progression curve ───────────────────────────────────────────────

    [SerializeField]
    [Tooltip("Controls how many enemy units appear at each level.\n" +
             "X axis: normalised level index (0 = level 1, 1 = last level).\n" +
             "Y axis: enemy count (will be rounded and clamped to MAX_ENEMY_UNITS).\n" +
             "Example shape: starts at 2, climbs to 16 with an ease-in curve.")]
    private AnimationCurve enemyCountCurve = new AnimationCurve(
        new Keyframe(0f,   2f),
        new Keyframe(0.5f, 8f),
        new Keyframe(1f,  16f)
    );

    // ─────────────────────────────────────────────────────────────────────────
    // Editor Window Entry Point
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("TDEV/Grid Level Generator")]
    public static void ShowWindow()
    {
        GridLevelGenerator window = GetWindow<GridLevelGenerator>("Level Generator");
        window.minSize = new Vector2(380, 520);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Epic Grid Auto-Battler Generator", EditorStyles.boldLabel);
        GUILayout.Label("Generates tactically diverse 8x8 Grid Levels", EditorStyles.miniLabel);
        GUILayout.Space(10);

        SerializedObject so = new SerializedObject(this);
        so.Update();

        // Enemy pool
        SerializedProperty enemyPoolProp = so.FindProperty("enemyPool");
        EditorGUILayout.PropertyField(enemyPoolProp, new GUIContent("Enemy Unit Pool"), true);

        GUILayout.Space(10);

        // Player unit data
        GUILayout.Label("Player Unit Setup", EditorStyles.boldLabel);
        playerMelee  = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Melee Data",  playerMelee,  typeof(BaseUnitDataSO), false);
        playerRanged = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Ranged Data", playerRanged, typeof(BaseUnitDataSO), false);

        GUILayout.Space(15);

        // Campaign settings
        GUILayout.Label("Campaign Settings", EditorStyles.boldLabel);
        numberOfLevels = EditorGUILayout.IntSlider("Levels to Generate", numberOfLevels, 10, 500);
        baseGoldReward = EditorGUILayout.IntField("Base Gold Reward", baseGoldReward);

        GUILayout.Space(15);

        // Enemy difficulty curve
        GUILayout.Label("Enemy Difficulty Curve", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "X axis = normalised level (0 → 1).  Y axis = enemy count.\n" +
            "Values are rounded to integers and clamped to MAX_ENEMY_UNITS.\n" +
            "Player capacity is calculated automatically via a linear formula.",
            MessageType.Info
        );

        SerializedProperty enemyCurveProp = so.FindProperty("enemyCountCurve");
        EditorGUILayout.PropertyField(enemyCurveProp, new GUIContent(
            $"Enemy Count Curve  (max {MAX_ENEMY_UNITS})",
            "How many enemies spawn at each level."));

        so.ApplyModifiedProperties();

        GUILayout.Space(25);

        // Generate button
        GUI.backgroundColor = new Color(0.2f, 0.6f, 0.3f);
        if (GUILayout.Button($"Generate {numberOfLevels} Diverse Levels", GUILayout.Height(40)))
        {
            if (enemyPool.Count == 0 || playerMelee == null || playerRanged == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign Enemy Pool and Player Unit Data!", "OK");
                return;
            }
            GenerateCampaign();
        }
        GUI.backgroundColor = Color.white;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Campaign Generation
    // ─────────────────────────────────────────────────────────────────────────

    private void GenerateCampaign()
    {
        CreateFolderStructure();

        try
        {
            for (int i = 1; i <= numberOfLevels; i++)
            {
                EditorUtility.DisplayProgressBar(
                    "Generating Campaign",
                    $"Forging Tactical Level {i} of {numberOfLevels}...",
                    (float)i / numberOfLevels
                );

                // ── Player capacity: linear formula (predictable, designer-approved) ──
                // Starts at 3 slots on level 1, gains one slot every 15 levels,
                // and is hard-capped at MAX_PLAYER_UNITS.
                int playerTotal = Mathf.Min(3 + (i / 15), MAX_PLAYER_UNITS);

                LevelDataSO levelData  = CreateInstance<LevelDataSO>();
                levelData.meleeData    = playerMelee;
                levelData.rangedData   = playerRanged;
                levelData.meleeLimit   = Mathf.CeilToInt(playerTotal * 0.6f);
                levelData.rangedLimit  = playerTotal - levelData.meleeLimit;

                // Gold reward scales with level; boss levels give a bonus
                bool isBossLevel     = (i % 10 == 0);
                levelData.goldReward = baseGoldReward * i * (isBossLevel ? 3 : 1);

                // ── Enemy count: AnimationCurve (designer-tweakable) ──────────
                // Normalised position of this level within the full campaign (0-1)
                float t = (numberOfLevels > 1)
                    ? (float)(i - 1) / (numberOfLevels - 1)
                    : 0f;

                int baseEnemyCount = Mathf.Clamp(
                    Mathf.RoundToInt(enemyCountCurve.Evaluate(t)),
                    2,
                    MAX_ENEMY_UNITS
                );

                // Boss levels always max out; the level right after a boss is a breather
                if (isBossLevel)
                    baseEnemyCount = MAX_ENEMY_UNITS;
                else if (i % 10 == 1 && i != 1)
                    baseEnemyCount = Mathf.Max(2, baseEnemyCount - 4);

                // ── Formation asset ───────────────────────────────────────────
                EnemyFormationSO formation = CreateInstance<EnemyFormationSO>();
                formation.units = GenerateTacticalRoster(baseEnemyCount, i, isBossLevel);

                string formationName = $"L{i}_Formation";
                AssetDatabase.CreateAsset(formation, $"{folderPath}/{formationName}.asset");

                levelData.enemyFormation = formation;
                string levelName = $"Level_{i}_DATA";
                AssetDatabase.CreateAsset(levelData, $"{folderPath}/{levelName}.asset");
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Success", $"Forged {numberOfLevels} unique tactical levels!", "Awesome");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tactical Roster Builder
    // ─────────────────────────────────────────────────────────────────────────

    private List<UnitPlacement> GenerateTacticalRoster(int count, int levelIndex, bool isBoss)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();

        BaseUnitDataSO primaryUnit   = enemyPool[Random.Range(0, enemyPool.Count)];
        BaseUnitDataSO secondaryUnit = enemyPool[Random.Range(0, enemyPool.Count)];

        if (isBoss)
        {
            // BOSS TACTIC: Pure Chaos — complete random mix, fully packed
            for (int i = 0; i < count; i++)
                placements.Add(CreatePlacement(enemyPool[Random.Range(0, enemyPool.Count)]));

            return placements;
        }

        // Cycle through 4 different tactical compositions based on level index
        int tacticType = levelIndex % 4;

        switch (tacticType)
        {
            case 0:
                // TACTIC 1: Mono-Squad — all units are the same type
                for (int i = 0; i < count; i++)
                    placements.Add(CreatePlacement(primaryUnit));
                break;

            case 1:
                // TACTIC 2: Front / Back Split
                // Spawner fills Col 7 first, so the first half acts as the frontline
                for (int i = 0; i < count; i++)
                    placements.Add(CreatePlacement(i < count / 2 ? primaryUnit : secondaryUnit));
                break;

            case 2:
                // TACTIC 3: Checkerboard — alternating unit types
                for (int i = 0; i < count; i++)
                    placements.Add(CreatePlacement(i % 2 == 0 ? primaryUnit : secondaryUnit));
                break;

            case 3:
                // TACTIC 4: Vanguard — heavy frontline, minimal backline
                int vanguardCount = Mathf.CeilToInt(count * 0.75f);
                for (int i = 0; i < count; i++)
                    placements.Add(CreatePlacement(i < vanguardCount ? primaryUnit : secondaryUnit));
                break;
        }

        return placements;
    }

    private UnitPlacement CreatePlacement(BaseUnitDataSO unit)
    {
        return new UnitPlacement
        {
            unitData = unit,
            offset   = Vector2.zero // Offset is bypassed by GridManager
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Folder Utilities
    // ─────────────────────────────────────────────────────────────────────────

    private void CreateFolderStructure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameData"))
            AssetDatabase.CreateFolder("Assets", "GameData");

        if (!AssetDatabase.IsValidFolder("Assets/GameData/Levels"))
            AssetDatabase.CreateFolder("Assets/GameData", "Levels");
    }
}
