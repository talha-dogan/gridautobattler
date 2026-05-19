using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using TDEV.Core;

public class GridLevelGenerator : EditorWindow
{
    [SerializeField] private List<BaseUnitDataSO> enemyPool = new List<BaseUnitDataSO>();

    private BaseUnitDataSO playerMelee;
    private BaseUnitDataSO playerRanged;

    private int numberOfLevels = 100;
    private int baseGoldReward = 100;

    private const int MAX_PLAYER_UNITS = 8;
    private const int MAX_ENEMY_UNITS = 16;
    private readonly string folderPath = "Assets/GameData/Levels";

    [MenuItem("TDEV/Grid Level Generator")]
    public static void ShowWindow()
    {
        GridLevelGenerator window = GetWindow<GridLevelGenerator>("Level Generator");
        window.minSize = new Vector2(350, 450);
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Epic Grid Auto-Battler Generator", EditorStyles.boldLabel);
        GUILayout.Label("Generates tactically diverse 8x8 Grid Levels", EditorStyles.miniLabel);
        GUILayout.Space(10);

        SerializedObject so = new SerializedObject(this);
        so.Update();

        SerializedProperty enemyPoolProp = so.FindProperty("enemyPool");
        EditorGUILayout.PropertyField(enemyPoolProp, new GUIContent("Enemy Unit Pool"), true);

        GUILayout.Space(10);

        GUILayout.Label("Player Unit Setup", EditorStyles.boldLabel);
        playerMelee = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Melee Data", playerMelee, typeof(BaseUnitDataSO), false);
        playerRanged = (BaseUnitDataSO)EditorGUILayout.ObjectField("Player Ranged Data", playerRanged, typeof(BaseUnitDataSO), false);

        so.ApplyModifiedProperties();

        GUILayout.Space(15);

        GUILayout.Label("Campaign Settings", EditorStyles.boldLabel);
        numberOfLevels = EditorGUILayout.IntSlider("Levels to Generate", numberOfLevels, 10, 500);
        baseGoldReward = EditorGUILayout.IntField("Base Gold Reward", baseGoldReward);

        GUILayout.Space(25);

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

    private void GenerateCampaign()
    {
        CreateFolderStructure();

        try
        {
            for (int i = 1; i <= numberOfLevels; i++)
            {
                EditorUtility.DisplayProgressBar("Generating Campaign", $"Forging Tactical Level {i} of {numberOfLevels}...", (float)i / numberOfLevels);

                LevelDataSO levelData = CreateInstance<LevelDataSO>();
                levelData.meleeData = playerMelee;
                levelData.rangedData = playerRanged;

                // Player progression: Smoothly scales up to MAX_PLAYER_UNITS
                int playerTotal = Mathf.Min(3 + (i / 15), MAX_PLAYER_UNITS);
                levelData.meleeLimit = Mathf.CeilToInt(playerTotal * 0.6f);
                levelData.rangedLimit = playerTotal - levelData.meleeLimit;

                // Gold reward scales with level, Boss levels give bonus
                bool isBossLevel = (i % 10 == 0);
                levelData.goldReward = baseGoldReward * i * (isBossLevel ? 3 : 1);

                EnemyFormationSO formation = CreateInstance<EnemyFormationSO>();

                // Enemy scaling with difficulty breathing (drops after a boss)
                int baseEnemyCount = Mathf.Clamp(2 + (i / 7), 2, MAX_ENEMY_UNITS);

                if (isBossLevel)
                    baseEnemyCount = MAX_ENEMY_UNITS; // Maximum threat
                else if (i % 10 == 1 && i != 1)
                    baseEnemyCount = Mathf.Max(2, baseEnemyCount - 4); // Breather level after boss

                // Generate a specific tactical roster for this level
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

    private void CreateFolderStructure()
    {
        if (!AssetDatabase.IsValidFolder("Assets/GameData"))
            AssetDatabase.CreateFolder("Assets", "GameData");

        if (!AssetDatabase.IsValidFolder("Assets/GameData/Levels"))
            AssetDatabase.CreateFolder("Assets/GameData", "Levels");
    }

    private List<UnitPlacement> GenerateTacticalRoster(int count, int levelIndex, bool isBoss)
    {
        List<UnitPlacement> placements = new List<UnitPlacement>();

        // Ensure we have at least 2 distinct enemies in the pool for advanced tactics
        BaseUnitDataSO primaryUnit = enemyPool[Random.Range(0, enemyPool.Count)];
        BaseUnitDataSO secondaryUnit = enemyPool[Random.Range(0, enemyPool.Count)];

        if (isBoss)
        {
            // BOSS TACTIC: Pure Chaos (Complete random mix, fully packed)
            for (int i = 0; i < count; i++)
            {
                placements.Add(CreatePlacement(enemyPool[Random.Range(0, enemyPool.Count)]));
            }
            return placements;
        }

        // Cycle through 4 different tactical compositions
        int tacticType = levelIndex % 4;

        switch (tacticType)
        {
            case 0:
                // TACTIC 1: Mono-Squad (All units are exactly the same type)
                for (int i = 0; i < count; i++)
                {
                    placements.Add(CreatePlacement(primaryUnit));
                }
                break;

            case 1:
                // TACTIC 2: Front / Back Split 
                // Since Spawner fills Col 7 first, first half acts as frontline
                for (int i = 0; i < count; i++)
                {
                    placements.Add(CreatePlacement(i < count / 2 ? primaryUnit : secondaryUnit));
                }
                break;

            case 2:
                // TACTIC 3: Checkerboard / Alternating Mix
                for (int i = 0; i < count; i++)
                {
                    placements.Add(CreatePlacement(i % 2 == 0 ? primaryUnit : secondaryUnit));
                }
                break;

            case 3:
                // TACTIC 4: Vanguard (Heavy frontline, minimal backline)
                int vanguardCount = Mathf.CeilToInt(count * 0.75f);
                for (int i = 0; i < count; i++)
                {
                    placements.Add(CreatePlacement(i < vanguardCount ? primaryUnit : secondaryUnit));
                }
                break;
        }

        return placements;
    }

    private UnitPlacement CreatePlacement(BaseUnitDataSO unit)
    {
        return new UnitPlacement
        {
            unitData = unit,
            offset = Vector2.zero // Offset bypassed by GridManager
        };
    }
}